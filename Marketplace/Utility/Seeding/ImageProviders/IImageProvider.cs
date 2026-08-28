namespace Marketplace.Utility.Seeding.ImageProviders
{
    // Simple contract for any image source used during demo seeding.
    // Each provider knows its name and can fetch bytes for a keyword.
    public interface IImageProvider
    {
        string Name { get; }

        // Tries to get an image for the keyword. Returns null if it fails.
        Task<byte[]?> FetchAsync(string keyword, int lockId, CancellationToken ct);
    }
}
