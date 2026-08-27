using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? ProfilePicturePath { get; set; }
        public string? Description { get; set; }

        /// <summary>Opt-in visibility: default hidden (false) to avoid spam. Owner can toggle.</summary>
        public bool ShowEmail { get; set; } = false;
        public bool ShowPhone { get; set; } = false;

        /// <summary>Account-level status. Admins can ban/unban; banned users cannot sign in.</summary>
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        /// <summary>Reason recorded at the time of ban. Null when active.</summary>
        public string? BanReason { get; set; }

        public DateTime? BannedAtUtc { get; set; }

        public string? BannedByUserId { get; set; }
        public ApplicationUser? BannedByUser { get; set; }
    }
}
