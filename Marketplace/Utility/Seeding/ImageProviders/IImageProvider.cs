namespace Marketplace.Utility.Seeding.ImageProviders
{
    public interface IImageProvider
    {
        string Name { get; }
        Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct);
    }
}
