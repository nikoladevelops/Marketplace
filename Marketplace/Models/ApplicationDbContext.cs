using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Models
{
    // This is our main database context.
    // It ties all our models to the database and configures indexes and relationships.
    // If you need to add a new table, add a DbSet here and configure it in OnModelCreating.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Core tables for the marketplace

        public DbSet<AdvertisementModel> Advertisements { get; set; }

        public DbSet<CategoryModel> Categories { get; set; }

        public DbSet<AdvertisementImageModel> AdvertisementImages { get; set; }

        public DbSet<ChatMessage> ChatMessages { get; set; }

        public DbSet<ChatReport> ChatReports { get; set; }

        public DbSet<UserBlock> UserBlocks { get; set; }

        public DbSet<UserBanHistory> UserBanHistories { get; set; }

        // This keeps Data Protection keys in the database so logins stay valid after a restart
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        // Configure database details like indexes and relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enable trigram search for Postgres (helps with fuzzy text search)
            modelBuilder.HasPostgresExtension("pg_trgm");

            // Price needs exact decimal type
            modelBuilder.Entity<AdvertisementModel>()
                .Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            // Full text search indexes for ads (title, description, location)
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

            // Helpful indexes for filtering and sorting ads
            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.Price);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.DateCreatedOn);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => x.CategoryId);

            modelBuilder.Entity<AdvertisementModel>()
                .HasIndex(x => new { x.CategoryId, x.Price });

            // A user can block another user only once
            modelBuilder.Entity<UserBlock>()
                .HasIndex(b => new { b.BlockerId, b.BlockedId })
                .IsUnique();

            // Indexes for ban history lookups
            modelBuilder.Entity<UserBanHistory>()
                .HasIndex(h => new { h.UserId, h.PerformedAtUtc });

            modelBuilder.Entity<UserBanHistory>()
                .HasIndex(h => h.AdminUserId);

            // Quick lookup by account status
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.Status);

            // Ban history relationships - keep history even if user is removed from other places
            modelBuilder.Entity<UserBanHistory>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserBanHistory>()
                .HasOne(h => h.AdminUser)
                .WithMany()
                .HasForeignKey(h => h.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Who banned this user - set to null if admin is deleted
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.BannedByUser)
                .WithMany()
                .HasForeignKey(u => u.BannedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Chat indexes for inbox and thread queries
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.ReceiverId, m.IsReadByReceiver });

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.SenderId);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.AdvertisementId);

            // Reports: one per reporter per thread, fast lookup for admin
            modelBuilder.Entity<ChatReport>()
                .HasIndex(r => new { r.ReporterId, r.ThreadKey })
                .IsUnique();

            modelBuilder.Entity<ChatReport>()
                .HasIndex(r => new { r.ReportedUserId, r.Status });

            modelBuilder.Entity<ChatReport>()
                .HasIndex(r => r.Status);

            modelBuilder.Entity<ChatReport>()
                .HasIndex(r => r.CreatedAtUtc);

            // Keep reports even if users or ads are deleted, but restrict deletes
            modelBuilder.Entity<ChatReport>()
                .HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatReport>()
                .HasOne(r => r.ReportedUser)
                .WithMany()
                .HasForeignKey(r => r.ReportedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatReport>()
                .HasOne(r => r.Advertisement)
                .WithMany()
                .HasForeignKey(r => r.AdvertisementId)
                .OnDelete(DeleteBehavior.Restrict);

            // If the reviewing admin is deleted, keep the report but clear the reviewer.

            modelBuilder.Entity<ChatReport>()
                .HasOne(r => r.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);

            // Helpful for blocked badge counts
            modelBuilder.Entity<UserBlock>()
                .HasIndex(b => b.BlockedId);

            modelBuilder.Entity<UserBlock>()
                .HasIndex(b => b.BlockerId);
        }
    }
}
