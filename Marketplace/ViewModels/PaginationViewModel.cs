namespace Marketplace.ViewModels
{
    // Tiny helper for paging controls.
    // Keeps page numbers and base URL together.
    public class PaginationViewModel
    {
        public int PageNumber { get; set; }

        public int MaxCountPages { get; set; }

        public string BaseUrl { get; set; } = "";
    }
}
