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

            modelBuilder.HasPostgresExtension("pg_trgm");

            modelBuilder.Entity<AdvertisementModel>()
                .Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.Title)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.Description)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.Location)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.Price);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.DateCreatedOn);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.CategoryId);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => new { x.CategoryId, x.Price });

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
