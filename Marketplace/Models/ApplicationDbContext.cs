using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Marketplace.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options):base(options)    
        {

        }
        public DbSet<AdvertisementModel> Advertisements { get; set; }
        public DbSet<CategoryModel> Categories { get; set; }
        public DbSet<AdvertisementImageModel> AdvertisementImages { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<UserBlock> UserBlocks { get; set; }

        // Data Protection key ring storage (auth cookie validation across restarts).
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserBlock>()
                .HasIndex(b => new { b.BlockerId, b.BlockedId })
                .IsUnique();

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.ReceiverId, m.IsReadByReceiver });

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.SenderId);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.AdvertisementId);
        }

    }
}
