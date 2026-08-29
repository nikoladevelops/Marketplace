using Marketplace.Models;

namespace Marketplace.ViewModels
{
    // One row in the admin user list.
    // Simple data holder for displaying a user in search results.
    public class AdminUserListItemViewModel
    {
        public string UserId { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Email { get; set; } = "";

        public bool IsAdmin { get; set; }

        public bool IsPremium { get; set; }

        public bool IsSeller { get; set; }

        // Current account status and ban info
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        public string? BanReason { get; set; }

        public DateTime? BannedAtUtc { get; set; }

        // How many times this user was reported (total)
        public int ReportCount { get; set; }

        // How many users blocked this user
        public int BlockedByCount { get; set; }
    }
}
