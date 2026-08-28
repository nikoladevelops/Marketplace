namespace Marketplace.ViewModels
{
    // View model for the public profile page.
    // Shows user info and their ads, with privacy handling for email and phone.
    public class ProfileViewModel
    {
        public string Username { get; set; } = "";

        public string? ProfilePicturePath { get; set; }

        public string? Description { get; set; }

        // Raw values for the owner when editing
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public bool ShowEmail { get; set; }

        public bool ShowPhone { get; set; }

        // What the viewer actually gets to see (may be hidden or censored)
        public string? DisplayEmail { get; set; }

        public string? DisplayPhone { get; set; }

        public bool CanViewEmail { get; set; }

        public bool CanViewPhone { get; set; }

        public bool IsCensoredEmail { get; set; }

        public bool IsCensoredPhone { get; set; }

        public bool IsAuthenticated { get; set; }

        // Who is looking and what they can do
        public bool IsOwner { get; set; }

        public bool IsAdmin { get; set; }

        public string CurrentUserId { get; set; } = "";

        // Helper - only the owner can edit their profile
        public bool CanEditProfile => IsOwner;

        // Ads for this user
        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; } = Enumerable.Empty<SimplifiedAdvertisementViewModel>();

        // Paging info
        public int PageNumber { get; set; }

        public int MaxCountPages { get; set; }

        public int TotalCount { get; set; }

        public int PageSize { get; set; } = 12;

        public int MaxAdvertisements { get; set; }

        public bool IsPremium { get; set; }

        // Inline edit form, shown when owner adds ?edit=1
        public MyProfileViewModel? EditForm { get; set; }

        public bool ShowEditForm { get; set; }
    }
}
