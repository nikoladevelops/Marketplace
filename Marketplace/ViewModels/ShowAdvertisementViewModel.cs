using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class ShowAdvertisementViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Price { get; set; } = "";
        public string Location { get; set; } = "";
        public string UserId { get; set; } = "";

        [Display(Name = "Category")]
        public string CategoryName { get; set; } = "";

        [Display(Name = "Date Created")]
        public DateTime DateCreatedOn { get; set; }

        public string ImagePath { get; set; } = "";
        public IList<string>? AdditionalImagePaths { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? ProfilePicturePath { get; set; }
        public string UserName { get; set; } = "";
        // Raw
        public string Email { get; set; } = "";
        public string? PhoneNumber { get; set; }

        // Viewer-aware display (censored / hidden)
        public string? DisplayEmail { get; set; }
        public string? DisplayPhone { get; set; }
        public bool CanViewEmail { get; set; }
        public bool CanViewPhone { get; set; }
        public bool IsCensoredEmail { get; set; }
        public bool IsCensoredPhone { get; set; }
        public bool ViewerIsAuthenticated { get; set; }
        public bool IsOwner { get; set; }
        public bool IsAdmin { get; set; }
    }
}
