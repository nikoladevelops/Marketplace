using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    public class AdvertisementModel
    {
        public int Id { get; set; }
        public byte[] ImageData { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public string Location { get; set; }
        public DateTime DateCreatedOn { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int CategoryId { get; set; }
        public CategoryModel Category { get; set; }
    }
}
