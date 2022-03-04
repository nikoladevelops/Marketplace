using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Marketplace.Models
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions options):base(options)    
        {

        }
        public DbSet<AdvertisementModel> Advertisements { get; set; }
    }
}
