using Marketplace.Hubs;
using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Services
{
    // ChatService - handles inbox, threads, read receipts and blocking.
    public class ChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _chatHub;

        // ChatService - set up DB, user manager and SignalR hub.
        public ChatService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> chatHub)
        {
            _context = context;
            _userManager = userManager;
            _chatHub = chatHub;
        }

        // GetInboxAsync - builds the chat inbox grouped by ad and partner.
        public async Task<ChatInboxViewModel> GetInboxAsync(string userId, int page = 1, int pageSize = 12)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .Select(m => new
                {
                    m.AdvertisementId,
                    m.Body,
                    m.SentAt,
                    m.IsReadByReceiver,
                    IsToMe = m.ReceiverId == userId,
                    PartnerId = m.SenderId == userId ? m.ReceiverId : m.SenderId
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

            return new ChatInboxViewModel
            {
                Items = paged,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        // GetUnreadCountAsync - counts unread messages for a user.
        public async Task<int> GetUnreadCountAsync(string userId) =>
            await _context.ChatMessages.CountAsync(m => m.ReceiverId == userId && !m.IsReadByReceiver);

        public enum ThreadOutcome
        {
            Ok,
            PartnerNotFound,
            AdNotFound
        }

        // GetThreadAsync - loads a chat thread, marks messages read, returns paging info.
        public async Task<(ThreadOutcome Outcome, ChatThreadViewModel? ViewModel)> GetThreadAsync(
            string with, int adId, string viewerId, string viewerName, bool viewerIsAdmin, int page = 1, int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var partner = await _userManager.FindByNameAsync(with);

            if (partner == null)
            {
                return (ThreadOutcome.PartnerNotFound, null);
            }

            var ad = await _context.Advertisements.FindAsync(adId);

            if (ad == null)
            {
                return (ThreadOutcome.AdNotFound, null);
            }

            var partnerIsAdmin = await _userManager.IsInRoleAsync(partner, Helper.AdminRole);

            // Opening the thread marks the partner's messages as read.

            var unread = await _context.ChatMessages
                .Where(m => m.AdvertisementId == adId
                            && m.ReceiverId == viewerId
                            && m.SenderId == partner.Id
                            && !m.IsReadByReceiver)
                .ToListAsync();

            foreach (var m in unread)
            {
                m.IsReadByReceiver = true;
            }

            if (unread.Count > 0)
            {
                await _context.SaveChangesAsync();

                var receipt = new { byUserName = viewerName };

                await _chatHub.Clients.Group(ChatHub.UserGroup(viewerId)).SendAsync("MessagesRead", receipt);
                await _chatHub.Clients.Group(ChatHub.UserGroup(partner.Id)).SendAsync("MessagesRead", receipt);
            }

            var baseQuery = _context.ChatMessages.AsNoTracking()
                .Where(m => m.AdvertisementId == adId &&
                            ((m.SenderId == viewerId && m.ReceiverId == partner.Id) ||
                             (m.SenderId == partner.Id && m.ReceiverId == viewerId)));

            var totalMessages = await baseQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalMessages / (double)pageSize));

            page = Math.Clamp(page, 1, totalPages);

            // Page 1 = newest (most recent), page 2 = older, etc.

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
                    IsMine = m.SenderId == viewerId,
                    IsReadByReceiver = m.IsReadByReceiver,
                    SenderName = m.Sender.UserName!
                })
                .ToListAsync();

            var blockedByMe = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == viewerId && b.BlockedId == partner.Id);

            var hasBlockedMe = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == partner.Id && b.BlockedId == viewerId);

            // Already reported by me for this thread
            var threadKey = BuildThreadKey(ad.Id, viewerId, partner.Id);
            var alreadyReported = await _context.ChatReports
                .AnyAsync(r => r.ReporterId == viewerId && r.ThreadKey == threadKey);

            // Phone for quick share button
            var meUser = await _context.Users.AsNoTracking()
                .Where(u => u.Id == viewerId)
                .Select(u => new { u.PhoneNumber })
                .FirstOrDefaultAsync();

            var myPhone = meUser?.PhoneNumber;
            var hasValidPhone = !string.IsNullOrWhiteSpace(myPhone) && IsValidPhone(myPhone);

            return (ThreadOutcome.Ok, new ChatThreadViewModel
            {
                PartnerName = partner.UserName!,
                MyUserName = viewerName,
                IsPartnerAdmin = partnerIsAdmin,
                AdvertisementId = ad.Id,
                AdvertisementTitle = ad.Title,
                AdvertisementPrice = ad.Price + " EUR",
                AdvertisementImagePath = ad.ImagePath,
                Messages = messages,
                IsBlockedByMe = blockedByMe,
                HasBlockedMe = hasBlockedMe,
                CanSend = viewerIsAdmin || (!blockedByMe && !hasBlockedMe),
                AlreadyReportedByMe = alreadyReported,
                MyPhoneNumber = myPhone,
                HasValidPhone = hasValidPhone,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalMessages
            });
        }

        // GetMessagesForAdminAsync - admin view of any thread, bypassing blocks.
        // No block checks, just pure message history between two users for an ad.
        // Private on purpose: only callable via report-gated GetAdminChatLogForReportAsync to avoid arbitrary chat disclosure.
        private async Task<List<ChatMessageViewModel>> GetMessagesForAdminAsync(int adId, string userAId, string userBId, int take = 100)
        {
            take = Math.Clamp(take, 1, 200);

            var messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.AdvertisementId == adId &&
                            ((m.SenderId == userAId && m.ReceiverId == userBId) ||
                             (m.SenderId == userBId && m.ReceiverId == userAId)))
                .OrderBy(m => m.SentAt)
                .Take(take)
                .Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    Body = m.Body,
                    SentAt = m.SentAt,
                    IsMine = false,
                    IsReadByReceiver = m.IsReadByReceiver,
                    SenderName = m.Sender.UserName!
                })
                .ToListAsync();

            return messages;
        }

        // GetAdminChatLogForReportAsync - admin reads the exact reported thread.
        // Uses the stored Report.ReporterId / ReportedUserId / AdvertisementId.
        // Handles edge cases: report missing, ad deleted, no messages.
        public async Task<AdminChatLogViewModel?> GetAdminChatLogForReportAsync(int reportId, int take = 100)
        {
            var report = await _context.ChatReports
                .AsNoTracking()
                .Include(r => r.Reporter)
                .Include(r => r.ReportedUser)
                .Include(r => r.Advertisement)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return null;
            }

            var messages = await GetMessagesForAdminAsync(report.AdvertisementId, report.ReporterId, report.ReportedUserId, take);

            var adTitle = report.Advertisement?.Title ?? "(deleted advertisement)";
            var adImage = report.Advertisement?.ImagePath ?? "";
            var reporterName = report.Reporter?.UserName ?? "Unknown user";
            var reportedName = report.ReportedUser?.UserName ?? "Unknown user";

            return new AdminChatLogViewModel
            {
                Report = report,
                Messages = messages,
                ReporterName = reporterName,
                ReportedName = reportedName,
                AdTitle = adTitle,
                AdImagePath = adImage,
                AdvertisementId = report.AdvertisementId
            };
        }

        public enum BlockOutcome
        {
            Ok,
            SelfBlock,
            AdminTarget
        }

        // BlockAsync - block a user unless self or admin.
        public async Task<(BlockOutcome Outcome, ApplicationUser? Partner)> BlockAsync(string viewerId, string with)
        {
            var partner = await _userManager.FindByNameAsync(with);

            if (partner == null)
            {
                return (BlockOutcome.Ok, null);
            }

            if (viewerId == partner.Id)
            {
                return (BlockOutcome.SelfBlock, partner);
            }

            if (await _userManager.IsInRoleAsync(partner, Helper.AdminRole))
            {
                return (BlockOutcome.AdminTarget, partner);
            }

            var exists = await _context.UserBlocks
                .AnyAsync(b => b.BlockerId == viewerId && b.BlockedId == partner.Id);

            if (!exists)
            {
                _context.UserBlocks.Add(new UserBlock
                {
                    BlockerId = viewerId,
                    BlockedId = partner.Id
                });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Concurrent duplicate - treat as already blocked.
                }
            }

            return (BlockOutcome.Ok, partner);
        }

        // UnblockAsync - remove a block if it exists.
        public async Task<ApplicationUser?> UnblockAsync(string viewerId, string with)
        {
            var partner = await _userManager.FindByNameAsync(with);

            if (partner == null)
            {
                return null;
            }

            var block = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerId == viewerId && b.BlockedId == partner.Id);

            if (block != null)
            {
                _context.UserBlocks.Remove(block);

                await _context.SaveChangesAsync();
            }

            return partner;
        }

        // ReportOutcome - result of trying to report a chat
        public enum ReportOutcome
        {
            Ok,
            AlreadyReported,
            PartnerNotFound,
            AdNotFound,
            SelfReport,
            InvalidInput,
            AdminTarget
        }

        // ReportThreadAsync - report a chat thread, once per reporter per thread
        public async Task<ReportOutcome> ReportThreadAsync(string reporterId, string with, int adId, ReportReason reason, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return ReportOutcome.InvalidInput;
            }

            description = description.Trim();

            if (description.Length < 20 || description.Length > 500)
            {
                return ReportOutcome.InvalidInput;
            }

            var partner = await _userManager.FindByNameAsync(with);

            if (partner == null)
            {
                return ReportOutcome.PartnerNotFound;
            }

            if (reporterId == partner.Id)
            {
                return ReportOutcome.SelfReport;
            }

            // Trusted users - admins cannot be reported.

            if (await _userManager.IsInRoleAsync(partner, Helper.AdminRole))
            {
                return ReportOutcome.AdminTarget;
            }

            var ad = await _context.Advertisements.FindAsync(adId);

            if (ad == null)
            {
                return ReportOutcome.AdNotFound;
            }

            var threadKey = BuildThreadKey(adId, reporterId, partner.Id);

            var exists = await _context.ChatReports
                .AnyAsync(r => r.ReporterId == reporterId && r.ThreadKey == threadKey);

            if (exists)
            {
                return ReportOutcome.AlreadyReported;
            }

            var report = new ChatReport
            {
                ReporterId = reporterId,
                ReportedUserId = partner.Id,
                AdvertisementId = adId,
                ThreadKey = threadKey,
                Reason = reason,
                Description = description,
                Status = ReportStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ChatReports.Add(report);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Concurrent duplicate - treat as already reported.

                return ReportOutcome.AlreadyReported;
            }

            return ReportOutcome.Ok;
        }

        // BuildThreadKey - stable key for a chat thread, sorted user ids so both sides match
        private static string BuildThreadKey(int adId, string userA, string userB)
        {
            if (string.CompareOrdinal(userA, userB) > 0)
            {
                (userA, userB) = (userB, userA);
            }

            return $"{adId}:{userA}:{userB}";
        }

        // IsValidPhone - simple check, 8-15 digits
        private static bool IsValidPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }

            var trimmed = phone.Trim();

            if (trimmed.Length < 8 || trimmed.Length > 15)
            {
                return false;
            }

            foreach (var ch in trimmed)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }

            return true;
        }

        public enum PhoneShareOutcome
        {
            Ok,
            NoPhone,
            Blocked,
            PartnerNotFound,
            AdNotFound
        }

        // SendPhoneAsync - sends your phone number as a chat message, one click
        public async Task<PhoneShareOutcome> SendPhoneAsync(string senderId, string with, int adId)
        {
            var partner = await _userManager.FindByNameAsync(with);

            if (partner == null)
            {
                return PhoneShareOutcome.PartnerNotFound;
            }

            var ad = await _context.Advertisements.FindAsync(adId);

            if (ad == null)
            {
                return PhoneShareOutcome.AdNotFound;
            }

            if (senderId == partner.Id)
            {
                return PhoneShareOutcome.Blocked;
            }

            // Respect blocks, admins bypass
            var meUser = await _context.Users.AsNoTracking()
                .Where(u => u.Id == senderId)
                .Select(u => new { u.PhoneNumber })
                .FirstOrDefaultAsync();

            var phone = meUser?.PhoneNumber;

            if (!IsValidPhone(phone))
            {
                return PhoneShareOutcome.NoPhone;
            }

            var blockedByMe = await _context.UserBlocks.AnyAsync(b => b.BlockerId == senderId && b.BlockedId == partner.Id);
            var hasBlockedMe = await _context.UserBlocks.AnyAsync(b => b.BlockerId == partner.Id && b.BlockedId == senderId);

            if (blockedByMe || hasBlockedMe)
            {
                // Check if sender is admin, then allow
                var sender = await _userManager.FindByIdAsync(senderId);

                if (sender != null && !await _userManager.IsInRoleAsync(sender, Helper.AdminRole))
                {
                    return PhoneShareOutcome.Blocked;
                }
            }

            var msg = new ChatMessage
            {
                SenderId = senderId,
                ReceiverId = partner.Id,
                AdvertisementId = adId,
                Body = "📞 My phone: " + phone!.Trim(),
                SentAt = DateTime.UtcNow,
                IsReadByReceiver = false
            };

            _context.ChatMessages.Add(msg);

            await _context.SaveChangesAsync();

            var senderName = (await _userManager.FindByIdAsync(senderId))?.UserName ?? "Unknown";

            var payload = new
            {
                id = msg.Id,
                body = msg.Body,
                sentAt = msg.SentAt,
                senderName = senderName,
                advertisementId = adId
            };

            await _chatHub.Clients.Group(ChatHub.UserGroup(senderId)).SendAsync("ReceiveMessage", payload);
            await _chatHub.Clients.Group(ChatHub.UserGroup(partner.Id)).SendAsync("ReceiveMessage", payload);

            return PhoneShareOutcome.Ok;
        }
    }
}
