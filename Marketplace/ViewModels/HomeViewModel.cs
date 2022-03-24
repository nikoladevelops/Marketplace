using Microsoft.AspNetCore.Mvc.Rendering;

namespace Marketplace.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<SelectListItem> CategoryDropDown { get; set; }
        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; }
        public string? SearchTerm { get; set; }
        public int CategoryId { get; set; }
        public string Filter { get; set; }
        public string? Location { get; set; }
        public string MinimumPrice { get; set; }
        public string MaximumPrice { get; set; }
        public int PageNumber { get; set; }
        public int MaxCountPages { get; set; }
    }
}
