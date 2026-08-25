using Marketplace.Hubs;
using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _chatHub;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> chatHub)
        {
            _context = context;
            _userManager = userManager;
            _chatHub = chatHub;
        }

        public async Task<IActionResult> Inbox(int page = 1, int pageSize = 12)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);
            var meId = _userManager.GetUserId(User);

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SenderId == meId || m.ReceiverId == meId)
                .Select(m => new
                {
                    m.AdvertisementId,
                    m.Body,
                    m.SentAt,
                    m.IsReadByReceiver,
                    IsToMe = m.ReceiverId == meId,
                    PartnerId = m.SenderId == meId ? m.ReceiverId : m.SenderId
                })
                .ToListAsync();

            var partnerIds = messages.Select(m => m.PartnerId).Distinct().ToList();
            var adIds = messages.Select(m => m.AdvertisementId).Distinct().ToList();

            var partnerNames = await _context.Users
                .Where(u => partnerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName })
                .ToDictionaryAsync(u => u.Id, u => u.UserName);

            var ads = await _context.Advertisements
                .Where(a => adIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Title, a.ImagePath })
                .ToDictionaryAsync(a => a.Id);

            var allConversations = messages
                .GroupBy(m => new { m.AdvertisementId, m.PartnerId })
                .Select(g =>
                {
                    var last = g.OrderByDescending(m => m.SentAt).First();
                    ads.TryGetValue(g.Key.AdvertisementId, out var ad);
                    return new ChatInboxItemViewModel
                    {
                        AdvertisementId = g.Key.AdvertisementId,
                        PartnerName = partnerNames.GetValueOrDefault(g.Key.PartnerId, "Unknown user") ?? "Unknown user",
                        AdvertisementTitle = ad?.Title ?? "(deleted advertisement)",
                        AdvertisementImagePath = ad?.ImagePath ?? "",
                        Snippet = last.Body,
                        LastSentAt = last.SentAt,
                        UnreadCount = g.Count(m => m.IsToMe && !m.IsReadByReceiver)
                    };
                })
                .OrderByDescending(i => i.LastSentAt)
                .ToList();

            var total = allConversations.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var paged = allConversations.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var vm = new ChatInboxViewModel
            {
                Items = paged,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var meId = _userManager.GetUserId(User);
            if (meId == null) return Json(new { count = 0 });
            var c = await _context.ChatMessages.CountAsync(m => m.ReceiverId == meId && !m.IsReadByReceiver);
            return Json(new { count = c });
        }

        [HttpGet]
        public async Task<IActionResult> Thread(string with, int adId, int page = 1, int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);
            var partner = await _userManager.FindByNameAsync(with);
            if (partner == null) return NotFound();

            var ad = await _context.Advertisements.FindAsync(adId);
            if (ad == null) return NotFound();

            var meId = _userManager.GetUserId(User);
            var amAdmin = User.IsInRole(Helper.AdminRole);
            var partnerIsAdmin = await _userManager.IsInRoleAsync(partner, Helper.AdminRole);

            // Opening the thread marks the partner's messages as read.
            var unread = await _context.ChatMessages
                .Where(m => m.AdvertisementId == adId
                            && m.ReceiverId == meId && m.SenderId == partner.Id
                            && !m.IsReadByReceiver)
                .ToListAsync();
            foreach (var m in unread)
            {
                m.IsReadByReceiver = true;
            }
            if (unread.Count > 0)
            {
                await _context.SaveChangesAsync();
                var receipt = new { byUserName = User.Identity?.Name ?? "" };
                await _chatHub.Clients.Group(ChatHub.UserGroup(meId!)).SendAsync("MessagesRead", receipt);
                await _chatHub.Clients.Group(ChatHub.UserGroup(partner.Id)).SendAsync("MessagesRead", receipt);
            }

            var baseQuery = _context.ChatMessages.AsNoTracking()
                .Where(m => m.AdvertisementId == adId &&
                            ((m.SenderId == meId && m.ReceiverId == partner.Id) ||
                             (m.SenderId == partner.Id && m.ReceiverId == meId)));

            var totalMessages = await baseQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalMessages / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            // Page 1 = newest messages (most recent), page 2 = older etc.
            var messages = await baseQuery
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt)
                .Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    Body = m.Body,
                    SentAt = m.SentAt,
                    IsMine = m.SenderId == meId,
                    IsReadByReceiver = m.IsReadByReceiver,
                    SenderName = m.Sender.UserName!
                })
                .ToListAsync();

            bool blockedByMe = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == meId && b.BlockedId == partner.Id);
            bool hasBlockedMe = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == partner.Id && b.BlockedId == meId);

            var vm = new ChatThreadViewModel
            {
                PartnerName = partner.UserName!,
                MyUserName = User.Identity?.Name ?? "",
                IsPartnerAdmin = partnerIsAdmin,
                AdvertisementId = ad.Id,
                AdvertisementTitle = ad.Title,
                AdvertisementPrice = ad.Price + " EUR",
                AdvertisementImagePath = ad.ImagePath,
                Messages = messages,
                IsBlockedByMe = blockedByMe,
                HasBlockedMe = hasBlockedMe,
                CanSend = amAdmin || (!blockedByMe && !hasBlockedMe),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalMessages
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Block(string with, int adId)
        {
            var partner = await _userManager.FindByNameAsync(with);
            if (partner == null) return NotFound();

            var meId = _userManager.GetUserId(User);
            if (meId == partner.Id)
            {
                TempData["ChatError"] = "You cannot block yourself.";
                return RedirectToAction(nameof(Thread), new { with, adId });
            }
            if (await _userManager.IsInRoleAsync(partner, Helper.AdminRole))
            {
                TempData["ChatError"] = "Administrators cannot be blocked.";
                return RedirectToAction(nameof(Thread), new { with, adId });
            }

            bool exists = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == meId && b.BlockedId == partner.Id);
            if (!exists)
            {
                _context.UserBlocks.Add(new UserBlock { BlockerId = meId!, BlockedId = partner.Id });
                await _context.SaveChangesAsync();
                TempData["ChatNotice"] = $"{partner.UserName} can no longer message you.";
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unblock(string with, int adId)
        {
            var partner = await _userManager.FindByNameAsync(with);
            if (partner == null) return NotFound();

            var meId = _userManager.GetUserId(User);
            var block = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == meId! && b.BlockedId == partner.Id);
            if (block != null)
            {
                _context.UserBlocks.Remove(block);
                await _context.SaveChangesAsync();
                TempData["ChatNotice"] = $"{partner.UserName} can message you again.";
            }

            return RedirectToAction(nameof(Thread), new { with, adId });
        }
    }
}
