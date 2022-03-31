using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    public class AdvertisementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int SELLER_MAXIMUM_ADS = 20;
        private const int PREMIUM_MAXIMUM_ADS = 40;
        public AdvertisementController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public IActionResult Create()
        {
            if (CheckIfMaximumAdsReached()) 
            {
                return View("ReachedMaximumAds");
            }

            var viewModel = new CreateAdvertisementViewModel()
            {
                CategoryDropDown = LoadCategoryDropDown()
            };
            
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(CreateAdvertisementViewModel viewModel)
        {
            if (CheckIfMaximumAdsReached())
            {
                return View("ReachedMaximumAds");
            }

            if (!ModelState.IsValid) 
            {
                viewModel.CategoryDropDown = LoadCategoryDropDown();

                return View(viewModel);
            }

            var advertisement = new AdvertisementModel()
            {
                ImageData = await Helper.GetByteArrayFromImage(viewModel.Image),
                Title = viewModel.Title,
                Description = viewModel.Description,
                Price = viewModel.Price,
                Location=viewModel.Location,
                UserId = User.FindFirst(ClaimTypes.NameIdentifier).Value,
                CategoryId=viewModel.CategoryId,
                DateCreatedOn=DateTime.UtcNow
            };

            await _context.Advertisements.AddAsync(advertisement);
            await _context.SaveChangesAsync();

            if (viewModel.AdditionalImages!=null)
            {
                foreach (var img in viewModel.AdditionalImages)
                {
                    var advertisementImages = new AdvertisementImageModel()
                    {
                        ImageData = await Helper.GetByteArrayFromImage(img),
                        AdvertisementId = advertisement.Id
                    };

                    await _context.AdvertisementImages.AddAsync(advertisementImages);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyAdvertisements", "Account");
        }

        [Authorize]
        public IActionResult Edit(int id)
        {
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var ad = _context.Advertisements
                .Select(x => new EditAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Price = x.Price,
                    Location = x.Location,
                    CategoryId = x.CategoryId,
                    ImageInBytes = x.ImageData,
                    UserId = x.UserId
                })
                .FirstOrDefault(x => x.Id == id);

            if (ad == null || currentLoggedInUserId != ad.UserId)
            {
                return NotFound();
            }

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => x.ImageData)
                .ToList();

            ad.CategoryDropDown = LoadCategoryDropDown();
            ad.AdditionalImagesInBytes = additionalImages;


            return View("Edit", ad);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(EditAdvertisementViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                viewModel.CategoryDropDown = LoadCategoryDropDown();
                return View(viewModel);
            }

            var advertisement = _context.Advertisements.FirstOrDefault(x => x.Id == viewModel.Id);
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (advertisement == null || currentLoggedInUserId != advertisement.UserId)
            {
                return NotFound();
            }

            advertisement.ImageData = await Helper.GetByteArrayFromImage(viewModel.Image);
            advertisement.Title = viewModel.Title;
            advertisement.Description = viewModel.Description;
            advertisement.Price = viewModel.Price;
            advertisement.Location = viewModel.Location;
            advertisement.CategoryId = viewModel.CategoryId;

            await _context.SaveChangesAsync();

            var imgs = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == advertisement.Id)
                .ToList();

            _context.AdvertisementImages.RemoveRange(imgs);

            if (viewModel.AdditionalImages != null)
            {
                foreach (var img in viewModel.AdditionalImages)
                {
                    var advertisementImage = new AdvertisementImageModel()
                    {
                        ImageData = await Helper.GetByteArrayFromImage(img),
                        AdvertisementId = advertisement.Id
                    };
                    
                    await _context.AdvertisementImages.AddAsync(advertisementImage);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyAdvertisements", "Account");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var ad = _context.Advertisements
                .Include(x=>x.User)
                .Where(x=>x.Id==id)
                .SingleOrDefault();

            if (ad == null)
            {
                return NotFound();
            }

            var isUserAdmin = User.IsInRole(Helper.AdminRole);
            var adOwnerUsername = ad.User.UserName;

            // if the user does not own the advertisement AND he is also NOT an admin
            if (currentLoggedInUserId != ad.UserId && !(isUserAdmin))
            {
                return NotFound();
            }

            _context.Advertisements.Remove(ad);
            await _context.SaveChangesAsync();

            if (isUserAdmin)
            {
                return RedirectToAction("Profile", "Account", new { username = adOwnerUsername });
            }

            return RedirectToAction("MyAdvertisements","Account");
        }

        public IActionResult Show(int id)
        {
            var ad = _context.Advertisements
                .Select(x => new ShowAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Price = x.Price + " EUR",
                    Location = x.Location,
                    ImageInBytes = x.ImageData,
                    UserId = x.UserId,
                    DateCreatedOn=x.DateCreatedOn.ToShortDateString(),
                    CategoryName=x.CategoryId.ToString()
                })
                .FirstOrDefault(x => x.Id == id);

            if (ad == null)
            {
                return NotFound();
            }

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => x.ImageData)
                .ToList();

            var adOwnerData = _context.Users
                .Where(x => x.Id == ad.UserId)
                .Select(x => new 
                { 
                    UserName = x.UserName,
                    ProfilePicture = x.ProfilePicture ,
                    Email=x.Email,
                    PhoneNumber = x.PhoneNumber
                })
                .Single();


            ad.CategoryName = GetCategoryName(int.Parse(ad.CategoryName));
            ad.AdditionalImagesInBytes = additionalImages;
            ad.UserName = adOwnerData.UserName;
            ad.ProfilePicture=adOwnerData.ProfilePicture;
            ad.Email = adOwnerData.Email;
            ad.PhoneNumber = adOwnerData.PhoneNumber;

            return View("Show", ad);
        }

        private IEnumerable<SelectListItem> LoadCategoryDropDown() 
        {
            return _context.Categories
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();
        }

        private string GetCategoryName(int categoryId) 
        {
            var categoryName = _context.Categories
                .Where(x=>x.Id==categoryId)
                .Select(x=>x.Name)
                .Single();

            return categoryName;
        }

        private bool CheckIfMaximumAdsReached() 
        {
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var currentUserAdsCount = _context.Advertisements
                .Where(x => x.UserId == currentLoggedInUserId)
                .Count();

            var maxAdsAllowed = SELLER_MAXIMUM_ADS;

            if (User.IsInRole(Helper.PremiumRole))
            {
                maxAdsAllowed = PREMIUM_MAXIMUM_ADS;
            }

            if (currentUserAdsCount >= maxAdsAllowed) // using >= instead of == because user's premium status can be removed and he can be left with more advertisements compared to normal seller accounts.
            {
                return true;
            }
            return false;
        }

    }
}
