using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class ApplicationUser:IdentityUser
    {
        public byte[] ProfilePicture { get; set; }

        public string Description { get; set; }
    }
}
