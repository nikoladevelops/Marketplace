namespace Marketplace.Models
{
    public class AdvertisementImages
    {
        public int Id { get; set; }
        public int AdvertisementId { get; set; }
        public AdvertisementModel Advertisement { get; set; }
        public byte[] Image { get; set; }
    }
}
