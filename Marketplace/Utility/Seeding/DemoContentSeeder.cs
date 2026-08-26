using Marketplace.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Marketplace.Utility.Seeding
{
    /// <summary>
    /// Creates demo users and sample advertisements with images.
    /// Fetches images online and falls back to a placeholder when offline.
    /// Requires <see cref="IdentityAndCatalogSeeder"/> to have run first, but will seed missing categories if needed.
    /// </summary>
    public class DemoContentSeeder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<DemoContentSeeder> _logger;

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

        private static readonly string[] ConditionPhrases =
        [
            "Barely used, kept in a smoke-free home.",
            "Used but fully working, normal signs of wear.",
            "Like new, original box included.",
            "Small cosmetic scratches, mechanically perfect."
        ];

        public DemoContentSeeder(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory, ILogger<DemoContentSeeder> logger)
        {
            _scopeFactory = scopeFactory;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        /// <summary>Ensures demo users exist, then creates up to <paramref name="adCount"/> advertisements.</summary>
        /// <returns>The number of ads actually created.</returns>
        public async Task<int> SeedAsync(int adCount, CancellationToken ct = default)
        {
            adCount = Math.Clamp(adCount, 1, 200);

            var users = await EnsureDemoUsersAsync(ct);
            if (users.Count == 0)
            {
                Console.WriteLine("Demo seeding aborted: could not create demo users.");
                return 0;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

            // Ensure categories exist even if setup was skipped.
            if (!await context.Categories.AnyAsync(ct))
            {
                _logger.LogWarning("No categories found, seeding essential categories first.");
                var catalogSeeder = scope.ServiceProvider.GetRequiredService<IdentityAndCatalogSeeder>();
                await catalogSeeder.SeedCategoriesAsync(ct);
            }

            var categoryNames = CategoryContent.Keys.ToList();
            var random = new Random();
            int created = 0;

            for (int i = 0; i < adCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var owner = users[i % users.Count];
                var categoryName = categoryNames[random.Next(categoryNames.Count)];
                var (titles, minPrice, maxPrice) = CategoryContent[categoryName];
                var title = titles[random.Next(titles.Length)];
                var city = Cities[random.Next(Cities.Length)];
                var price = Math.Round(minPrice + random.NextDouble() * (maxPrice - minPrice), 2);

                var mainImage = await FetchImageAsync(categoryName, ct);
                string imagePath;
                if (mainImage != null)
                {
                    imagePath = await Helper.SaveImageAsync(mainImage, "advertisements", env) ?? "/plusSign.png";
                }
                else
                {
                    Console.WriteLine($"  image download failed for ad #{i + 1} ({categoryName}), using placeholder.");
                    imagePath = "/plusSign.png";
                }

                var ad = new AdvertisementModel
                {
                    ImagePath = imagePath,
                    Title = title,
                    Description = BuildDescription(title, city.Name, random),
                    Price = price,
                    Location = $"{city.Name}, Bulgaria",
                    Latitude = city.Lat + (random.NextDouble() - 0.5) * 0.12,
                    Longitude = city.Lng + (random.NextDouble() - 0.5) * 0.12,
                    UserId = owner.Id,
                    CategoryId = await GetCategoryIdAsync(context, categoryName, ct),
                    DateCreatedOn = DateTime.UtcNow.AddDays(-random.Next(0, 90))
                };
                context.Advertisements.Add(ad);
                await context.SaveChangesAsync(ct);

                int extraCount = random.Next(0, 3);
                for (int e = 0; e < extraCount; e++)
                {
                    var extraImage = await FetchImageAsync(categoryName, ct);
                    if (extraImage == null) break;
                    var extraPath = await Helper.SaveImageAsync(extraImage, "advertisements", env);
                    if (extraPath != null)
                    {
                        context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = extraPath,
                            AdvertisementId = ad.Id
                        });
                    }
                }
                await context.SaveChangesAsync(ct);

                created++;
                if (created % 5 == 0)
                {
                    Console.WriteLine($"  …{created} demo ads created so far.");
                }
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
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    await userManager.AddToRoleAsync(user, role);
                }
                result.Add(user);
            }

            return result;
        }

        private async Task<IFormFile?> FetchImageAsync(string categoryName, CancellationToken ct)
        {
            var keywords = CategoryKeywords[categoryName];
            var lockId = Interlocked.Increment(ref _imageLock);
            var keyword = keywords[lockId % keywords.Length];
            var url = $"https://loremflickr.com/800/600/{keyword}?lock={lockId}";

            var http = _httpFactory.CreateClient("DemoContentSeeder");
            http.Timeout = TimeSpan.FromSeconds(30);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync(url, ct);
                    if (bytes.Length > 0)
                    {
                        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Image", $"{categoryName}-{lockId}.jpg")
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = "image/jpeg"
                        };
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    Console.WriteLine($"  image retry after error: {ex.Message}");
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }

        private static string BuildDescription(string title, string cityName, Random random)
        {
            var condition = ConditionPhrases[random.Next(ConditionPhrases.Length)];
            return $"{title}. {condition} Pick-up preferred in {cityName}, but I can meet somewhere central. Cash on delivery or bank transfer, message me for details.";
        }

        private static async Task<int> GetCategoryIdAsync(ApplicationDbContext context, string categoryName, CancellationToken ct)
        {
            var cat = await context.Categories.FirstOrDefaultAsync(c => c.Name == categoryName, ct);
            if (cat != null) return cat.Id;
            var created = new CategoryModel { Name = categoryName };
            context.Categories.Add(created);
            await context.SaveChangesAsync(ct);
            return created.Id;
        }
    }
}
