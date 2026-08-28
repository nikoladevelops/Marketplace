using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Marketplace.ViewModels
{
    // View model for editing your own profile.
    public class MyProfileViewModel
    {
        // New picture upload
        public IFormFile? ProfilePicture { get; set; }

        // Keep the old one if no new upload
        public string? ExistingProfilePicturePath { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        // Privacy toggles - hidden by default
        public bool ShowPhone { get; set; } = false;

        public bool ShowEmail { get; set; } = false;

        // Old name for ShowPhone, kept so older forms still work
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
