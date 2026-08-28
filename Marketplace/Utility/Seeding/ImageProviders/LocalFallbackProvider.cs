using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Marketplace.Utility.Seeding.ImageProviders
{
    // Serves images from wwwroot/seed-fallback when running in Development.
    // This is the last resort in the provider chain so seeding never fails offline.
    public class LocalFallbackProvider : IImageProvider
    {
        private readonly IWebHostEnvironment _webEnv;
        private readonly IHostEnvironment _hostEnv;
        private readonly ILogger<LocalFallbackProvider> _logger;

        public string Name => "LocalFallback";

        // Creates the provider with hosting info and a logger.
        public LocalFallbackProvider(IWebHostEnvironment webEnv, IHostEnvironment hostEnv, ILogger<LocalFallbackProvider> logger)
        {
            _webEnv = webEnv;
            _hostEnv = hostEnv;
            _logger = logger;
        }

        // Tries to find a local image for the keyword.
        // Checks direct slug files, then category files, then a generic fallback.
        public Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct)
        {
            if (!_hostEnv.IsDevelopment())
            {
                _logger.LogDebug("LocalFallback is dev-only, skipping for keyword {Keyword}", keyword);

                return Task.FromResult<byte[]?>(null);
            }

            // Turn keyword into a safe file slug
            var slug = keyword.ToLowerInvariant()
                .Replace(" & ", "-")
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace(",", "-");

            var candidates = new[]
            {
                Path.Combine(_webEnv.WebRootPath, "seed-fallback", $"{slug}.jpg"),
                Path.Combine(_webEnv.WebRootPath, "seed-fallback", $"{slug}.png"),
            };

            // Try direct slug match first
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("LocalFallback serving per-keyword file {Path}", path);

                    return Task.FromResult<byte[]?>(File.ReadAllBytes(path));
                }
            }

            // Try to match known category files
            var categoryMap = new Dictionary<string, string>
            {
                ["sofa"] = "furniture.jpg",
                ["armchair"] = "furniture.jpg",
                ["bookshelf"] = "furniture.jpg",
                ["dining-table"] = "furniture.jpg",
                ["desk"] = "furniture.jpg",
                ["coffee-table"] = "furniture.jpg",
                ["kettle"] = "home-appliances.jpg",
                ["vacuum-cleaner"] = "home-appliances.jpg",
                ["jacket"] = "fashion.jpg",
                ["handbag"] = "fashion.jpg",
                ["iphone"] = "smartphones.jpg",
                ["smartphone"] = "smartphones.jpg",
                ["laptop"] = "computers.jpg",
                ["keyboard"] = "computers.jpg",
                ["headphones"] = "audio.jpg",
                ["speaker"] = "audio.jpg",
                ["television"] = "tv.jpg",
                ["projector"] = "tv.jpg",
                ["camera"] = "cameras.jpg",
                ["bicycle"] = "sports.jpg",
                ["tent"] = "sports.jpg",
            };

            foreach (var kv in categoryMap)
            {
                if (keyword.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var catPath = Path.Combine(_webEnv.WebRootPath, "seed-fallback", kv.Value);

                    if (File.Exists(catPath))
                    {
                        _logger.LogDebug("LocalFallback serving category file {Path} for keyword {Keyword}", catPath, keyword);

                        return Task.FromResult<byte[]?>(File.ReadAllBytes(catPath));
                    }
                }
            }

            // Final generic fallback
            var generic = Path.Combine(_webEnv.WebRootPath, "seed-fallback", "generic.jpg");

            if (File.Exists(generic))
            {
                _logger.LogDebug("LocalFallback serving generic file");

                return Task.FromResult<byte[]?>(File.ReadAllBytes(generic));
            }

            _logger.LogDebug("LocalFallback has no files for keyword {Keyword}, no plusSign fallback", keyword);

            return Task.FromResult<byte[]?>(null);
        }
    }
}
