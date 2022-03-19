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

        [Display(Name ="Category")]
        public string CategoryName { get; set; }

        [Display(Name ="Date Created")]
        public string DateCreatedOn { get; set; }

        public byte[]? ImageInBytes { get; set; }

        public IList<byte[]>? AdditionalImagesInBytes { get; set; }

        //the profile picture of the user who owns the ad
        public byte[]? ProfilePicture { get; set; }
        //the username of who owns the ad
        public string UserName { get; set; }

        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
