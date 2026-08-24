using System.Collections.Generic;

namespace Marketplace.ViewModels
{
    public class AdminPanelViewModel
    {
        public string Username { get; set; }
        public string UserId { get; set; }
        public bool UserNotFound { get; set; }
        public bool UserAccountUpdated { get; set; }
        public string SearchTerm { get; set; }
        public List<AdminUserListItemViewModel> SearchResults { get; set; } = new List<AdminUserListItemViewModel>();
    }
}
