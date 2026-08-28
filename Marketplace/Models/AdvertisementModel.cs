using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    // This is the main ad model. Holds all info about a single listing.
    public class AdvertisementModel
    {
        public int Id { get; set; }

        // Main image path (kept for backward compat, new images use AdvertisementImages)
        public string? ImagePath { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string Location { get; set; } = string.Empty;

        // Optional map coordinates
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime DateCreatedOn { get; set; }

        // Who created this ad
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Which category it belongs to
        public int CategoryId { get; set; }

        public CategoryModel Category { get; set; } = null!;

        // All images for this ad, including the main one
        public ICollection<AdvertisementImageModel> AdvertisementImages { get; set; } = new List<AdvertisementImageModel>();
    }
}
