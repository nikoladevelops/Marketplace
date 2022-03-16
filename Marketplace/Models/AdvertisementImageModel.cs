namespace Marketplace.Models
{
    public class AdvertisementImageModel
    {
        public int Id { get; set; }
        public int AdvertisementId { get; set; }
        public AdvertisementModel Advertisement { get; set; }
        public byte[] ImageData { get; set; }
    }
}
