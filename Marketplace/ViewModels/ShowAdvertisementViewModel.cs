using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class ShowAdvertisementViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Price { get; set; }
        public string Location { get; set; }
        public string UserId { get; set; }

        [Display(Name = "Category")]
        public string CategoryName { get; set; }

        [Display(Name = "Date Created")]
        public string DateCreatedOn { get; set; }

        public string ImagePath { get; set; }
        public IList<string>? AdditionalImagePaths { get; set; }

        public string? ProfilePicturePath { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}