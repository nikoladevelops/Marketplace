using Microsoft.AspNetCore.Mvc.Rendering;

namespace Marketplace.ViewModels
{
    public class HomeViewModel
    {
        public IEnumerable<SelectListItem> CategoryDropDown { get; set; }
        public IEnumerable<SimplifiedAdvertisementViewModel> Advertisements { get; set; }
    }
}
