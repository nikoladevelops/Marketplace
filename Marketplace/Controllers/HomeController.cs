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

        public IActionResult Index(int pageNumber=0, string filter="new", string category="all", string searchTerm=null, string location=null, double minimumPrice=1, double maximumPrice=1000000)
        {
            filter = filter.ToLower();
            category=category.ToLower();

            List<SimplifiedAdvertisementViewModel> adsResult = null;
            IQueryable<AdvertisementModel> currentQuery = _context.Advertisements;

            var loadAdsPerPage = 3;
            int categoryId = -1;

            if (searchTerm!=null)
            {
                currentQuery = currentQuery
                    .Where(x => x.Title.Contains(searchTerm));
            }

            switch (location)
            {
                case null:
                    // dont filter then
                    break;
                default:
                    location = location.ToLower().Trim();
                    currentQuery = currentQuery
                        .Where(x => x.Location == location);
                    break;
            }

            currentQuery = currentQuery
                .Where(x => x.Price >= minimumPrice && x.Price <= maximumPrice);
            
            switch (category)
            {
                case "all":
                    //no need to filter based on category
                    break;
                default:
                    var allCategories = _context.Categories
                    .Select(x => x.Name.ToLower())
                    .ToList();

                    categoryId = allCategories.IndexOf(category);
                    if (categoryId != -1)
                    {
                        currentQuery = currentQuery
                            .Include(x=>x.Category)
                            .Where(x => x.Category.Name.ToLower() == category);
                    }
                    else
                    {
                        // no such caregory exists
                        return NotFound();
                    }
                    break;
            }

            switch (filter)
            {
                case "new":
                    currentQuery = currentQuery
                        .OrderByDescending(x => x.DateCreatedOn);
                    break;
                case "old":
                    currentQuery = currentQuery
                        .OrderBy(x => x.DateCreatedOn);
                    break;
                case "cheapest":
                    currentQuery = currentQuery
                        .OrderBy(x => x.Price);
                        break;
                case "most expensive":
                    currentQuery = currentQuery
                        .OrderByDescending(x => x.Price);
                    break;
                default: // just dont order then
                    break;
            }

            adsResult = currentQuery
                .Skip(pageNumber * loadAdsPerPage)
                .Take(loadAdsPerPage)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImageInBase64 = Convert.ToBase64String(x.ImageData),
                }).ToList();


            var homeVM = new HomeViewModel()
            {
                CategoryDropDown = _context.Categories
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList(),
                Advertisements = adsResult,
                SearchTerm=searchTerm,
                CategoryId=categoryId+1, // adding +1 so the indexes are correct because I've added an additional category 'All' in the drop down menu in the view
                PageNumber=pageNumber
            };

            return View(homeVM);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}