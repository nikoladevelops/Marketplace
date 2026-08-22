using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    public class AdvertisementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const int SELLER_MAXIMUM_ADS = 20;
        private const int PREMIUM_MAXIMUM_ADS = 40;

        public AdvertisementController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [Authorize]
        public IActionResult Create()
        {
            if (CheckIfMaximumAdsReached()) return View("ReachedMaximumAds");

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
            if (CheckIfMaximumAdsReached()) return View("ReachedMaximumAds");

            if (!ModelState.IsValid)
            {
                viewModel.CategoryDropDown = LoadCategoryDropDown();
                return View(viewModel);
            }

            // Save main image to disk
            string mainImagePath = await Helper.SaveImageAsync(viewModel.Image, "advertisements", _webHostEnvironment);

            var advertisement = new AdvertisementModel()
            {
                ImagePath = mainImagePath,
                Title = viewModel.Title,
                Description = viewModel.Description,
                Price = viewModel.Price,
                Location = viewModel.Location,
                UserId = User.FindFirst(ClaimTypes.NameIdentifier).Value,
                CategoryId = viewModel.CategoryId,
                DateCreatedOn = DateTime.UtcNow
            };

            await _context.Advertisements.AddAsync(advertisement);
            await _context.SaveChangesAsync();

            // Collect individual additional images into an array/list for iteration
            var additionalFiles = new[] { viewModel.AdditionalImage1, viewModel.AdditionalImage2, viewModel.AdditionalImage3 };
            foreach (var img in additionalFiles)
            {
                if (img != null && img.Length > 0)
                {
                    string additionalPath = await Helper.SaveImageAsync(img, "advertisements", _webHostEnvironment);
                    var advertisementImage = new AdvertisementImageModel()
                    {
                        ImagePath = additionalPath,
                        AdvertisementId = advertisement.Id
                    };
                    await _context.AdvertisementImages.AddAsync(advertisementImage);
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
                    ExistingImagePath = x.ImagePath,
                    UserId = x.UserId
                })
                .FirstOrDefault(x => x.Id == id);

            if (ad == null || currentLoggedInUserId != ad.UserId) return NotFound();

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .ToList();

            var paths = new List<string> { "", "", "" };
            for (int i = 0; i < Math.Min(additionalImages.Count, 3); i++)
            {
                paths[i] = additionalImages[i].ImagePath;
            }

            ad.CategoryDropDown = LoadCategoryDropDown();
            ad.ExistingAdditionalImagePaths = paths;

            return View("Edit", ad);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(EditAdvertisementViewModel viewModel)
        {
            // Enforce that main image is mandatory
            if (viewModel.Image == null && string.IsNullOrEmpty(viewModel.ExistingImagePath))
            {
                ModelState.AddModelError("Image", "The main advertisement image is mandatory.");
            }

            if (!ModelState.IsValid)
            {
                viewModel.CategoryDropDown = LoadCategoryDropDown();
                viewModel.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
                return View(viewModel);
            }

            var advertisement = _context.Advertisements
                .Include(x => x.AdvertisementImages)
                .FirstOrDefault(x => x.Id == viewModel.Id);
                
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (advertisement == null || currentLoggedInUserId != advertisement.UserId) return NotFound();

            // 1. Handle Main Image
            if (viewModel.Image != null)
            {
                Helper.DeleteImage(advertisement.ImagePath, _webHostEnvironment);
                advertisement.ImagePath = await Helper.SaveImageAsync(viewModel.Image, "advertisements", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(viewModel.ExistingImagePath))
            {
                Helper.DeleteImage(advertisement.ImagePath, _webHostEnvironment);
                advertisement.ImagePath = null;
            }

            advertisement.Title = viewModel.Title;
            advertisement.Description = viewModel.Description;
            advertisement.Price = viewModel.Price;
            advertisement.Location = viewModel.Location;
            advertisement.CategoryId = viewModel.CategoryId;

            // 2. Map individual inputs slot-by-slot (Slots 0, 1, 2)
            var newFiles = new[] { viewModel.AdditionalImage1, viewModel.AdditionalImage2, viewModel.AdditionalImage3 };
            viewModel.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
            var orderedExisting = advertisement.AdvertisementImages.OrderBy(x => x.Id).ToList();

            for (int i = 0; i < 3; i++)
            {
                AdvertisementImageModel dbImage = i < orderedExisting.Count ? orderedExisting[i] : null;
                var newFile = newFiles[i];
                var existingPathTracker = i < viewModel.ExistingAdditionalImagePaths.Count ? viewModel.ExistingAdditionalImagePaths[i] : "";

                if (newFile != null && newFile.Length > 0)
                {
                    // User uploaded a new image for this slot
                    if (dbImage != null)
                    {
                        Helper.DeleteImage(dbImage.ImagePath, _webHostEnvironment);
                        dbImage.ImagePath = await Helper.SaveImageAsync(newFile, "advertisements", _webHostEnvironment);
                    }
                    else
                    {
                        string addPath = await Helper.SaveImageAsync(newFile, "advertisements", _webHostEnvironment);
                        _context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = addPath,
                            AdvertisementId = advertisement.Id
                        });
                    }
                }
                else if (string.IsNullOrEmpty(existingPathTracker))
                {
                    // User clicked delete for this slot
                    if (dbImage != null)
                    {
                        Helper.DeleteImage(dbImage.ImagePath, _webHostEnvironment);
                        _context.AdvertisementImages.Remove(dbImage);
                    }
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
            var ad = _context.Advertisements.Include(x => x.User).Include(x => x.AdvertisementImages).SingleOrDefault(x => x.Id == id);

            if (ad == null) return NotFound();

            bool isUserAdmin = User.IsInRole(Helper.AdminRole);
            if (currentLoggedInUserId != ad.UserId && !isUserAdmin) return NotFound();

            // Delete files from disk
            Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);
            foreach (var img in ad.AdvertisementImages)
            {
                Helper.DeleteImage(img.ImagePath, _webHostEnvironment);
            }

            _context.Advertisements.Remove(ad);
            await _context.SaveChangesAsync();

            if (isUserAdmin) return RedirectToAction("Profile", "Account", new { username = ad.User.UserName });
            return RedirectToAction("MyAdvertisements", "Account");
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
                    ImagePath = x.ImagePath,
                    UserId = x.UserId,
                    DateCreatedOn = x.DateCreatedOn.ToShortDateString(),
                    CategoryName = x.CategoryId.ToString()
                })
                .FirstOrDefault(x => x.Id == id);

            if (ad == null) return NotFound();

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => x.ImagePath)
                .ToList();

            var adOwnerData = _context.Users
                .Where(x => x.Id == ad.UserId)
                .Select(x => new { x.UserName, x.ProfilePicturePath, x.Email, x.PhoneNumber })
                .Single();

            ad.CategoryName = GetCategoryName(int.Parse(ad.CategoryName));
            ad.AdditionalImagePaths = additionalImages;
            ad.UserName = adOwnerData.UserName;
            ad.ProfilePicturePath = adOwnerData.ProfilePicturePath;
            ad.Email = adOwnerData.Email;
            ad.PhoneNumber = adOwnerData.PhoneNumber;

            return View("Show", ad);
        }

        private IEnumerable<SelectListItem> LoadCategoryDropDown() =>
            _context.Categories.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();

        private string GetCategoryName(int categoryId) =>
            _context.Categories.Where(x => x.Id == categoryId).Select(x => x.Name).Single();

        private bool CheckIfMaximumAdsReached()
        {
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int count = _context.Advertisements.Count(x => x.UserId == currentLoggedInUserId);
            int max = User.IsInRole(Helper.PremiumRole) ? PREMIUM_MAXIMUM_ADS : SELLER_MAXIMUM_ADS;
            return count >= max;
        }
    }
}