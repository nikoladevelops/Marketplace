using System.Collections.Generic;
using Marketplace.Models;

namespace Marketplace.ViewModels
{
    // View model for the admin panel page.
    // Handles searching for users and showing admin actions.
    public class AdminPanelViewModel
    {
        public string Username { get; set; } = "";

        public string UserId { get; set; } = "";

        public bool UserNotFound { get; set; }

        public bool UserAccountUpdated { get; set; }

        public string SearchTerm { get; set; } = "";

        public string RoleFilter { get; set; } = "all";

        // Paging for search results
        public int PageNumber { get; set; } = 0;

        public int MaxCountPages { get; set; } = 0;

        public int PageSize { get; set; } = 20;

        public int TotalCount { get; set; }

        // Results for the current search
        public List<AdminUserListItemViewModel> SearchResults { get; set; } = new List<AdminUserListItemViewModel>();

        // Info about the currently selected user (for the manage panel)
        public bool IsTargetAdmin { get; set; }

        public bool IsTargetSelf { get; set; }

        public AccountStatus TargetStatus { get; set; } = AccountStatus.Active;

        public string? TargetBanReason { get; set; }
    }
}
