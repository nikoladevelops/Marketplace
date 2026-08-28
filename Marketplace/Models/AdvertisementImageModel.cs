namespace Marketplace.Models
{
    // Just an extra image for an ad.
    // One ad can have many of these.
    public class AdvertisementImageModel
    {
        public int Id { get; set; }

        public int AdvertisementId { get; set; }

        public AdvertisementModel Advertisement { get; set; } = null!;

        public string ImagePath { get; set; } = "";
    }
}
