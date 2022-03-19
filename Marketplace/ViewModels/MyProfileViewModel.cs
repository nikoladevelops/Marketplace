using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class MyProfileViewModel
    {
        public IFormFile? ProfilePicture { get; set; }
        public byte[]? ProfilePictureInBytes { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        public bool PhoneNumberAgreement { get; set; }

        [DataType(DataType.PhoneNumber)]
        public string? PhoneNumber { get; set; }
    }
}
