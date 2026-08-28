namespace Marketplace.Utility.Seeding.ImageProviders
{
    // Fetches images from Unsplash Source using a keyword.
    // Good for topical images like sofa, bicycle, camera, etc.
    public class UnsplashProvider : IImageProvider
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<UnsplashProvider> _logger;

        public string Name => "Unsplash";

        // Creates the provider with http factory and logger.
        public UnsplashProvider(IHttpClientFactory factory, ILogger<UnsplashProvider> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        // Downloads an 800x600 image for the keyword.
        // Retries once before giving up.
        public async Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct)
        {
            var url = $"https://source.unsplash.com/800x600/?{keyword}";
            var client = _factory.CreateClient("Unsplash");

            client.Timeout = TimeSpan.FromSeconds(30);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(url, ct);

                    if (bytes.Length > 0)
                    {
                        return bytes;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    _logger.LogDebug(ex, "Unsplash retry for keyword {Keyword} lock {LockId}", keyword, lockId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unsplash failed for keyword {Keyword} lock {LockId} url {Url}", keyword, lockId, url);

                    return null;
                }
            }

            return null;
        }
    }
}
