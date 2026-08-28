using System;

namespace Marketplace.Models
{
    // Simple audit log for bans and unbans.
    // Every time an admin bans or unbans someone we add a new row here.
    // Never edited or deleted, just appended.
    public class UserBanHistory
    {
        public int Id { get; set; }

        // Who was banned or unbanned
        public string UserId { get; set; } = "";

        public ApplicationUser? User { get; set; }

        // What happened - "ban" or "unban"
        public string Action { get; set; } = "";

        // Optional reason given at that time
        public string? Reason { get; set; }

        // Which admin did it
        public string AdminUserId { get; set; } = "";

        public ApplicationUser? AdminUser { get; set; }

        // When it happened (UTC)
        public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
