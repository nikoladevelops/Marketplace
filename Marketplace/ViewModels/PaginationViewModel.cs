namespace Marketplace.ViewModels
{
    public class PaginationViewModel
    {
        public int PageNumber { get; set; }
        public int MaxCountPages { get; set; }
        public string BaseUrl { get; set; } = "";
    }
}
