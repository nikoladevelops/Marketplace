using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Utility.Seeding
{
    /// <summary>
    /// CLI dispatcher for seeding commands. Keeps Program.cs small and makes commands testable.
    /// </summary>
    public static class SeedingCommands
    {
        public const string HelpText = """
            Marketplace - seeding help
              dotnet run -- setup                         Migrate DB and seed roles, users (seller/premium/admin), categories (idempotent)
              dotnet run -- seed:demo                     Create 25 demo ads (requires setup first)
              dotnet run -- seed:demo --count 50          Create 50 demo ads (1..200)
              dotnet run -- --seed-demo=25                Backwards compat alias for seed:demo
              dotnet run -- db:reset --force              Purge dev DB and uploads (DEV only, requires --force)
              dotnet run -- db:reset --force --reseed     Purge and re-seed essential data
              dotnet run -- help                          Show this help (no DB needed)

            Order for first-time dev: setup -> seed:demo -> dotnet run
            Dev reset is blocked in Production. Use --force to confirm.
            """;

        public record HandleResult(bool Handled, int ExitCode);

        /// <summary>
        /// Early help check that needs no DB/services. Returns true if help was printed and app should exit.
        /// </summary>
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

        /// <summary>
        /// Handles seeding commands after Migrate. Returns (Handled, ExitCode).
        /// If Handled=false, caller should continue to app.Run().
        /// </summary>
        public static async Task<HandleResult> TryHandleAsync(string[] args, WebApplication app, CancellationToken ct = default)
        {
            var normalized = args.Select(a => a.Trim().TrimStart('-').ToLowerInvariant()).ToList();
            bool isHelp = normalized.Any(a => a is "help" or "h" or "?");
            bool isSetup = normalized.Any(a => a is "setup" or "seed:core" or "seed-core");
            bool isDemo = normalized.Any(a => a is "seed:demo" or "seed-demo" or "seeddemo" or "demo");
            bool hasSeedDemoEq = args.Any(a => a.StartsWith("--seed-demo", StringComparison.OrdinalIgnoreCase)
                                            || a.StartsWith("seed:demo=", StringComparison.OrdinalIgnoreCase));
            bool isReset = normalized.Any(a => a is "db:reset" or "db-reset" or "reset:dev" or "reset-dev" or "reset" or "purge" or "clean");

            if (hasSeedDemoEq) isDemo = true;

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

            if (isDemo)
            {
                int count = 25;
                var countArg = args.FirstOrDefault(a => a.StartsWith("--count", StringComparison.OrdinalIgnoreCase));
                if (countArg != null)
                {
                    var val = countArg.Contains('=') ? countArg.Split('=', 2)[1] : args.SkipWhile(x => x != countArg).Skip(1).FirstOrDefault();
                    if (int.TryParse(val, out var parsed)) count = Math.Clamp(parsed, 1, 200);
                }
                var demoEq = args.FirstOrDefault(a => a.StartsWith("--seed-demo=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("seed:demo=", StringComparison.OrdinalIgnoreCase));
                if (demoEq != null && demoEq.Contains('=') && int.TryParse(demoEq.Split('=', 2)[1], out var parsed2)) count = Math.Clamp(parsed2, 1, 200);

                using var scope = app.Services.CreateScope();
                var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoContentSeeder>();
                int created = await demoSeeder.SeedAsync(count, ct);
                Console.WriteLine($"Demo seeding finished: {created} advertisements created.");
                return new HandleResult(true, 0);
            }

            return new HandleResult(false, 0);
        }
    }
}
