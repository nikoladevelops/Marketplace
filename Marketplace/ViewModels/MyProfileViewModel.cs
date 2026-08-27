using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    public class MyProfileViewModel
    {
        public IFormFile? ProfilePicture { get; set; }
        public string? ExistingProfilePicturePath { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        // New opt-in flags: default hidden (false). Keep PhoneNumberAgreement as obsolete shim.
        public bool ShowPhone { get; set; } = false;
        public bool ShowEmail { get; set; } = false;

        [Obsolete("Use ShowPhone")]
        public bool PhoneNumberAgreement
        {
            get => ShowPhone;
            set => ShowPhone = value;
        }

        [DataType(DataType.PhoneNumber)]
        public string? PhoneNumber { get; set; }
    }
}
