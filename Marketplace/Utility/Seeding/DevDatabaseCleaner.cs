using Marketplace.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Marketplace.Utility.Seeding
{
    // Cleans the dev database completely. Dev only, needs --force.
    // Removes ads, images, chat, blocks, users, roles, categories and uploaded files.
    public class DevDatabaseCleaner
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _env;
        private readonly IWebHostEnvironment _webEnv;
        private readonly ILogger<DevDatabaseCleaner> _logger;

        // Creates the cleaner with environment and logging info.
        public DevDatabaseCleaner(
            IServiceScopeFactory scopeFactory,
            IHostEnvironment env,
            IWebHostEnvironment webEnv,
            ILogger<DevDatabaseCleaner> logger)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _webEnv = webEnv;
            _logger = logger;
        }

        // Wipes the dev database and optionally reseeds it.
        // Returns 0 on success, 2 if blocked.
        public async Task<int> PurgeAsync(bool force, bool reseed, CancellationToken ct = default)
        {
            if (!_env.IsDevelopment())
            {
                Console.Error.WriteLine("Refusing to purge: not in Development (ASPNETCORE_ENVIRONMENT != Development). This command is dev-only.");
                _logger.LogWarning("DevDatabaseCleaner blocked: environment is {Env}, not Development.", _env.EnvironmentName);

                return 2;
            }

            if (!force)
            {
                Console.Error.WriteLine("Refusing to purge: add --force to confirm. Example: dotnet run -- db:reset --force");

                return 2;
            }

            Console.WriteLine("Purging dev database...");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure DB exists and is migrated before deleting
            try
            {
                await db.Database.MigrateAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Migration before purge failed, continuing with purge anyway.");
            }

            // Delete in FK-safe order: children first
            // Raw SQL is fastest and avoids tracking. Use quoted identifiers for Postgres.

            await TryDeleteAsync(db, "\"ChatReports\"", ct);
            await TryDeleteAsync(db, "\"UserBanHistories\"", ct);
            await TryDeleteAsync(db, "\"AdvertisementImages\"", ct);
            await TryDeleteAsync(db, "\"ChatMessages\"", ct);
            await TryDeleteAsync(db, "\"UserBlocks\"", ct);
            await TryDeleteAsync(db, "\"Advertisements\"", ct);

            // Identity tables
            await TryDeleteAsync(db, "\"AspNetUserRoles\"", ct);
            await TryDeleteAsync(db, "\"AspNetUserClaims\"", ct);
            await TryDeleteAsync(db, "\"AspNetUserLogins\"", ct);
            await TryDeleteAsync(db, "\"AspNetUserTokens\"", ct);
            await TryDeleteAsync(db, "\"AspNetUsers\"", ct);
            await TryDeleteAsync(db, "\"AspNetRoleClaims\"", ct);
            await TryDeleteAsync(db, "\"AspNetRoles\"", ct);
            await TryDeleteAsync(db, "\"Categories\"", ct);

            // DataProtectionKeys: keep table but clear rows so cookies are invalidated cleanly
            await TryDeleteAsync(db, "\"DataProtectionKeys\"", ct);

            // Reset Postgres sequences so Ids restart at 1 (best-effort)
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    DO $$ DECLARE r RECORD; BEGIN
                      FOR r IN SELECT sequence_name FROM information_schema.sequences WHERE sequence_schema='public' LOOP
                        EXECUTE 'ALTER SEQUENCE public."' || r.sequence_name || '" RESTART WITH 1';
                      END LOOP;
                    END $$;
                    """, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Sequence reset skipped (non-Postgres or missing privilege).");
            }

            // Delete uploaded files (but keep folder)
            var uploadsToClean = new[]
            {
                Path.Combine(_webEnv.WebRootPath, "uploads", "advertisements"),
                Path.Combine(_webEnv.WebRootPath, "uploads", "profiles"),
            };

            int filesDeleted = 0;

            foreach (var dir in uploadsToClean)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(dir))
                {
                    try
                    {
                        File.Delete(file);
                        filesDeleted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete upload file {File}", file);
                    }
                }
            }

            Console.WriteLine($"Database purged. Files deleted: {filesDeleted}");

            if (reseed)
            {
                Console.WriteLine("Re-seeding essential data (roles/users/categories)...");
                var seeder = scope.ServiceProvider.GetRequiredService<IdentityAndCatalogSeeder>();
                await seeder.SeedAsync(ct);
                Console.WriteLine("Re-seed complete.");
            }
            else
            {
                Console.WriteLine("Tip: run 'dotnet run -- setup' to re-seed roles/users/categories.");
            }

            return 0;
        }

        // Tries to delete all rows from a table. Ignores errors if table is missing.
        private static async Task TryDeleteAsync(ApplicationDbContext db, string quotedTable, CancellationToken ct)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {quotedTable};", ct);
            }
            catch
            {
                // Table may not exist yet, ignore
            }
        }
    }
}
