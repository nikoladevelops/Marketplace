using Marketplace.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;

namespace Marketplace.Utility
{
    /// <summary>
    /// Seeds data
    /// </summary>
    public class DataSeeder
    {
        private readonly IServiceProvider _serviceProvider;
        public DataSeeder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Seeds roles into the database. If they already exist, it does nothing.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public async Task SeedRoles()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // If even the first role exists, we assume that all roles exist and abort
                if (await roleManager.RoleExistsAsync(Helper.SellerRole))
                {
                    return;
                }

                await roleManager.CreateAsync(new IdentityRole(Helper.SellerRole));
                await roleManager.CreateAsync(new IdentityRole(Helper.PremiumRole));
                await roleManager.CreateAsync(new IdentityRole(Helper.AdminRole));
            }
        }

        /// <summary>
        /// Seeds users into the database. If they already exist, it does nothing
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public async Task SeedUsers()
        {
            await SeedSingleUser("seller", "seller@gmail.com", "aaaaaaA!1", Helper.SellerRole);
            await SeedSingleUser("premium", "premium@gmail.com", "aaaaaaA!1", Helper.PremiumRole);
            await SeedSingleUser("admin", "admin@gmail.com", "aaaaaaA!1", Helper.AdminRole);
        }

        /// <summary>
        /// Seeds Categories into the database, if there are none
        /// </summary>
        public async Task SeedCategories()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var hasCategoriesAlready = await context.Categories.AnyAsync();

                if (hasCategoriesAlready)
                {
                    return;
                }

                List<CategoryModel> categoriesToSeed = new()
                {
                     new() { Name = "Furniture" },
                     new() { Name = "Home Appliances" },
                     new() { Name = "Fashion & Accessories" },
                     new() { Name = "Smartphones" },
                     new() { Name = "Computers & Laptops" },
                     new() { Name = "Audio & Headphones" },
                     new() { Name = "TV & Home Entertainment" },
                     new() { Name = "Cameras & Photography" },
                     new() { Name = "Sports & Outdoors" },
                };

                foreach (var category in categoriesToSeed)
                {
                    if (!context.Categories.Any(a => a.Name == category.Name))
                    {
                        context.Categories.Add(category);
                    }
                }

                await context.SaveChangesAsync();
            }
        }


        /// <summary>
        /// Seeds a single user into the database.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="role"></param>
        /// <param name="serviceProvider"></param>
        /// <returns>Whether the user was seeded successfully</returns>
        private async Task<bool> SeedSingleUser(string username, string email, string password, string role)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                // If user with this email already exists, abort
                if (await userManager.FindByEmailAsync(email) != null)
                {
                    return false;
                }

                // If user with this username already exists, abort
                if (await userManager.FindByNameAsync(username) != null)
                {
                    return false;
                }

                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = email
                };

                var createUserResult = await userManager.CreateAsync(user, password);

                if (createUserResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }

            return true;
        }
    }
}