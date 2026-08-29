using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    // HomeController - shows the home page, search, recommendations and recently viewed helpers.
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AdvertisementFilterService _filterService;

        // Constructor - wires up the database and the filter helper.
        public HomeController(ApplicationDbContext context, AdvertisementFilterService filterService)
        {
            _context = context;
            _filterService = filterService;
        }

        // Index - main home page with paging, filters, category, search, location and price range.
        public IActionResult Index(int pageNumber = 0, string filter = "new", string? category = "all", string? searchTerm = null, string? location = null, string? minimumPrice = null, string? maximumPrice = null)
        {
            var vm = BuildHomeViewModel(pageNumber, filter, category, searchTerm, location, minimumPrice, maximumPrice);

            return View(vm);
        }

        // Search - returns just the ad grid partial for live search without reloading the whole page.
        [HttpGet]
        public IActionResult Search(int pageNumber = 0, string filter = "new", string? category = "all", string? searchTerm = null, string? location = null, string? minimumPrice = null, string? maximumPrice = null)
        {
            var vm = BuildHomeViewModel(pageNumber, filter, category, searchTerm, location, minimumPrice, maximumPrice);

            return PartialView("_AdGrid", vm);
        }

        // ValidateRecent - checks which ids from your recently viewed list still exist, used to prune deleted ads.
        [HttpGet]
        public IActionResult ValidateRecent(string? ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return Json(new { existingIds = Array.Empty<int>() });
            }

            var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var v) ? (int?)v : null)
                .Where(v => v.HasValue && v.Value > 0)
                .Select(v => v!.Value)
                .Distinct()
                .Take(60)
                .ToList();

            if (parsed.Count == 0)
            {
                return Json(new { existingIds = Array.Empty<int>() });
            }

            var existing = _context.Advertisements.AsNoTracking()
                .Where(a => parsed.Contains(a.Id) && a.User.Status == AccountStatus.Active)
                .Select(a => a.Id)
                .ToList();

            return Json(new { existingIds = existing });
        }

        // RecommendationsRequest - small DTO that comes from the browser with viewed ids and a limit.
        public class RecommendationsRequest
        {
            public List<int>? ViewedIds { get; set; }
            public int? Limit { get; set; }
        }

        // Recommendations - smart suggestions based on your recently viewed categories, price and location.
        // Returns JSON { ads: SimplifiedAdvertisementViewModel[] }. Hidden when no history or no candidates.
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult Recommendations([FromBody] RecommendationsRequest req)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var viewedIds = (req?.ViewedIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .Take(50)
                .ToList();

            var limit = Math.Clamp(req?.Limit ?? 15, 1, 20);

            if (viewedIds.Count == 0)
            {
                return Json(new { ads = Array.Empty<object>() });
            }

            // Load viewed ads truth (category, price, location) - tolerates stale localStorage (deleted ids ignored)
            var viewedAds = _context.Advertisements.AsNoTracking()
                .Where(a => viewedIds.Contains(a.Id))
                .Select(a => new { a.Id, a.CategoryId, a.Price, a.Location, a.UserId })
                .ToList();

            if (viewedAds.Count == 0)
            {
                return Json(new { ads = Array.Empty<object>() });
            }

            // Build affinity scores: recency weight, category scores, price band, location tokens
            var catScores = new Dictionary<int, double>();
            var priceVals = new List<decimal>();
            var locTokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < viewedIds.Count; i++)
            {
                var vid = viewedIds[i];

                var va = viewedAds.FirstOrDefault(x => x.Id == vid);

                if (va == null)
                {
                    continue;
                }

                double recencyWeight = Math.Pow(0.85, i);

                catScores[va.CategoryId] = catScores.GetValueOrDefault(va.CategoryId) + recencyWeight;

                priceVals.Add(va.Price);

                if (!string.IsNullOrWhiteSpace(va.Location))
                {
                    var loc = va.Location.Trim();

                    locTokens[loc] = locTokens.GetValueOrDefault(loc) + 1;
                }
            }

            if (catScores.Count == 0)
            {
                return Json(new { ads = Array.Empty<object>() });
            }

            var topCategoryIds = catScores.OrderByDescending(kv => kv.Value)
                .Take(2)
                .Select(kv => kv.Key)
                .ToList();

            // Price band affinity: median plus/minus 35 percent
            priceVals.Sort();

            decimal medianPrice = priceVals[priceVals.Count / 2];
            decimal bandLow = medianPrice * 0.65m;
            decimal bandHigh = medianPrice * 1.35m;

            if (medianPrice == 0)
            {
                bandLow = 0;
                bandHigh = decimal.MaxValue;
            }

            var dominantLocation = locTokens.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;

            IQueryable<AdvertisementModel> q = _context.Advertisements.AsNoTracking()
                .Where(a => topCategoryIds.Contains(a.CategoryId))
                .Where(a => !viewedIds.Contains(a.Id))
                .Where(a => a.User.Status == AccountStatus.Active);

            // Hide own ads when logged in (mirrors RecentlyViewed filtering)
            if (!string.IsNullOrEmpty(currentUserId))
            {
                q = q.Where(a => a.UserId != currentUserId);
            }

            // Fetch candidates (price band is soft, we prefer band but fallback if too few)
            var candidates = q
                .OrderByDescending(a => a.DateCreatedOn)
                .Take(60)
                .Select(a => new SimplifiedAdvertisementViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Price = a.Price,
                    ImagePath = a.ImagePath ?? "/plusSign.png",
                    Location = a.Location ?? "",
                    Category = a.Category.Name,
                    CategoryId = a.CategoryId,
                    UserName = a.User != null ? (a.User.UserName ?? "Unknown") : "Unknown",
                    UserId = a.UserId ?? "",
                    DateCreatedOn = a.DateCreatedOn
                }).ToList();

            // Score and sort candidates, weights are explicit so we can tune later.

            const double wCat = 3.0;
            const double wPrice = 1.0;
            const double wLoc = 0.4;

            double maxCatScore = catScores.Values.DefaultIfEmpty(1).Max();

            var scored = candidates.Select(c =>
            {
                double catScore = catScores.GetValueOrDefault(c.CategoryId) / maxCatScore;

                double priceScore = (c.Price >= bandLow && c.Price <= bandHigh) ? 1.0 : 0.0;

                double locScore = (!string.IsNullOrEmpty(dominantLocation) && c.Location != null && c.Location.Equals(dominantLocation, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;

                double total = wCat * catScore + wPrice * priceScore + wLoc * locScore;

                return new { ad = c, score = total };
            })
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.ad.DateCreatedOn)
            .Select(x => x.ad)
            .ToList();

            // Diversify: interleave top categories round-robin if we have 2 cats
            List<SimplifiedAdvertisementViewModel> diversified = scored;

            if (topCategoryIds.Count > 1 && scored.Count > limit)
            {
                var buckets = topCategoryIds.ToDictionary(id => id, _ => new List<SimplifiedAdvertisementViewModel>());

                var other = new List<SimplifiedAdvertisementViewModel>();

                foreach (var s in scored)
                {
                    if (buckets.ContainsKey(s.CategoryId))
                    {
                        buckets[s.CategoryId].Add(s);
                    }
                    else
                    {
                        other.Add(s);
                    }
                }

                diversified = new List<SimplifiedAdvertisementViewModel>();

                int idx = 0;

                while (diversified.Count < scored.Count)
                {
                    bool added = false;

                    foreach (var cat in topCategoryIds)
                    {
                        if (idx < buckets[cat].Count)
                        {
                            diversified.Add(buckets[cat][idx]);
                            added = true;
                        }
                    }

                    if (!added)
                    {
                        break;
                    }

                    idx++;
                }

                diversified.AddRange(other.Where(o => !diversified.Contains(o)));
            }

            var result = diversified.Take(limit).ToList();

            // Return lightweight DTO with formatted price for client card
            var payload = result.Select(a => new
            {
                id = a.Id,
                title = a.Title,
                price = PriceFormatter.ToEur(a.Price),
                priceValue = a.Price,
                imagePath = a.ImagePath,
                userId = a.UserId,
                userName = a.UserName,
                categoryId = a.CategoryId,
                category = a.Category,
                location = a.Location
            }).ToList();

            return Json(new { ads = payload });
        }

        // BuildHomeViewModel - builds the home page model with filters, sorting and paging.
        private HomeViewModel BuildHomeViewModel(int pageNumber, string filter, string? category, string? searchTerm, string? location, string? minimumPrice, string? maximumPrice)
        {
            filter = (filter ?? "new").ToLowerInvariant();

            category ??= "all";

            int? categoryId = null;

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(category, out var cid))
                {
                    categoryId = cid;
                }
                else
                {
                    var cat = _context.Categories.AsNoTracking().FirstOrDefault(c => c.Name.ToLower() == category.ToLower());

                    if (cat != null)
                    {
                        categoryId = cat.Id;
                    }
                }
            }

            IQueryable<AdvertisementModel> query = _context.Advertisements.AsNoTracking();

            query = _filterService.Apply(query, searchTerm, location, categoryId, minimumPrice, maximumPrice);

            query = _filterService.ApplySorting(query, filter);

            int loadAdsPerPage = 24;

            var countFilteredAds = query.Count();

            var maxCountPages = countFilteredAds == 0 ? 0 : (int)Math.Ceiling((double)countFilteredAds / loadAdsPerPage);

            pageNumber = Math.Clamp(pageNumber, 0, Math.Max(0, maxCountPages - 1));

            var adsResult = query
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
                    CategoryId = x.CategoryId,
                    UserName = x.User != null ? (x.User.UserName ?? "Unknown") : "Unknown",
                    UserId = x.UserId ?? "",
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
                MaxCountPages = maxCountPages,
                Filter = filter,
                Location = location,
                MinimumPrice = minimumPrice ?? "",
                MaximumPrice = maximumPrice ?? ""
            };
        }

        // Privacy - shows the privacy page.
        public IActionResult Privacy()
        {
            return View();
        }

        // Error - shows the error page with a request id.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
