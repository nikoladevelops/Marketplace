using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Marketplace.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options):base(options)    
        {

        }
        public DbSet<AdvertisementModel> Advertisements { get; set; }
        public DbSet<CategoryModel> Categories { get; set; }
        public DbSet<AdvertisementImageModel> AdvertisementImages { get; set; }

    }
}
