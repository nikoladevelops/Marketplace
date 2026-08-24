using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.Controllers
{
    [Authorize]
    public class AdvertisementController : Controller
    {
        private readonly IAiService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const int SELLER_MAXIMUM_ADS = 20;
        private const int PREMIUM_MAXIMUM_ADS = 40;

        public AdvertisementController(IAiService aiService, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _aiService = aiService;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
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
        public async Task<IActionResult> Create(CreateAdvertisementViewModel viewModel, CancellationToken cancellationToken)
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
                Latitude = SanitizeCoordinate(viewModel.Latitude, -90, 90),
                Longitude = SanitizeCoordinate(viewModel.Longitude, -180, 180),
                UserId = User.FindFirst(ClaimTypes.NameIdentifier).Value,
                CategoryId = viewModel.CategoryId,
                DateCreatedOn = DateTime.UtcNow
            };

            await _context.Advertisements.AddAsync(advertisement, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Collect individual additional images into an array for slot-by-slot iteration
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
                    await _context.AdvertisementImages.AddAsync(advertisementImage, cancellationToken);
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            return RedirectToAction("MyAdvertisements", "Account");
        }

        [HttpGet]
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
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
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
        public async Task<IActionResult> Edit(EditAdvertisementViewModel model, CancellationToken cancellationToken)
        {
            if (model.Image == null && string.IsNullOrEmpty(model.ExistingImagePath))
            {
                ModelState.AddModelError("Image", "The main advertisement image is mandatory.");
            }

            if (!ModelState.IsValid)
            {
                model.CategoryDropDown = LoadCategoryDropDown();
                model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
                return View(model);
            }

            var advertisement = await _context.Advertisements
                .Include(x => x.AdvertisementImages)
                .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (advertisement == null || currentLoggedInUserId != advertisement.UserId) return NotFound();

            // 1. Handle Main Image update
            if (model.Image != null)
            {
                Helper.DeleteImage(advertisement.ImagePath, _webHostEnvironment);
                advertisement.ImagePath = await Helper.SaveImageAsync(model.Image, "advertisements", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(model.ExistingImagePath))
            {
                Helper.DeleteImage(advertisement.ImagePath, _webHostEnvironment);
                advertisement.ImagePath = null;
            }

            advertisement.Title = model.Title;
            advertisement.Description = model.Description;
            advertisement.Price = model.Price;
            advertisement.Location = model.Location;
            advertisement.Latitude = SanitizeCoordinate(model.Latitude, -90, 90);
            advertisement.Longitude = SanitizeCoordinate(model.Longitude, -180, 180);
            advertisement.CategoryId = model.CategoryId;

            // 2. Map slot-by-slot additional images (Slots 0, 1, 2)
            var newFiles = new[] { model.AdditionalImage1, model.AdditionalImage2, model.AdditionalImage3 };
            model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
            var orderedExisting = advertisement.AdvertisementImages.OrderBy(x => x.Id).ToList();

            for (int i = 0; i < 3; i++)
            {
                AdvertisementImageModel dbImage = i < orderedExisting.Count ? orderedExisting[i] : null;
                var newFile = newFiles[i];
                var existingPathTracker = i < model.ExistingAdditionalImagePaths.Count ? model.ExistingAdditionalImagePaths[i] : "";

                if (newFile != null && newFile.Length > 0)
                {
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
                    if (dbImage != null)
                    {
                        Helper.DeleteImage(dbImage.ImagePath, _webHostEnvironment);
                        _context.AdvertisementImages.Remove(dbImage);
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction("MyAdvertisements", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ad = await _context.Advertisements
                .Include(x => x.User)
                .Include(x => x.AdvertisementImages)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (ad == null) return NotFound();

            bool isUserAdmin = User.IsInRole(Helper.AdminRole);
            if (currentLoggedInUserId != ad.UserId && !isUserAdmin) return NotFound();

            Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);
            foreach (var img in ad.AdvertisementImages)
            {
                Helper.DeleteImage(img.ImagePath, _webHostEnvironment);
            }

            _context.Advertisements.Remove(ad);
            await _context.SaveChangesAsync(cancellationToken);

            if (isUserAdmin) return RedirectToAction("Profile", "Account", new { username = ad.User.UserName });
            return RedirectToAction("MyAdvertisements", "Account");
        }

        [AllowAnonymous]
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
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GenerateListingAI(List<IFormFile> images, CancellationToken cancellationToken)
        {
            if (images == null || !images.Any(f => f != null && f.Length > 0))
            {
                return Json(new { success = false, message = "No valid images provided." });
            }

            var result = await _aiService.GenerateListingFromImagesAsync(images, cancellationToken);

            if (result == null)
            {
                return Json(new { success = false, message = "AI generation failed or LM Studio is unresponsive." });
            }

            return Json(new { success = true, data = result });
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

        private static double? SanitizeCoordinate(double? value, double min, double max)
        {
            if (value == null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
            return value.Value >= min && value.Value <= max ? value.Value : null;
        }
    }
}