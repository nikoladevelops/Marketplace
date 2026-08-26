using Marketplace.Models;
using Marketplace.Utility.Seeding.ImageProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Marketplace.Utility.Seeding
{
    /// <summary>
    /// Creates demo users and sample advertisements with images.
    /// Categories are taken from the database. Requires setup to have run first.
    /// Each ad gets at least 2 images and the main image always has a valid file.
    /// </summary>
    public class DemoContentSeeder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DemoContentSeeder> _logger;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _webEnv;
        private readonly IHostEnvironment _hostEnv;

        private const string DemoPassword = "aaaaaaA!1";
        private static int _imageLock = 1;

        private static readonly string[] DemoUsers =
        [
            "demo_maria", "demo_georgi", "demo_stefan", "demo_ivana",
            "demo_petar", "demo_elena", "demo_nikola", "demo_radka"
        ];

        private static readonly string[] PremiumUsers = ["demo_maria", "demo_georgi"];

        private static readonly (string Name, double Lat, double Lng)[] Cities =
        [
            ("Sofia", 42.6977, 23.3219),
            ("Plovdiv", 42.1354, 24.7453),
            ("Varna", 43.2141, 27.9147),
            ("Burgas", 42.5048, 27.4626),
            ("Ruse", 43.8356, 25.9657),
            ("Stara Zagora", 42.4258, 25.6345),
            ("Pleven", 43.4170, 24.6067)
        ];

        private static readonly Dictionary<string, string[]> CategoryKeywords = new()
        {
            ["Furniture"] = ["sofa", "armchair,wooden"],
            ["Home Appliances"] = ["kettle,kitchen", "vacuum,cleaner"],
            ["Fashion & Accessories"] = ["jacket,fashion", "handbag,leather"],
            ["Smartphones"] = ["smartphone", "iphone"],
            ["Computers & Laptops"] = ["laptop", "keyboard,mechanical"],
            ["Audio & Headphones"] = ["headphones", "speaker,bluetooth"],
            ["TV & Home Entertainment"] = ["television", "projector"],
            ["Cameras & Photography"] = ["camera,dslr", "tripod,camera"],
            ["Sports & Outdoors"] = ["bicycle", "tent,camping"]
        };

        private static readonly Dictionary<string, (string[] Titles, double MinPrice, double MaxPrice)> CategoryContent = new()
        {
            ["Furniture"] = (["Vintage oak dining table", "Three-seat fabric sofa", "Scandinavian armchair", "Solid wood bookshelf", "Retro walnut coffee table", "Corner desk with drawers"], 40, 900),
            ["Home Appliances"] = (["Electric kettle 1.7L", "Robot vacuum cleaner", "Stand mixer 1000W", "Air fryer 5L digital", "Steam iron ceramic soleplate", "Espresso machine semi-automatic"], 25, 450),
            ["Fashion & Accessories"] = (["Leather biker jacket", "Winter parka size L", "Genuine leather handbag", "Wool overcoat charcoal", "Canvas backpack vintage style", "Silk scarf floral print"], 15, 250),
            ["Smartphones"] = (["iPhone 12 128GB", "Samsung Galaxy S21 5G", "Google Pixel 6a", "Xiaomi Redmi Note 11", "OnePlus Nord 2", "iPhone SE 2020 64GB"], 120, 1200),
            ["Computers & Laptops"] = (["ThinkPad T480 i5 16GB", "MacBook Air M1 256GB", "Mechanical keyboard RGB", "Dell UltraSharp 27 monitor", "RTX 3060 gaming PC", "Logitech MX Master 3 mouse"], 30, 1500),
            ["Audio & Headphones"] = (["Sony WH-1000XM4 headphones", "JBL Flip 5 portable speaker", "Audio-Technica ATH-M50x", "Marshall Stanmore II speaker", "Apple AirPods Pro", "Studio monitor pair 5 inch"], 20, 400),
            ["TV & Home Entertainment"] = (["LG 55 inch 4K Smart TV", "Samsung soundbar 3.1", "Full HD projector 1080p", "PlayStation 4 Slim 1TB", "Xbox Series S", "TV wall mount full motion"], 35, 800),
            ["Cameras & Photography"] = (["Canon EOS 750D kit 18-55mm", "Nikon D3500 kit", "GoPro Hero 8 Black", "Carbon tripod ball head", "Godox speedlight flash", "Fujifilm Instax Mini 11"], 40, 950),
            ["Sports & Outdoors"] = (["Mountain bike 26 inch", "3-person tent waterproof", "Yoga mat 6mm TPE", "Adjustable dumbbells 20kg", "Camping stove portable", "Hiking backpack 45L"], 15, 700)
        };

        private static readonly Dictionary<string, string> TitleKeywords = new()
        {
            ["Vintage oak dining table"] = "dining-table",
            ["Three-seat fabric sofa"] = "sofa",
            ["Scandinavian armchair"] = "armchair",
            ["Solid wood bookshelf"] = "bookshelf",
            ["Retro walnut coffee table"] = "coffee-table",
            ["Corner desk with drawers"] = "desk",
            ["Electric kettle 1.7L"] = "kettle",
            ["Robot vacuum cleaner"] = "vacuum-cleaner",
            ["Stand mixer 1000W"] = "stand-mixer",
            ["Air fryer 5L digital"] = "air-fryer",
            ["Steam iron ceramic soleplate"] = "iron",
            ["Espresso machine semi-automatic"] = "espresso-machine",
            ["Leather biker jacket"] = "leather-jacket",
            ["Winter parka size L"] = "parka",
            ["Genuine leather handbag"] = "handbag",
            ["Wool overcoat charcoal"] = "overcoat",
            ["Canvas backpack vintage style"] = "backpack",
            ["Silk scarf floral print"] = "scarf",
            ["iPhone 12 128GB"] = "iphone",
            ["Samsung Galaxy S21 5G"] = "samsung-galaxy",
            ["Google Pixel 6a"] = "pixel-phone",
            ["Xiaomi Redmi Note 11"] = "xiaomi-phone",
            ["OnePlus Nord 2"] = "oneplus-phone",
            ["iPhone SE 2020 64GB"] = "iphone",
            ["ThinkPad T480 i5 16GB"] = "thinkpad",
            ["MacBook Air M1 256GB"] = "macbook",
            ["Mechanical keyboard RGB"] = "keyboard",
            ["Dell UltraSharp 27 monitor"] = "monitor",
            ["RTX 3060 gaming PC"] = "gaming-pc",
            ["Logitech MX Master 3 mouse"] = "mouse",
            ["Sony WH-1000XM4 headphones"] = "headphones",
            ["JBL Flip 5 portable speaker"] = "speaker",
            ["Audio-Technica ATH-M50x"] = "headphones",
            ["Marshall Stanmore II speaker"] = "speaker",
            ["Apple AirPods Pro"] = "airpods",
            ["Studio monitor pair 5 inch"] = "studio-monitor",
            ["LG 55 inch 4K Smart TV"] = "television",
            ["Samsung soundbar 3.1"] = "soundbar",
            ["Full HD projector 1080p"] = "projector",
            ["PlayStation 4 Slim 1TB"] = "playstation",
            ["Xbox Series S"] = "xbox",
            ["TV wall mount full motion"] = "tv-mount",
            ["Canon EOS 750D kit 18-55mm"] = "canon-camera",
            ["Nikon D3500 kit"] = "nikon-camera",
            ["GoPro Hero 8 Black"] = "gopro",
            ["Carbon tripod ball head"] = "tripod",
            ["Godox speedlight flash"] = "camera-flash",
            ["Fujifilm Instax Mini 11"] = "instax",
            ["Mountain bike 26 inch"] = "bicycle",
            ["3-person tent waterproof"] = "tent",
            ["Yoga mat 6mm TPE"] = "yoga-mat",
            ["Adjustable dumbbells 20kg"] = "dumbbells",
            ["Camping stove portable"] = "camping-stove",
            ["Hiking backpack 45L"] = "hiking-backpack"
        };

        private static readonly string[] ConditionPhrases =
        [
            "Barely used, kept in a smoke-free home.",
            "Used but fully working, normal signs of wear.",
            "Like new, original box included.",
            "Small cosmetic scratches, mechanically perfect."
        ];

        public DemoContentSeeder(IServiceScopeFactory scopeFactory, IConfiguration config, IServiceProvider serviceProvider, IWebHostEnvironment webEnv, IHostEnvironment hostEnv, ILogger<DemoContentSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _serviceProvider = serviceProvider;
            _webEnv = webEnv;
            _hostEnv = hostEnv;
            _logger = logger;
        }

        /// <summary>Ensures demo users exist, then creates up to <paramref name="adCount"/> advertisements.</summary>
        /// <returns>The number of ads actually created.</returns>
        public async Task<int> SeedAsync(int adCount, CancellationToken ct = default)
        {
            adCount = Math.Clamp(adCount, 1, 200);

            if (_hostEnv.IsDevelopment())
            {
                try
                {
                    await EnsureFallbackImagesExistAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to ensure fallback images exist");
                }
            }

            using var preScope = _scopeFactory.CreateScope();
            var preContext = preScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbCategoriesFirst = await preContext.Categories.AsNoTracking().ToListAsync(ct);
            if (dbCategoriesFirst.Count == 0)
            {
                var msg = "Demo seeding aborted: no categories found in database. Run 'dotnet run -- setup' first to seed categories.";
                Console.Error.WriteLine(msg);
                _logger.LogError(msg);
                return 0;
            }

            var users = await EnsureDemoUsersAsync(ct);
            if (users.Count == 0)
            {
                Console.WriteLine("Demo seeding aborted: could not create demo users. Ensure roles exist by running 'dotnet run -- setup' first.");
                return 0;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            var dbCategories = await context.Categories.AsNoTracking().ToListAsync(ct);
            if (dbCategories.Count == 0)
            {
                var msg = "Demo seeding aborted: no categories found in database. Run 'dotnet run -- setup' first to seed categories.";
                Console.Error.WriteLine(msg);
                _logger.LogError(msg);
                return 0;
            }

            var random = new Random();
            int created = 0;
            int failed = 0;

            for (int i = 0; i < adCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var owner = users[i % users.Count];
                    var category = dbCategories[random.Next(dbCategories.Count)];
                    var categoryName = category.Name;

                    string title;
                    double minPrice;
                    double maxPrice;

                    if (CategoryContent.TryGetValue(categoryName, out var content))
                    {
                        title = content.Titles[random.Next(content.Titles.Length)];
                        minPrice = content.MinPrice;
                        maxPrice = content.MaxPrice;
                        if (title.Length > 35) title = title.Substring(0, 35);
                        if (title.Length < 3) title = categoryName + " item";
                    }
                    else
                    {
                        var fallbackTitles = CategoryContent.Values.SelectMany(v => v.Titles).ToArray();
                        title = fallbackTitles[random.Next(fallbackTitles.Length)];
                        if (title.Length > 35) title = title.Substring(0, 35);
                        minPrice = 20;
                        maxPrice = 500;
                        _logger.LogDebug("Using fallback title for unknown category {Category}", categoryName);
                    }

                    var city = Cities[random.Next(Cities.Length)];
                    var price = Math.Round(minPrice + random.NextDouble() * (maxPrice - minPrice), 2);
                    if (price < 1) price = 1;

                    // Main image: guaranteed to return a file via provider chain + local fallback
                    var mainImage = await FetchImageWithChainAsync(title, categoryName, ct);
                    if (mainImage == null)
                    {
                        var msg = $"Failed to obtain any image for ad '{title}' [{categoryName}] even after provider chain and local fallback. Skipping this ad.";
                        Console.Error.WriteLine(msg);
                        _logger.LogError(msg);
                        failed++;
                        continue;
                    }

                    var imagePath = await Helper.SaveImageAsync(mainImage, "advertisements", env);
                    if (imagePath == null)
                    {
                        var msg = $"Failed to save image for ad '{title}' [{categoryName}]: Helper returned null.";
                        Console.Error.WriteLine(msg);
                        _logger.LogError(msg);
                        failed++;
                        continue;
                    }

                    var description = BuildDescription(title, city.Name, random);

                    var ad = new AdvertisementModel
                    {
                        ImagePath = imagePath,
                        Title = title,
                        Description = description,
                        Price = price,
                        Location = $"{city.Name}, Bulgaria",
                        Latitude = city.Lat + (random.NextDouble() - 0.5) * 0.12,
                        Longitude = city.Lng + (random.NextDouble() - 0.5) * 0.12,
                        UserId = owner.Id,
                        CategoryId = category.Id,
                        DateCreatedOn = DateTime.UtcNow.AddDays(-random.Next(0, 90))
                    };
                    context.Advertisements.Add(ad);
                    await context.SaveChangesAsync(ct);

                    // At least 1 extra, up to 2 extras, all guaranteed via chain
                    int extraCount = random.Next(1, 3);
                    int extrasCreated = 0;
                    for (int e = 0; e < extraCount; e++)
                    {
                        var extraImage = await FetchImageWithChainAsync(title, categoryName, ct);
                        if (extraImage == null)
                        {
                            _logger.LogWarning("Extra image {Index} for ad '{Title}' [{Category}] failed to fetch from all providers, skipping this extra", e, title, categoryName);
                            continue;
                        }
                        var extraPath = await Helper.SaveImageAsync(extraImage, "advertisements", env);
                        if (extraPath == null)
                        {
                            _logger.LogWarning("Failed to save extra image {Index} for ad '{Title}'", e, title);
                            continue;
                        }
                        context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = extraPath,
                            AdvertisementId = ad.Id
                        });
                        extrasCreated++;
                    }

                    // Safety net: if no extra succeeded (should not happen due to local fallback), create one more attempt
                    if (extrasCreated == 0)
                    {
                        var fallbackExtra = await FetchImageWithChainAsync(title, categoryName, ct);
                        if (fallbackExtra != null)
                        {
                            var extraPath = await Helper.SaveImageAsync(fallbackExtra, "advertisements", env);
                            if (extraPath != null)
                            {
                                context.AdvertisementImages.Add(new AdvertisementImageModel
                                {
                                    ImagePath = extraPath,
                                    AdvertisementId = ad.Id
                                });
                            }
                        }
                    }

                    await context.SaveChangesAsync(ct);

                    // Verify at least 2 images total
                    var totalImages = 1 + await context.AdvertisementImages.CountAsync(a => a.AdvertisementId == ad.Id, ct);
                    if (totalImages < 2)
                    {
                        _logger.LogWarning("Ad '{Title}' ended with only {Count} images, this should not happen", title, totalImages);
                    }

                    created++;
                    if (created % 5 == 0)
                    {
                        Console.WriteLine($"  ...{created} demo ads created so far.");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    var msg = $"Failed to create demo ad #{i + 1}: {ex.GetType().Name}: {ex.Message}";
                    Console.Error.WriteLine(msg);
                    _logger.LogError(ex, "Failed to create demo ad #{Index}", i + 1);
                    // Continue to next ad instead of aborting whole batch
                }
            }

            if (failed > 0)
            {
                Console.WriteLine($"Demo seeding finished with {failed} failures: {created} advertisements created successfully.");
                _logger.LogWarning("Demo seeding completed with failures: {Created} created, {Failed} failed", created, failed);
            }

            return created;
        }

        private async Task<List<ApplicationUser>> EnsureDemoUsersAsync(CancellationToken ct)
        {
            var result = new List<ApplicationUser>();
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var name in DemoUsers)
            {
                var user = await userManager.FindByNameAsync(name);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = name,
                        Email = $"{name}@example.com",
                        EmailConfirmed = true
                    };
                    var createResult = await userManager.CreateAsync(user, DemoPassword);
                    if (!createResult.Succeeded)
                    {
                        Console.WriteLine($"Could not create demo user '{name}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                        continue;
                    }
                    user = await userManager.FindByNameAsync(name);
                }
                if (user == null) continue;

                var role = PremiumUsers.Contains(name) ? Helper.PremiumRole : Helper.SellerRole;
                try
                {
                    if (!await userManager.IsInRoleAsync(user, role))
                    {
                        var roleResult = await userManager.AddToRoleAsync(user, role);
                        if (!roleResult.Succeeded)
                        {
                            Console.WriteLine($"Could not add role '{role}' to demo user '{name}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}. Run 'dotnet run -- setup' first.");
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"Could not add role '{role}' to demo user '{name}': {ex.Message}. Run 'dotnet run -- setup' first to create roles.");
                    _logger.LogWarning(ex, "Missing role {Role} for demo user {User}", role, name);
                }
                result.Add(user);
            }

            return result;
        }

        private string GetKeyword(string title, string categoryName)
        {
            if (!string.IsNullOrEmpty(title) && TitleKeywords.TryGetValue(title, out var titleKw))
                return titleKw;
            if (CategoryKeywords.TryGetValue(categoryName, out var catKw))
            {
                var lockPreview = _imageLock;
                return catKw[lockPreview % catKw.Length];
            }
            return categoryName.ToLowerInvariant()
                .Replace(" & ", "-")
                .Replace(" ", "-")
                .Replace("/", "-");
        }

        private async Task<IFormFile?> FetchImageWithChainAsync(string title, string categoryName, CancellationToken ct)
        {
            var keyword = GetKeyword(title, categoryName);
            var lockId = Interlocked.Increment(ref _imageLock);
            var orderedProviders = GetOrderedProviders();

            foreach (var providerName in orderedProviders)
            {
                var provider = ResolveProvider(providerName);
                if (provider == null)
                {
                    _logger.LogWarning("Image provider '{Name}' not registered, skipping", providerName);
                    continue;
                }

                try
                {
                    var bytes = await provider.FetchAsync(keyword, lockId, ct);
                    if (bytes != null && bytes.Length > 0)
                    {
                        if (provider.Name != "LocalFallback")
                            _logger.LogDebug("Image for '{Title}' [{Category}] fetched via {Provider} with keyword '{Keyword}'", title, categoryName, provider.Name, keyword);
                        var safeName = string.IsNullOrEmpty(title) ? categoryName : title;
                        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Image", $"{safeName}-{lockId}.jpg")
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = "image/jpeg"
                        };
                    }
                    else
                    {
                        _logger.LogDebug("Provider {Provider} returned empty for '{Title}' [{Category}] keyword '{Keyword}'", provider.Name, title, categoryName, keyword);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var msg = $"Provider {provider.Name} failed for ad '{title}' [{categoryName}] keyword '{keyword}' lock {lockId}: {ex.GetType().Name}: {ex.Message}";
                    Console.Error.WriteLine(msg);
                    _logger.LogWarning(ex, "Image provider {Provider} failed for '{Title}' [{Category}]", provider.Name, title, categoryName);
                }
            }

            var finalMsg = $"All image providers failed for ad '{title}' [{categoryName}] keyword '{keyword}' lock {lockId}";
            Console.Error.WriteLine(finalMsg);
            _logger.LogError(finalMsg);
            return null;
        }

        private IImageProvider? ResolveProvider(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "loremflickr" => _serviceProvider.GetService(typeof(LoremFlickrProvider)) as IImageProvider,
                "unsplash" => _serviceProvider.GetService(typeof(UnsplashProvider)) as IImageProvider,
                "picsum" => _serviceProvider.GetService(typeof(PicsumProvider)) as IImageProvider,
                "localfallback" => _serviceProvider.GetService(typeof(LocalFallbackProvider)) as IImageProvider,
                _ => null
            };
        }

        private string[] GetOrderedProviders()
        {
            // Env var takes precedence for easy switching: DEMO_IMAGE_PROVIDERS=LoremFlickr,Picsum
            var env = Environment.GetEnvironmentVariable("DEMO_IMAGE_PROVIDERS");
            if (!string.IsNullOrWhiteSpace(env))
            {
                var parts = env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0) return FilterProvidersForEnv(parts);
            }

            var configProviders = _config.GetSection("DemoSeeding:ImageProviders").Get<string[]>();
            if (configProviders != null && configProviders.Length > 0) return FilterProvidersForEnv(configProviders);

            return FilterProvidersForEnv(["LoremFlickr", "Unsplash", "Picsum", "LocalFallback"]);
        }

        private string[] FilterProvidersForEnv(string[] providers)
        {
            if (_hostEnv.IsDevelopment()) return providers;
            return providers.Where(p => !p.Equals("LocalFallback", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        private async Task EnsureFallbackImagesExistAsync(CancellationToken ct)
        {
            var fallbackDir = Path.Combine(_webEnv.WebRootPath, "seed-fallback");
            Directory.CreateDirectory(fallbackDir);

            byte[]? plusBytes = null;
            var plusPath = Path.Combine(_webEnv.WebRootPath, "plusSign.png");
            if (File.Exists(plusPath))
            {
                try { plusBytes = await File.ReadAllBytesAsync(plusPath, ct); } catch { plusBytes = null; }
            }

            var categoryToFile = new Dictionary<string, string>
            {
                ["Furniture"] = "furniture.jpg",
                ["Home Appliances"] = "home-appliances.jpg",
                ["Fashion & Accessories"] = "fashion.jpg",
                ["Smartphones"] = "smartphones.jpg",
                ["Computers & Laptops"] = "computers.jpg",
                ["Audio & Headphones"] = "audio.jpg",
                ["TV & Home Entertainment"] = "tv.jpg",
                ["Cameras & Photography"] = "cameras.jpg",
                ["Sports & Outdoors"] = "sports.jpg",
            };

            var ordered = GetOrderedProviders().Where(p => !p.Equals("LocalFallback", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (ordered.Length == 0) ordered = ["LoremFlickr", "Picsum"];

            foreach (var kv in categoryToFile)
            {
                var filePath = Path.Combine(fallbackDir, kv.Value);
                bool needsGenerate = false;
                if (!File.Exists(filePath)) needsGenerate = true;
                else if (plusBytes != null)
                {
                    try
                    {
                        var existing = await File.ReadAllBytesAsync(filePath, ct);
                        if (existing.Length == plusBytes.Length && existing.SequenceEqual(plusBytes))
                            needsGenerate = true;
                    }
                    catch { needsGenerate = true; }
                }

                if (!needsGenerate) continue;

                var title = CategoryContent.TryGetValue(kv.Key, out var cc) ? cc.Titles[0] : kv.Key;
                var keyword = GetKeyword(title, kv.Key);
                byte[]? bytes = null;
                string? usedProvider = null;
                foreach (var providerName in ordered)
                {
                    var provider = ResolveProvider(providerName);
                    if (provider == null) continue;
                    try
                    {
                        bytes = await provider.FetchAsync(keyword, Random.Shared.Next(1, 100000), ct);
                        if (bytes != null && bytes.Length > 0)
                        {
                            usedProvider = provider.Name;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Fallback generation provider {Provider} failed for {Category}", providerName, kv.Key);
                    }
                }

                if (bytes != null && bytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(filePath, bytes, ct);
                    _logger.LogInformation("Generated dev fallback image for {Category} via {Provider} keyword {Keyword}", kv.Key, usedProvider, keyword);
                }
                else
                {
                    _logger.LogWarning("Could not generate fallback image for {Category} keyword {Keyword}", kv.Key, keyword);
                }
            }

            var genericPath = Path.Combine(fallbackDir, "generic.jpg");
            if (!File.Exists(genericPath) || (plusBytes != null && (await File.ReadAllBytesAsync(genericPath, ct)).SequenceEqual(plusBytes)))
            {
                var keyword = "marketplace";
                byte[]? bytes = null;
                foreach (var providerName in ordered)
                {
                    var provider = ResolveProvider(providerName);
                    if (provider == null) continue;
                    try
                    {
                        bytes = await provider.FetchAsync(keyword, Random.Shared.Next(1, 100000), ct);
                        if (bytes != null && bytes.Length > 0) break;
                    }
                    catch { }
                }
                if (bytes != null)
                {
                    await File.WriteAllBytesAsync(genericPath, bytes, ct);
                    _logger.LogInformation("Generated generic fallback image");
                }
            }
        }

        private static string BuildDescription(string title, string cityName, Random random)
        {
            var condition = ConditionPhrases[random.Next(ConditionPhrases.Length)];
            var baseDesc = $"{title}. {condition} Pick-up preferred in {cityName}, but I can meet somewhere central. Cash on delivery or bank transfer, message me for details.";
            if (baseDesc.Length < 20)
                baseDesc = baseDesc.PadRight(20, '.');
            if (baseDesc.Length > 250)
                baseDesc = baseDesc.Substring(0, 250);
            return baseDesc;
        }
    }
}
