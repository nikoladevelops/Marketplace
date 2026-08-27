using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? ProfilePicturePath { get; set; }
        public string? Description { get; set; }

        /// <summary>Opt-in visibility: default hidden (false) to avoid spam. Owner can toggle.</summary>
        public bool ShowEmail { get; set; } = false;
        public bool ShowPhone { get; set; } = false;
    }
}