using System.Collections.Generic;
using Marketplace.Models;

namespace Marketplace.ViewModels
{
    public class AdminPanelViewModel
    {
        public string Username { get; set; }
        public string UserId { get; set; }
        public bool UserNotFound { get; set; }
        public bool UserAccountUpdated { get; set; }
        public string SearchTerm { get; set; }
        public string RoleFilter { get; set; } = "all";
        public int PageNumber { get; set; } = 0;
        public int MaxCountPages { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public List<AdminUserListItemViewModel> SearchResults { get; set; } = new List<AdminUserListItemViewModel>();

        // Flags about the currently selected user (for the management panel).
        public bool IsTargetAdmin { get; set; }
        public bool IsTargetSelf { get; set; }
        public AccountStatus TargetStatus { get; set; } = AccountStatus.Active;
        public string? TargetBanReason { get; set; }
    }
}
