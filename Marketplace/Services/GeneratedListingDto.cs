namespace Marketplace.Services
{
    // GeneratedListingDto - simple data bag for AI generated title, description and category.
    public class GeneratedListingDto
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int CategoryId { get; set; }
    }
}
