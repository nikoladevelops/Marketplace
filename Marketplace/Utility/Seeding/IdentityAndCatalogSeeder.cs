using Marketplace.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Marketplace.Utility.Seeding
{
    // Seeds the basic data your app needs to run.
    // Creates roles, default users and categories. Safe to run many times.
    public class IdentityAndCatalogSeeder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IdentityAndCatalogSeeder> _logger;

        // Creates the seeder with needed services.
        public IdentityAndCatalogSeeder(IServiceScopeFactory scopeFactory, ILogger<IdentityAndCatalogSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // Runs all seeding steps in order: roles, users, then categories.
        public async Task SeedAsync(CancellationToken ct = default)
        {
            await SeedRolesAsync(ct);

            await SeedUsersAsync(ct);

            await SeedCategoriesAsync(ct);
        }

        // Makes sure Seller, Premium and Admin roles exist.
        public async Task SeedRolesAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in new[] { Helper.SellerRole, Helper.PremiumRole, Helper.AdminRole })
            {
                if (await roleManager.RoleExistsAsync(role))
                {
                    _logger.LogDebug("Role {Role} already exists, skipping.", role);

                    continue;
                }

                var result = await roleManager.CreateAsync(new IdentityRole(role));

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to create role {Role}: {Errors}", role,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
                else
                {
                    _logger.LogInformation("Created role {Role}.", role);
                }
            }
        }

        // Creates the three default users: seller, premium and admin.
        public async Task SeedUsersAsync(CancellationToken ct = default)
        {
            await SeedSingleUserAsync("seller", "seller@gmail.com", "aaaaaaA!1", Helper.SellerRole, ct);

            await SeedSingleUserAsync("premium", "premium@gmail.com", "aaaaaaA!1", Helper.PremiumRole, ct);

            await SeedSingleUserAsync("admin", "admin@gmail.com", "aaaaaaA!1", Helper.AdminRole, ct);
        }

        // Adds the default categories if none exist yet.
        public async Task SeedCategoriesAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (await context.Categories.AnyAsync(ct))
            {
                _logger.LogDebug("Categories already exist, skipping seeding.");

                return;
            }

            List<CategoryModel> categoriesToSeed =
            [
                new() { Name = "Furniture" },
                new() { Name = "Home Appliances" },
                new() { Name = "Fashion & Accessories" },
                new() { Name = "Smartphones" },
                new() { Name = "Computers & Laptops" },
                new() { Name = "Audio & Headphones" },
                new() { Name = "TV & Home Entertainment" },
                new() { Name = "Cameras & Photography" },
                new() { Name = "Sports & Outdoors" },
            ];

            foreach (var category in categoriesToSeed)
            {
                if (!await context.Categories.AnyAsync(a => a.Name == category.Name, ct))
                {
                    context.Categories.Add(category);
                }
            }

            await context.SaveChangesAsync(ct);

            _logger.LogInformation("Seeded {Count} categories.", categoriesToSeed.Count);
        }

        // Helper that creates one user and assigns the role.
        // Skips if email or username already exists.
        private async Task<bool> SeedSingleUserAsync(string username, string email, string password, string role, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (await userManager.FindByEmailAsync(email) != null)
            {
                _logger.LogDebug("User with email {Email} already exists, skipping.", email);

                return false;
            }

            if (await userManager.FindByNameAsync(username) != null)
            {
                _logger.LogDebug("User with name {Username} already exists, skipping.", username);

                return false;
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Failed to create user {Username}: {Errors}", username,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));

                return false;
            }

            // Re-fetch to get the tracked entity with Id populated
            var created = await userManager.FindByNameAsync(username);

            if (created != null && !await userManager.IsInRoleAsync(created, role))
            {
                var roleResult = await userManager.AddToRoleAsync(created, role);

                if (!roleResult.Succeeded)
                {
                    _logger.LogWarning("Failed to add role {Role} to {Username}: {Errors}", role, username,
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }

            _logger.LogInformation("Created user {Username} with role {Role}.", username, role);

            return true;
        }
    }
}
