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

// Filtering service
builder.Services.AddScoped<AdvertisementFilterService>();
builder.Services.AddScoped<UserAdministrationService>();
builder.Services.AddScoped<AdvertisementService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ChatService>();

// Seeding services for Utility/Seeding folder
builder.Services.AddScoped<IdentityAndCatalogSeeder>();
builder.Services.AddScoped<DemoContentSeeder>();
builder.Services.AddScoped<DevDatabaseCleaner>();
builder.Services.AddScoped<UserSeeder>();

// Add Identity support + tables
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Persist Data Protection keys so auth cookies survive an app restart. Without
// this the keys are regenerated on each run and previously issued cookies fail
// validation, so SignalR sees an unauthenticated user and the chat hub
// rejects the connection.
// DEV on Linux (CachyOS): this app defaults to HTTP-only (http://localhost:5256) to avoid
// dev-certs pain on Arch. If you re-enable HTTPS in launchSettings.json
// (https://localhost:7256) run `dotnet dev-certs https --trust`.
// PRODUCTION: HTTPS is terminated at the reverse proxy (nginx/caddy). Configure the proxy
// to forward X-Forwarded-Proto and X-Forwarded-For and set
// ASPNETCORE_FORWARDEDHEADERS_ENABLED=true or enable ForwardedHeaders below.
// Kestrel then binds http://*:80 behind the proxy. Keys are persisted to Postgres
// so cookies stay valid across restarts.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// Forwarded headers for production reverse proxy (nginx/caddy), harmless in dev.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Early help: no DB required, does not touch CONNECTION_STRING
if (SeedingCommands.TryHandleEarlyHelp(args))
{
    return;
}

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
    // In dev, only redirect non-hub requests that are already HTTPS.
    // On http://localhost:5256 this is a no-op, so no dev cert is needed and
    // SignalR stays on the same scheme (ws, not wss).
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/hubs") && ctx.Request.IsHttps,
        branch => branch.UseHttpsRedirection());
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseMiddleware<BannedUserMiddleware>();

app.UseAuthorization();

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
        Environment.Exit(seedingResult.ExitCode);
    return;
}

app.Run();
