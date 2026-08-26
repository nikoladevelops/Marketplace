using Marketplace.Models;
using Marketplace.Services;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Marketplace.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AdvertisementFilterService _filterService;

        public HomeController(ApplicationDbContext context, AdvertisementFilterService filterService)
        {
            _context = context;
            _filterService = filterService;
        }

        public IActionResult Index(int pageNumber = 0, string filter = "new", string? category = "all", string? searchTerm = null, string? location = null, string? minimumPrice = null, string? maximumPrice = null)
        {
            var vm = BuildHomeViewModel(pageNumber, filter, category, searchTerm, location, minimumPrice, maximumPrice);
            return View(vm);
        }

        [HttpGet]
        public IActionResult Search(int pageNumber = 0, string filter = "new", string? category = "all", string? searchTerm = null, string? location = null, string? minimumPrice = null, string? maximumPrice = null)
        {
            var vm = BuildHomeViewModel(pageNumber, filter, category, searchTerm, location, minimumPrice, maximumPrice);
            return PartialView("_AdGrid", vm);
        }

        private HomeViewModel BuildHomeViewModel(int pageNumber, string filter, string? category, string? searchTerm, string? location, string? minimumPrice, string? maximumPrice)
        {
            if (pageNumber < 0) pageNumber = 0;
            filter = (filter ?? "new").ToLowerInvariant();
            category ??= "all";

            int? categoryId = null;
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(category, out var cid))
                    categoryId = cid;
                else
                {
                    var cat = _context.Categories.AsNoTracking().FirstOrDefault(c => c.Name.ToLower() == category.ToLower());
                    if (cat != null) categoryId = cat.Id;
                }
            }

            IQueryable<AdvertisementModel> query = _context.Advertisements.AsNoTracking();

            query = _filterService.Apply(query, searchTerm, location, categoryId, minimumPrice, maximumPrice);
            query = _filterService.ApplySorting(query, filter);

            int loadAdsPerPage = 24;
            var countFilteredAds = query.Count();

            var adsResult = query
                .Include(x => x.User)
                .Include(x => x.Category)
                .Skip(pageNumber * loadAdsPerPage)
                .Take(loadAdsPerPage)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImagePath = x.ImagePath,
                    Location = x.Location,
                    Category = x.Category.Name,
                    UserName = x.User != null ? (x.User.UserName ?? "Unknown") : "Unknown",
                    DateCreatedOn = x.DateCreatedOn
                }).ToList();

            int effectiveCategoryId = categoryId ?? -1;

            return new HomeViewModel()
            {
                CategoryDropDown = _context.Categories.AsNoTracking().Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
                Advertisements = adsResult,
                SearchTerm = searchTerm,
                CategoryId = effectiveCategoryId == -1 ? -1 : effectiveCategoryId,
                PageNumber = pageNumber,
                MaxCountPages = (int)Math.Ceiling((double)countFilteredAds / loadAdsPerPage)
            };
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}