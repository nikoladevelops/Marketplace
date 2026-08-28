using Marketplace.Models;
using Marketplace.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Hubs
{
    // Real time chat using SignalR.
    // Handles sending, syncing and marking messages as read.
    // Each user joins a personal group so messages go to both participants.
    [Authorize]
    public class ChatHub : Hub
    {
        private const int MaxSyncMessages = 200;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // Creates the hub with db and user manager.
        public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Gets messages in this ad and partner thread newer than the given id.
        // Used to catch up after a disconnect.
        public async Task<List<ChatSyncItem>> GetMessagesSince(int adId, string with, int afterMessageId)
        {
            var partner = await ResolvePartnerAsync(with);
            var meId = _userManager.GetUserId(Context.User!);

            if (partner == null || meId == null || meId == partner.Id)
            {
                return new List<ChatSyncItem>();
            }

            return await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.AdvertisementId == adId &&
                            ((m.SenderId == meId && m.ReceiverId == partner.Id) ||
                             (m.SenderId == partner.Id && m.ReceiverId == meId)) &&
                            m.Id > afterMessageId)
                .OrderBy(m => m.Id)
                .Take(MaxSyncMessages)
                .Select(m => new ChatSyncItem
                {
                    Id = m.Id,
                    Body = m.Body,
                    SentAt = m.SentAt,
                    SenderName = m.Sender.UserName!,
                    IsReadByReceiver = m.IsReadByReceiver
                })
                .ToListAsync();
        }

        // Called when opening a thread. Marks pending messages from the partner as read.
        public async Task JoinThread(int adId, string with)
        {
            var partner = await ResolvePartnerAsync(with);
            var meId = _userManager.GetUserId(Context.User!);

            if (partner == null || meId == null || meId == partner.Id)
            {
                return;
            }

            await BroadcastReadReceiptsAsync(adId, meId, partner);
        }

        // Called when a live message is visible. Marks the thread as read.
        public async Task MarkThreadRead(int adId, string with)
        {
            var partner = await ResolvePartnerAsync(with);
            var meId = _userManager.GetUserId(Context.User!);

            if (partner == null || meId == null || meId == partner.Id)
            {
                return;
            }

            await BroadcastReadReceiptsAsync(adId, meId, partner);
        }

        // Sends one message to the partner. Checks auth, blocks, and ad existence.
        public async Task SendMessage(int adId, string with, string message)
        {
            message = message?.Trim() ?? "";

            if (message.Length == 0 || message.Length > 1000)
            {
                throw new HubException("Message must be between 1 and 1000 characters.");
            }

            var me = await _userManager.GetUserAsync(Context.User!);
            var partner = await ResolvePartnerAsync(with);

            if (me == null || partner == null)
            {
                throw new HubException("User not found.");
            }

            if (me.Id == partner.Id)
            {
                throw new HubException("You cannot message yourself.");
            }

            var ad = await _context.Advertisements.FindAsync(adId);

            if (ad == null)
            {
                throw new HubException("Advertisement not found.");
            }

            // Admins bypass blocks, everyone else requires a block free pair.
            if (!Context.User!.IsInRole(Helper.AdminRole))
            {
                var anyBlock = await _context.UserBlocks.AnyAsync(b =>
                    (b.BlockerId == me.Id && b.BlockedId == partner.Id) ||
                    (b.BlockerId == partner.Id && b.BlockedId == me.Id));

                if (anyBlock)
                {
                    throw new HubException("Messaging is unavailable because one of you has blocked the other.");
                }
            }

            var msg = new ChatMessage
            {
                SenderId = me.Id,
                ReceiverId = partner.Id,
                AdvertisementId = adId,
                Body = message,
                SentAt = DateTime.UtcNow,
                IsReadByReceiver = false
            };

            _context.ChatMessages.Add(msg);

            await _context.SaveChangesAsync();

            var payload = new
            {
                id = msg.Id,
                body = msg.Body,
                sentAt = msg.SentAt,
                senderName = me.UserName!,
                advertisementId = adId
            };

            await Clients.Group(UserGroup(me.Id)).SendAsync("ReceiveMessage", payload);
            await Clients.Group(UserGroup(partner.Id)).SendAsync("ReceiveMessage", payload);
        }

        // Adds the connection to the user's personal group.
        public override async Task OnConnectedAsync()
        {
            var meId = _userManager.GetUserId(Context.User!);

            if (meId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(meId));
            }

            await base.OnConnectedAsync();
        }

        // Removes the connection from the user's group.
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var meId = _userManager.GetUserId(Context.User!);

            if (meId != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(meId));
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Looks up the partner user by name.
        private async Task<ApplicationUser?> ResolvePartnerAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return await _userManager.FindByNameAsync(userName);
        }

        // Marks pending messages as read and notifies both sides.
        private async Task BroadcastReadReceiptsAsync(int adId, string meId, ApplicationUser partner)
        {
            var myName = Context.User?.Identity?.Name ?? "";

            var changed = await _context.ChatMessages
                .Where(m => m.AdvertisementId == adId &&
                            m.ReceiverId == meId &&
                            m.SenderId == partner.Id &&
                            !m.IsReadByReceiver)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsReadByReceiver, true)) > 0;

            if (!changed)
            {
                return;
            }

            var receipt = new { byUserName = myName };

            await Clients.Group(UserGroup(meId)).SendAsync("MessagesRead", receipt);
            await Clients.Group(UserGroup(partner.Id)).SendAsync("MessagesRead", receipt);
        }

        // Helper to get the group name for a user.
        internal static string UserGroup(string userId)
        {
            return $"user-{userId}";
        }
    }

    // One message returned by GetMessagesSince.
    public class ChatSyncItem
    {
        public int Id { get; set; }

        public string Body { get; set; } = "";

        public DateTime SentAt { get; set; }

        public string SenderName { get; set; } = "";

        public bool IsReadByReceiver { get; set; }
    }
}
