using Marketplace.Hubs;
using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    o.MaximumReceiveMessageSize = 32 * 1024;
});

// Read the .env file in the project directory, automatically adds all those key value pairs as environment variables that can be accessed in runtime (you should create that file and have that DotNetEnv)
DotNetEnv.Env.Load();

string? connection_string = Environment.GetEnvironmentVariable("CONNECTION_STRING");
if (connection_string == null)
{
    throw new Exception("You haven't configured the connection string, check Program.cs and the .env file");
}

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection_string));

builder.Services.AddHttpClient<IAiService, AiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60); // Generous timeout for vision model inference
});

// Add Identity support + tables
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Persist Data Protection keys so auth cookies survive an app restart. Without
// this the keys are regenerated on each run and previously issued cookies fail
// validation — SignalR then sees an unauthenticated user and the chat hub
// rejects the connection ("connection lost" on every send).
//
// DEV on Linux (CachyOS): this app defaults to HTTP-only (http://localhost:5256) to avoid
// `dotnet dev-certs https --trust` pain on Arch (certutil/NSS). If you re-enable HTTPS
// in launchSettings.json (https://localhost:7256) run `dotnet dev-certs https --trust`.
//
// PRODUCTION: HTTPS is terminated at the reverse proxy (nginx/caddy). Configure the proxy
// to forward X-Forwarded-Proto/For and set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true or
// enable ForwardedHeaders below. Kestrel then binds http://*:80 behind the proxy.
// Persisted keys to Postgres keep cookies valid across restarts/scale-out.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// Forwarded headers for production reverse proxy (nginx/caddy) — harmless in dev.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Early help — no DB required
var earlyNormalized = args.Select(a => a.Trim().TrimStart('-').ToLowerInvariant()).ToList();
if (earlyNormalized.Any(a => a is "help" or "h" or "?"))
{
    Console.WriteLine("""
        Marketplace — seeding help
          dotnet run -- setup                    Migrate DB + seed roles, users (seller/premium/admin), categories
          dotnet run -- seed:demo               Create 25 demo ads (requires setup first)
          dotnet run -- seed:demo --count 50    Create 50 demo ads (1..200)
          dotnet run -- --seed-demo=25          Backward compat alias for seed:demo
        Order for first-time dev: setup → seed:demo → dotnet run
        """);
    return;
}

// Forwarded headers must run early so scheme/host are correct for SignalR/redirects.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    // DEV: only redirect non-hub requests to HTTPS if the request was already HTTPS.
    // On http://localhost:5256 this is a no-op, so no dev-cert is required and
    // SignalR negotiate stays on the same scheme (ws:// not wss://).
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/hubs") && ctx.Request.IsHttps,
        branch => branch.UseHttpsRedirection());
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/hubs/chat");

// Apply pending EF Core migrations automatically (including DataProtectionKeys).
// Without this a fresh clone without `dotnet ef database update` would run with
// missing tables and DataProtection would regenerate keys on every restart,
// invalidating auth cookies and breaking SignalR auth.
try
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database migration failed — check CONNECTION_STRING and that Postgres is reachable");
    throw;
}

// ---- Seeding CLI ----
// Simple, discoverable commands for dev setup. Order matters: setup first, then demo.
//   dotnet run -- setup              → migrate + seed roles/users/categories (idempotent, first-time devs)
//   dotnet run -- seed:demo          → 25 demo ads (or --seed-demo / --count 50)
//   dotnet run -- seed:demo --count 50
//   dotnet run -- help               → this help
// Running without args just starts the app (migrate only, no seeding) — see README.
var normalized = args.Select(a => a.Trim().TrimStart('-').ToLowerInvariant()).ToList();
bool isHelp = normalized.Any(a => a is "help" or "h" or "?");
bool isSetup = normalized.Any(a => a is "setup" or "seed:core" or "seed-core");
bool isDemo = normalized.Any(a => a is "seed:demo" or "seed-demo" or "seeddemo" or "demo");

if (isHelp)
{
    Console.WriteLine("""
        Marketplace — seeding help
          dotnet run -- setup                    Migrate DB + seed roles, users (seller/premium/admin), categories
          dotnet run -- seed:demo               Create 25 demo ads (requires setup first)
          dotnet run -- seed:demo --count 50    Create 50 demo ads (1..200)
          dotnet run -- --seed-demo=25          Backward compat alias for seed:demo
        Order for first-time dev: setup → seed:demo → dotnet run
        """);
    return;
}

if (isSetup)
{
    var seeder = new DataSeeder(app.Services);
    await seeder.SeedRoles();
    await seeder.SeedUsers();
    await seeder.SeedCategories();
    Console.WriteLine("Setup complete — roles, users (seller/premium/admin), categories seeded. Next: dotnet run -- seed:demo");
    return;
}

if (isDemo || args.Any(a => a.StartsWith("--seed-demo", StringComparison.OrdinalIgnoreCase)))
{
    int count = 25;
    // Support: seed:demo --count 50, --seed-demo=25, --count=50
    var countArg = args.FirstOrDefault(a => a.StartsWith("--count", StringComparison.OrdinalIgnoreCase));
    if (countArg != null)
    {
        var val = countArg.Contains('=') ? countArg.Split('=', 2)[1] : args.SkipWhile(x => x != countArg).Skip(1).FirstOrDefault();
        if (int.TryParse(val, out var parsed)) count = Math.Clamp(parsed, 1, 200);
    }
    var demoEq = args.FirstOrDefault(a => a.StartsWith("--seed-demo=", StringComparison.OrdinalIgnoreCase) || a.StartsWith("seed:demo=", StringComparison.OrdinalIgnoreCase));
    if (demoEq != null && demoEq.Contains('=') && int.TryParse(demoEq.Split('=', 2)[1], out var parsed2)) count = Math.Clamp(parsed2, 1, 200);

    var demoSeeder = new DemoDataSeeder(app.Services);
    int created = await demoSeeder.SeedAsync(count);
    Console.WriteLine($"Demo seeding finished: {created} advertisements created.");
    return;
}

app.Run();
