using Marketplace.Models;
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
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int pageNumber=0, string filter="new", string category="all", string? searchTerm=null, string? location=null, double minimumPrice=1, double maximumPrice=1000000)
        {
            if (pageNumber < 0)
            {
                pageNumber = 0;
            }

            filter = filter.ToLower();
            category = category.ToLower();

            IQueryable<AdvertisementModel> currentQuery = _context.Advertisements;
            int loadAdsPerPage = 24;
            int categoryId = -1;

            if (searchTerm != null)
            {
                currentQuery = currentQuery.Where(x => x.Title.Contains(searchTerm));
            }

            if (location != null)
            {
                location = location.ToLower().Trim();
                currentQuery = currentQuery.Where(x => x.Location == location);
            }

            currentQuery = currentQuery.Where(x => x.Price >= minimumPrice && x.Price <= maximumPrice);
            
            if (category != "all")
            {
                var allCategories = _context.Categories.Select(x => x.Name.ToLower()).ToList();
                categoryId = allCategories.IndexOf(category);
                if (categoryId != -1)
                {
                    currentQuery = currentQuery.Include(x => x.Category).Where(x => x.Category.Name.ToLower() == category);
                }
            }

            currentQuery = filter switch
            {
                "new" => currentQuery.OrderByDescending(x => x.DateCreatedOn),
                "old" => currentQuery.OrderBy(x => x.DateCreatedOn),
                "cheapest" => currentQuery.OrderBy(x => x.Price),
                "most expensive" => currentQuery.OrderByDescending(x => x.Price),
                _ => currentQuery
            };

            var countFilteredAds = currentQuery.Count();

            var adsResult = currentQuery
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

            var homeVM = new HomeViewModel()
            {
                CategoryDropDown = _context.Categories.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList(),
                Advertisements = adsResult,
                SearchTerm = searchTerm,
                CategoryId = categoryId + 1,
                PageNumber = pageNumber,
                MaxCountPages = (int)Math.Ceiling((double)countFilteredAds / loadAdsPerPage)
            };

            return View(homeVM);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}