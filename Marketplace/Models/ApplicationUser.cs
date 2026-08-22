using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? ProfilePicturePath { get; set; }
        public string? Description { get; set; }
    }
}