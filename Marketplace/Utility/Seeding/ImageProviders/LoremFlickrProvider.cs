namespace Marketplace.Utility.Seeding.ImageProviders
{
    // Fetches images from LoremFlickr.
    // Uses keyword and lock to get a consistent random image.
    public class LoremFlickrProvider : IImageProvider
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<LoremFlickrProvider> _logger;

        public string Name => "LoremFlickr";

        // Creates the provider with http factory and logger.
        public LoremFlickrProvider(IHttpClientFactory factory, ILogger<LoremFlickrProvider> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        // Downloads an 800x600 image for the keyword and lock.
        // Retries once before giving up.
        public async Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct)
        {
            var url = $"https://loremflickr.com/800/600/{keyword}?lock={lockId}";
            var client = _factory.CreateClient("LoremFlickr");

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
                    _logger.LogDebug(ex, "LoremFlickr retry for keyword {Keyword} lock {LockId}", keyword, lockId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LoremFlickr failed for keyword {Keyword} lock {LockId} url {Url}", keyword, lockId, url);

                    return null;
                }
            }

            return null;
        }
    }
}
