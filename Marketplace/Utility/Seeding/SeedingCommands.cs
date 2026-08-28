using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Utility.Seeding
{
    // Parses command line args for seeding and user management.
    // Handles setup, demo seeding, user create/role commands and db reset.
    public static class SeedingCommands
    {
        public const string HelpText = """
            Marketplace - seeding help
              dotnet run -- setup                         Migrate DB and seed roles, users (seller/premium/admin), categories (idempotent)
              dotnet run -- seed:demo                     Create 25 demo ads (requires setup first)
              dotnet run -- seed:demo --count 50          Create 50 demo ads (1..200)
              dotnet run -- seed:demo --user admin --count 20  Create ads for specific user
              dotnet run -- --seed-demo=25                Backwards compat alias for seed:demo
              dotnet run -- user:create --username newadmin --email newadmin@example.com [--password aaaaaaA!1] [--role Admin]  Create user (email required, default password aaaaaaA!1, default role Seller, multiple roles comma separated)
              dotnet run -- user:give-role --user admin --role Premium   Give role to user (username or email)
              dotnet run -- user:remove-role --user premium --role Premium  Remove role from user
              dotnet run -- user:list [--search mar] [--take 10]          List users (search substring, take 1..50)
              dotnet run -- db:reset --force              Purge dev DB and uploads (DEV only, requires --force)
              dotnet run -- db:reset --force --reseed     Purge and re-seed essential data
              dotnet run -- help                          Show this help (no DB needed)

            Order for first-time dev: setup -> seed:demo -> dotnet run
            Dev reset is blocked in Production. Use --force to confirm.
            """;

        public record HandleResult(bool Handled, int ExitCode);

        // Quick help check that needs no services. Prints help and returns true if help was requested.
        public static bool TryHandleEarlyHelp(string[] args)
        {
            var normalized = args.Select(a => a.Trim().TrimStart('-').ToLowerInvariant()).ToList();

            if (normalized.Any(a => a is "help" or "h" or "?"))
            {
                Console.WriteLine(HelpText);

                return true;
            }

            return false;
        }

        // Main command dispatcher. Checks args and runs the matching action.
        // Returns Handled true if a command was executed.
        public static async Task<HandleResult> TryHandleAsync(string[] args, WebApplication app, CancellationToken ct = default)
        {
            var normalized = args.Select(a => a.Trim().TrimStart('-').ToLowerInvariant()).ToList();
            bool isHelp = normalized.Any(a => a is "help" or "h" or "?");
            bool isSetup = normalized.Any(a => a is "setup" or "seed:core" or "seed-core");
            bool isDemo = normalized.Any(a => a is "seed:demo" or "seed-demo" or "seeddemo" or "demo");
            bool hasSeedDemoEq = args.Any(a => a.StartsWith("--seed-demo", StringComparison.OrdinalIgnoreCase)
                                            || a.StartsWith("seed:demo=", StringComparison.OrdinalIgnoreCase));
            bool isReset = normalized.Any(a => a is "db:reset" or "db-reset" or "reset:dev" or "reset-dev" or "reset" or "purge" or "clean");
            bool isUserCreate = normalized.Any(a => a is "user:create" or "user-create" or "create:user" or "create-user");
            bool isGiveRole = normalized.Any(a => a is "user:give-role" or "user-give-role" or "give-role" or "role:give");
            bool isRemoveRole = normalized.Any(a => a is "user:remove-role" or "user-remove-role" or "remove-role" or "role:remove");
            bool isUserList = normalized.Any(a => a is "user:list" or "user-list" or "list:users" or "list-users" or "users:list");

            if (hasSeedDemoEq)
            {
                isDemo = true;
            }

            if (isHelp)
            {
                Console.WriteLine(HelpText);

                return new HandleResult(true, 0);
            }

            if (isReset)
            {
                bool force = normalized.Contains("force") || args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase) || a.Equals("-f", StringComparison.OrdinalIgnoreCase));
                bool reseed = normalized.Contains("reseed") || args.Any(a => a.Equals("--reseed", StringComparison.OrdinalIgnoreCase));

                using var scope = app.Services.CreateScope();
                var cleaner = scope.ServiceProvider.GetRequiredService<DevDatabaseCleaner>();
                int code = await cleaner.PurgeAsync(force, reseed, ct);

                return new HandleResult(true, code);
            }

            if (isSetup)
            {
                using var scope = app.Services.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<IdentityAndCatalogSeeder>();
                await seeder.SeedAsync(ct);

                Console.WriteLine("Setup complete - roles, users (seller/premium/admin), categories seeded. Next: dotnet run -- seed:demo");

                return new HandleResult(true, 0);
            }

            if (isUserCreate)
            {
                var username = ParseArgValue(args, "username", "user", "name");
                var email = ParseArgValue(args, "email", "mail");
                var password = ParseArgValue(args, "password", "pass", "pwd");
                var roleRaw = ParseArgValue(args, "role", "roles");
                string[]? roles = null;

                if (roleRaw != null)
                {
                    roles = roleRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
                {
                    Console.Error.WriteLine("Create user failed: --username and --email are required. Example: dotnet run -- user:create --username myadmin --email myadmin@example.com --role Admin");

                    return new HandleResult(true, 1);
                }

                using var scope = app.Services.CreateScope();
                var userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
                bool ok = await userSeeder.CreateUserAsync(username, email, password, roles, ct);

                return new HandleResult(true, ok ? 0 : 1);
            }

            if (isGiveRole)
            {
                var userId = ParseArgValue(args, "user", "username", "name", "email", "for", "target");
                var role = ParseArgValue(args, "role", "roles");

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
                {
                    Console.Error.WriteLine("Give role failed: --user and --role are required. Example: dotnet run -- user:give-role --user admin --role Premium");

                    return new HandleResult(true, 1);
                }

                var singleRole = role.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? role;

                using var scope = app.Services.CreateScope();
                var userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
                bool ok = await userSeeder.GiveRoleAsync(userId, singleRole, ct);

                return new HandleResult(true, ok ? 0 : 1);
            }

            if (isRemoveRole)
            {
                var userId = ParseArgValue(args, "user", "username", "name", "email", "for", "target");
                var role = ParseArgValue(args, "role", "roles");

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
                {
                    Console.Error.WriteLine("Remove role failed: --user and --role are required. Example: dotnet run -- user:remove-role --user premium --role Premium");

                    return new HandleResult(true, 1);
                }

                var singleRole = role.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? role;

                using var scope = app.Services.CreateScope();
                var userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
                bool ok = await userSeeder.RemoveRoleAsync(userId, singleRole, ct);

                return new HandleResult(true, ok ? 0 : 1);
            }

            if (isUserList)
            {
                var search = ParseArgValue(args, "search", "query", "term", "for");
                var takeRaw = ParseArgValue(args, "take", "count", "limit");
                int take = 20;

                if (takeRaw != null && int.TryParse(takeRaw, out var parsedTake))
                {
                    take = Math.Clamp(parsedTake, 1, 50);
                }

                using var scope = app.Services.CreateScope();
                var userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
                var users = await userSeeder.ListUsersAsync(search, take, ct);

                if (users.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(search))
                    {
                        Console.WriteLine("No users found.");
                    }
                    else
                    {
                        Console.WriteLine($"No users found for '{search}'.");
                    }
                }
                else
                {
                    Console.WriteLine($"Found {users.Count} user(s):");
                    Console.WriteLine($"{"Username",-20} {"Email",-30} Roles");
                    Console.WriteLine(new string('-', 70));

                    foreach (var u in users)
                    {
                        Console.WriteLine($"{u.UserName,-20} {u.Email,-30} {string.Join(", ", u.Roles)}");
                    }
                }

                return new HandleResult(true, 0);
            }

            if (isDemo)
            {
                int count = 25;
                var countRaw = ParseArgValue(args, "count", "take", "limit", "num", "number", "c");

                if (countRaw != null && int.TryParse(countRaw, out var parsedCount))
                {
                    count = Math.Clamp(parsedCount, 1, 200);
                }

                var demoEqRaw = ParseArgValue(args, "seed:demo", "seed-demo", "seeddemo", "demo");

                if (demoEqRaw != null && int.TryParse(demoEqRaw, out var parsedDemo))
                {
                    count = Math.Clamp(parsedDemo, 1, 200);
                }
                else
                {
                    var demoEq = args.FirstOrDefault(a => a.StartsWith("--seed-demo=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("seed:demo=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--seed-demo:", StringComparison.OrdinalIgnoreCase) || a.StartsWith("seed:demo:", StringComparison.OrdinalIgnoreCase));

                    if (demoEq != null && demoEq.Contains('=') && int.TryParse(demoEq.Split(new[] { '=', ':' }, 3).Last(), out var parsed2))
                    {
                        count = Math.Clamp(parsed2, 1, 200);
                    }
                    else if (demoEq != null && demoEq.Contains(':') && int.TryParse(demoEq.Split(':').Last(), out var parsed3))
                    {
                        count = Math.Clamp(parsed3, 1, 200);
                    }
                }

                // fallback: bare numeric after seed:demo without flag, e.g. "seed:demo 5"
                if (countRaw == null)
                {
                    for (int k = 0; k < args.Length; k++)
                    {
                        var a = args[k].Trim().TrimStart('-').ToLowerInvariant();

                        if (a is "seed:demo" or "seed-demo" or "demo")
                        {
                            if (k + 1 < args.Length && int.TryParse(args[k + 1].Trim(), out var bare) && !args[k + 1].TrimStart().StartsWith("-") && !IsKnownKey(args[k + 1]))
                            {
                                count = Math.Clamp(bare, 1, 200);

                                break;
                            }
                        }
                    }
                }

                var targetUser = ParseArgValue(args, "user", "username", "for", "target", "as");

                using var scope = app.Services.CreateScope();
                var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoContentSeeder>();
                int created = await demoSeeder.SeedAsync(count, targetUser, ct);

                Console.WriteLine($"Demo seeding finished: {created} advertisements created.");

                return new HandleResult(true, 0);
            }

            return new HandleResult(false, 0);
        }

        // Checks if an arg looks like a known key name.
        private static bool IsKnownKey(string arg)
        {
            var t = arg.Trim().TrimStart('-').ToLowerInvariant().Split('=')[0].Split(':')[0];

            return t is "count" or "take" or "limit" or "user" or "username" or "for" or "target" or "as" or "role" or "roles" or "email" or "mail" or "password" or "pass" or "pwd" or "search" or "query" or "term" or "name";
        }

        // Reads a value for one of the given names from args. Supports --key value and --key=value forms.
        private static string? ParseArgValue(string[] args, params string[] names)
        {
            var lowerNames = names.Select(n => n.ToLowerInvariant()).ToHashSet();

            for (int i = 0; i < args.Length; i++)
            {
                var raw = args[i].Trim();

                if (IsCommandToken(raw))
                {
                    continue;
                }

                bool isDashed = raw.StartsWith("-");
                var stripped = raw.TrimStart('-');
                var eqIdx = stripped.IndexOf('=');
                string key;
                string? value = null;

                if (eqIdx >= 0)
                {
                    key = stripped.Substring(0, eqIdx).ToLowerInvariant().Split(':')[0];
                    value = stripped.Substring(eqIdx + 1).Trim();

                    // handle --seed-demo:25 style already captured via Split above, but keep
                    if (key.Contains(':'))
                    {
                        key = key.Split(':')[0];
                    }
                }
                else
                {
                    // split on ':' for seed:demo:5 style without dash
                    var colonIdx = stripped.IndexOf(':');

                    if (colonIdx >= 0)
                    {
                        var before = stripped.Substring(0, colonIdx).ToLowerInvariant();
                        var after = stripped.Substring(colonIdx + 1);

                        // if names contains before, this is e.g. seed:demo with value after colon (legacy)
                        if (lowerNames.Contains(before) && !string.IsNullOrWhiteSpace(after))
                        {
                            return after.Trim();
                        }

                        key = stripped.ToLowerInvariant().Split(':')[0];

                        // not a direct match, treat whole as key for later
                        key = stripped.ToLowerInvariant();
                    }
                    else
                    {
                        key = stripped.ToLowerInvariant();
                    }

                    // look ahead for value token
                    if (i + 1 < args.Length)
                    {
                        var next = args[i + 1].Trim();

                        // next is value if it does not look like a flag or command
                        if (!next.StartsWith("-") && !IsCommandToken(next))
                        {
                            value = next;
                        }
                    }
                }

                // normalize key: take before ':' for user:create style
                var normKey = key.Split(':')[0].Split('=')[0];

                if (lowerNames.Contains(normKey) || lowerNames.Contains(key))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim().Trim('"').Trim('\'');
                    }

                    if (eqIdx >= 0)
                    {
                        return "";
                    }

                    // check if next token is the value but we already captured above
                    // if value is still null, try next token again without command check for user target
                    if (value == null && i + 1 < args.Length)
                    {
                        var nxt = args[i + 1].Trim();

                        if (!string.IsNullOrWhiteSpace(nxt) && !nxt.TrimStart('-').Contains(':'))
                        {
                            // avoid stealing next command
                            var nxtNorm = nxt.TrimStart('-').ToLowerInvariant().Split(':')[0].Split('=')[0];

                            if (!lowerNames.Contains(nxtNorm) && !IsCommandToken(nxt))
                            {
                                return nxt.Trim().Trim('"').Trim('\'');
                            }
                        }
                    }
                }

                // also handle bare key without dash: e.g. "count 5" where count is key
                // above already handles via stripped==key, but need to ensure isDashed check not blocking
                // we already allowed non-dashed: isDashed check removed, so bare keys now work
            }

            return null;
        }

        // Returns true if the token is a known top-level command.
        private static bool IsCommandToken(string arg)
        {
            var t = arg.Trim().TrimStart('-').ToLowerInvariant();

            return t is "setup" or "seed:core" or "seed-core" or "seed:demo" or "seed-demo" or "seeddemo" or "demo" or "help" or "h" or "?" or "db:reset" or "db-reset" or "reset:dev" or "reset-dev" or "reset" or "purge" or "clean" or "user:create" or "user-create" or "create:user" or "create-user" or "user:give-role" or "user-give-role" or "give-role" or "role:give" or "user:remove-role" or "user-remove-role" or "remove-role" or "role:remove" or "user:list" or "user-list" or "list:users" or "list-users" or "users:list";
        }
    }
}
