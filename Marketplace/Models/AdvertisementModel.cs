using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class AdvertisementModel
    {
        public int Id { get; set; }
        public string? ImagePath { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Location { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime DateCreatedOn { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int CategoryId { get; set; }
        public CategoryModel Category { get; set; } = null!;
        
        public ICollection<AdvertisementImageModel> AdvertisementImages { get; set; } = new List<AdvertisementImageModel>();
    }
}