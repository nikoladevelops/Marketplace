using Microsoft.AspNetCore.Mvc.Rendering;

namespace Marketplace.ViewModels
{
    // View model for the home page with search and filters.
    public class HomeViewModel
    {
        // Dropdown for categories
        public IEnumerable<SelectListItem> CategoryDropDown { get; set; } = new List<SelectListItem>();

        // Ads to show on the page
        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; } = new List<SimplifiedAdvertisementViewModel>();

        // Filters from the user
        public string? SearchTerm { get; set; }

        public int CategoryId { get; set; }

        public string Filter { get; set; } = "";

        public string? Location { get; set; }

        public string MinimumPrice { get; set; } = "";

        public string MaximumPrice { get; set; } = "";

        // Paging
        public int PageNumber { get; set; }

        public int MaxCountPages { get; set; }
    }
}
