namespace Marketplace.Utility.Seeding.ImageProviders
{
    public class ImageProviderOptions
    {
        public string[] Providers { get; set; } = ["LoremFlickr", "Unsplash", "Picsum", "LocalFallback"];
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryPerProvider { get; set; } = 2;
    }
}
