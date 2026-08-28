using Marketplace.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Mail;

namespace Marketplace.Utility.Seeding
{
    // Handles user management from the command line.
    // Lets you create users, give or remove roles, and list users.
    public class UserSeeder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<UserSeeder> _logger;
        private static readonly string[] ValidRoles = [Helper.SellerRole, Helper.PremiumRole, Helper.AdminRole];
        private const string DefaultPassword = "aaaaaaA!1";

        // Creates the seeder with a scope factory and logger.
        public UserSeeder(IServiceScopeFactory scopeFactory, ILogger<UserSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // Creates a new user with the given username, email, password and roles.
        // Validates input, checks duplicates, and assigns roles. Returns true on success.
        public async Task<bool> CreateUserAsync(string username, string email, string? password, string[]? roles, CancellationToken ct = default)
        {
            username = username.Trim();
            email = email.Trim();
            password = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password.Trim();

            if (string.IsNullOrWhiteSpace(password))
            {
                password = DefaultPassword;
            }

            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                var msg = $"Create user failed: username '{username}' is required and must be at least 3 characters.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                var msg = $"Create user failed: email is required for username '{username}'.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            try
            {
                var addr = new MailAddress(email);

                if (addr.Address != email)
                {
                    throw new FormatException();
                }
            }
            catch
            {
                var msg = $"Create user failed: email '{email}' is not valid.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            string[] targetRoles = ValidRoles.Where(r => false).ToArray();

            if (roles == null || roles.Length == 0)
            {
                targetRoles = [Helper.SellerRole];
            }
            else
            {
                var parsed = new List<string>();

                foreach (var raw in roles)
                {
                    var r = raw.Trim();

                    if (string.IsNullOrWhiteSpace(r))
                    {
                        continue;
                    }

                    var matched = ValidRoles.FirstOrDefault(v => v.Equals(r, StringComparison.OrdinalIgnoreCase));

                    if (matched == null)
                    {
                        var msg = $"Create user failed: invalid role '{r}'. Valid roles: {string.Join(", ", ValidRoles)}";
                        Console.Error.WriteLine(msg);
                        _logger.LogWarning(msg);

                        return false;
                    }

                    if (!parsed.Contains(matched, StringComparer.OrdinalIgnoreCase))
                    {
                        parsed.Add(matched);
                    }
                }

                if (parsed.Count == 0)
                {
                    parsed.Add(Helper.SellerRole);
                }

                targetRoles = parsed.ToArray();
            }

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in targetRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var msg = $"Create user failed: role '{role}' does not exist. Run setup first.";
                    Console.Error.WriteLine(msg);
                    _logger.LogWarning(msg);

                    return false;
                }
            }

            if (await userManager.FindByNameAsync(username) != null)
            {
                var msg = $"Create user failed: username '{username}' already exists.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            if (await userManager.FindByEmailAsync(email) != null)
            {
                var msg = $"Create user failed: email '{email}' already exists.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var msg = $"Create user failed for '{username}': {string.Join(", ", result.Errors.Select(e => e.Description))}";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            var created = await userManager.FindByNameAsync(username);

            if (created == null)
            {
                var msg = $"Create user failed: could not retrieve user '{username}' after creation.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            foreach (var role in targetRoles)
            {
                if (!await userManager.IsInRoleAsync(created, role))
                {
                    var roleResult = await userManager.AddToRoleAsync(created, role);

                    if (!roleResult.Succeeded)
                    {
                        var msg = $"User '{username}' created but failed to add role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}";
                        Console.Error.WriteLine(msg);
                        _logger.LogWarning(msg);
                    }
                }
            }

            bool usedDefaultPassword = password == DefaultPassword;

            Console.WriteLine($"Created user '{username}' ({email}) with roles [{string.Join(", ", targetRoles)}]{(usedDefaultPassword ? " using default password" : "")}.");
            _logger.LogInformation("Created user {Username} with roles {Roles}", username, string.Join(",", targetRoles));

            return true;
        }

        // Gives a role to an existing user found by username or email.
        public async Task<bool> GiveRoleAsync(string identifier, string role, CancellationToken ct = default)
        {
            identifier = identifier.Trim();
            role = role.Trim();

            var matched = ValidRoles.FirstOrDefault(v => v.Equals(role, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                var msg = $"Give role failed: invalid role '{role}'. Valid roles: {string.Join(", ", ValidRoles)}";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            role = matched;

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(role))
            {
                var msg = $"Give role failed: role '{role}' does not exist. Run setup first.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            var user = await userManager.FindByNameAsync(identifier) ?? await userManager.FindByEmailAsync(identifier);

            if (user == null)
            {
                var msg = $"Give role failed: user '{identifier}' not found.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            if (await userManager.IsInRoleAsync(user, role))
            {
                Console.WriteLine($"User '{user.UserName}' already has role '{role}'.");

                return true;
            }

            var result = await userManager.AddToRoleAsync(user, role);

            if (!result.Succeeded)
            {
                var msg = $"Give role failed for '{user.UserName}': {string.Join(", ", result.Errors.Select(e => e.Description))}";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            Console.WriteLine($"Added role '{role}' to user '{user.UserName}'.");
            _logger.LogInformation("Added role {Role} to {User}", role, user.UserName);

            return true;
        }

        // Removes a role from a user. Safe if the user already lacks the role.
        public async Task<bool> RemoveRoleAsync(string identifier, string role, CancellationToken ct = default)
        {
            identifier = identifier.Trim();
            role = role.Trim();

            var matched = ValidRoles.FirstOrDefault(v => v.Equals(role, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                var msg = $"Remove role failed: invalid role '{role}'. Valid roles: {string.Join(", ", ValidRoles)}";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            role = matched;

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync(identifier) ?? await userManager.FindByEmailAsync(identifier);

            if (user == null)
            {
                var msg = $"Remove role failed: user '{identifier}' not found.";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                Console.WriteLine($"User '{user.UserName}' does not have role '{role}'.");

                return true;
            }

            var result = await userManager.RemoveFromRoleAsync(user, role);

            if (!result.Succeeded)
            {
                var msg = $"Remove role failed for '{user.UserName}': {string.Join(", ", result.Errors.Select(e => e.Description))}";
                Console.Error.WriteLine(msg);
                _logger.LogWarning(msg);

                return false;
            }

            Console.WriteLine($"Removed role '{role}' from user '{user.UserName}'.");
            _logger.LogInformation("Removed role {Role} from {User}", role, user.UserName);

            return true;
        }

        // Lists users, optionally filtered by a search term.
        // Limits results to between 1 and 50.
        public async Task<IReadOnlyList<(string UserName, string Email, string[] Roles)>> ListUsersAsync(string? searchTerm, int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 50);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IQueryable<ApplicationUser> query = context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var pattern = $"%{searchTerm.Trim()}%";

                query = query.Where(u => Microsoft.EntityFrameworkCore.EF.Functions.ILike(u.UserName ?? "", pattern) || Microsoft.EntityFrameworkCore.EF.Functions.ILike(u.Email ?? "", pattern));
            }

            var users = await query.OrderBy(u => u.UserName).Take(take).ToListAsync(ct);

            var result = new List<(string, string, string[])>();

            foreach (var u in users)
            {
                var roles = await userManager.GetRolesAsync(u);

                result.Add((u.UserName ?? "", u.Email ?? "", roles.ToArray()));
            }

            return result;
        }
    }
}
