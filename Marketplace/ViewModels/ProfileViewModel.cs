namespace Marketplace.ViewModels
{
    public class ProfileViewModel
    {
        public string Username { get; set; } = "";
        public string? ProfilePicturePath { get; set; }
        public string? Description { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsOwner { get; set; }
        public bool IsAdmin { get; set; }
        public string CurrentUserId { get; set; } = "";
        public bool CanEditProfile => IsOwner;

        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; } = Enumerable.Empty<SimplifiedAdvertisementViewModel>();
        public int PageNumber { get; set; }
        public int MaxCountPages { get; set; }
        public int TotalCount { get; set; }
        public int PageSize { get; set; } = 12;

        // For inline edit when ?edit=1 and IsOwner
        public MyProfileViewModel? EditForm { get; set; }
        public bool ShowEditForm { get; set; }
    }
}
