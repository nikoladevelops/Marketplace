using Marketplace.Models;

namespace Marketplace.ViewModels
{
    public class AdminUserListItemViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsPremium { get; set; }
        public bool IsSeller { get; set; }
        public AccountStatus Status { get; set; } = AccountStatus.Active;
        public string? BanReason { get; set; }
        public DateTime? BannedAtUtc { get; set; }
    }
}
