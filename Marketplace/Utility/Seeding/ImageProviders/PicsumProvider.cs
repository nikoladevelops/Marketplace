namespace Marketplace.Utility.Seeding.ImageProviders
{
    public class PicsumProvider : IImageProvider
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<PicsumProvider> _logger;

        public string Name => "Picsum";

        public PicsumProvider(IHttpClientFactory factory, ILogger<PicsumProvider> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct)
        {
            var url = $"https://picsum.photos/seed/{keyword}-{lockId}/800/600";
            var client = _factory.CreateClient("Picsum");
            client.Timeout = TimeSpan.FromSeconds(30);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(url, ct);
                    if (bytes.Length > 0) return bytes;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    _logger.LogDebug(ex, "Picsum retry for keyword {Keyword} lock {LockId}", keyword, lockId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Picsum failed for keyword {Keyword} lock {LockId} url {Url}", keyword, lockId, url);
                    return null;
                }
            }
            return null;
        }
    }
}
