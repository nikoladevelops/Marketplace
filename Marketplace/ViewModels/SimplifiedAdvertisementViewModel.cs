namespace Marketplace.ViewModels
{
    public class SimplifiedAdvertisementViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = "";
        public string Location { get; set; } = "";
        public string Category { get; set; } = "";
        public int CategoryId { get; set; }
        public string UserName { get; set; } = "";
        public string UserId { get; set; } = "";
        public DateTime DateCreatedOn { get; set; }
    }
}