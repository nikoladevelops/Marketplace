namespace Marketplace.ViewModels
{
    // PaginationViewModel
    // Tiny helper for the paging bar. Keeps the numbers and the URL together
    // so the _Pagination partial can be reused everywhere.

    public class PaginationViewModel
    {
        // Current page, zero based. 0 is the first page.
        public int PageNumber { get; set; }

        // Total number of pages.
        public int MaxCountPages { get; set; }

        // Base URL without the page param, e.g. "/Home/Index?searchTerm=cars"
        public string BaseUrl { get; set; } = "";

        // Query param name for the page. Home/Admin/Profile use "pageNumber",
        // Chat uses "page". This lets the same partial work for both.
        public string PageParamName { get; set; } = "pageNumber";

        // How many pages to show around the current one. 5 gives a nice 7-9 total.
        public int WindowSize { get; set; } = 5;
    }
}
