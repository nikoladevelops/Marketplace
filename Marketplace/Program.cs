using Marketplace.Hubs;
using Marketplace.Middleware;
using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility.Seeding;
using Marketplace.Utility.Seeding.ImageProviders;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Service setup
// ============================================================

// MVC and SignalR
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    o.MaximumReceiveMessageSize = 32 * 1024;
});

// Load .env file if present, so CONNECTION_STRING can come from there
DotNetEnv.Env.Load();

string? connection_string = Environment.GetEnvironmentVariable("CONNECTION_STRING");

if (connection_string == null)
{
    throw new Exception("You haven't configured the connection string, check Program.cs and the .env file");
}

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection_string));

// Http clients
builder.Services.AddHttpClient<IAiImageService, AiImageService>(client =>
{
    // Generous timeout for vision model inference with up to 4 images
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHttpClient("LoremFlickr", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("Unsplash", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("Picsum", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("DemoContentSeeder", client => client.Timeout = TimeSpan.FromSeconds(30));

// Image providers for demo seeding, switchable via config and env var DEMO_IMAGE_PROVIDERS
builder.Services.Configure<ImageProviderOptions>(builder.Configuration.GetSection("DemoSeeding"));
builder.Services.AddScoped<LoremFlickrProvider>();
builder.Services.AddScoped<UnsplashProvider>();
builder.Services.AddScoped<PicsumProvider>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<LocalFallbackProvider>();
}

// App services
builder.Services.AddScoped<AdvertisementFilterService>();
builder.Services.AddScoped<UserAdministrationService>();
builder.Services.AddScoped<AdvertisementService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ChatService>();

// Seeding services
builder.Services.AddScoped<IdentityAndCatalogSeeder>();
builder.Services.AddScoped<DemoContentSeeder>();
builder.Services.AddScoped<DevDatabaseCleaner>();
builder.Services.AddScoped<UserSeeder>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Data Protection: persist keys so auth cookies survive restarts
// In dev we use HTTP only on localhost:5256 to avoid cert issues.
// In production HTTPS is handled by the reverse proxy, so we forward headers
// and store keys in Postgres.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// Allow large Base64 image previews to post back after validation errors.
// We keep images client side as data URLs and post them via hidden fields.
// A 5MB image becomes ~6.7MB Base64, 4 images would be ~27MB, so we raise limits.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartBodyLengthLimit = 30 * 1024 * 1024;
    o.ValueCountLimit = 4096;
});

// Forwarded headers for proxy (nginx, caddy), harmless in dev
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ============================================================
// Build app and configure pipeline
// ============================================================

var app = builder.Build();

// Early help: no DB required
if (SeedingCommands.TryHandleEarlyHelp(args))
{
    return;
}

// Forwarded headers must run early
app.UseForwardedHeaders();

// Error handling and HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    // In dev, only redirect non-hub requests that are already HTTPS.
    // On http://localhost:5256 this is a no-op, so no dev cert is needed and
    // SignalR stays on the same scheme (ws, not wss).
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/hubs") && ctx.Request.IsHttps,
        branch => branch.UseHttpsRedirection());
}

// Static files and routing
app.UseStaticFiles();

app.UseRouting();

// Auth and custom middleware
app.UseAuthentication();

app.UseMiddleware<BannedUserMiddleware>();

app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/hubs/chat");

// Apply pending EF Core migrations automatically (including DataProtectionKeys).
// Without this a fresh clone without `dotnet ef database update` would run with
// missing tables and DataProtection would regenerate keys on every restart
// and invalidate auth cookies.
try
{
    using var migrateScope = app.Services.CreateScope();
    var db = migrateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database migration failed, check CONNECTION_STRING and that Postgres is reachable");

    throw;
}

// Seeding CLI: setup first, then demo
//   dotnet run -- setup              -> migrate and seed roles/users/categories (idempotent)
//   dotnet run -- seed:demo          -> 25 demo ads (or --count 50, compat --seed-demo=25)
//   dotnet run -- db:reset --force   -> purge dev DB and uploads (DEV only)
//   dotnet run -- help               -> this help (no DB needed)
var seedingResult = await SeedingCommands.TryHandleAsync(args, app);

if (seedingResult.Handled)
{
    if (seedingResult.ExitCode != 0)
    {
        Environment.Exit(seedingResult.ExitCode);
    }

    return;
}

app.Run();
