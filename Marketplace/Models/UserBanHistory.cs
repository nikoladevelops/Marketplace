using System;

namespace Marketplace.Models
{
    /// <summary>
    /// Append-only audit record of ban and unban actions taken by admins.
    /// </summary>
    public class UserBanHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        /// <summary>"ban" or "unban".</summary>
        public string Action { get; set; } = "";

        public string? Reason { get; set; }

        public string AdminUserId { get; set; } = "";
        public ApplicationUser? AdminUser { get; set; }

        public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
