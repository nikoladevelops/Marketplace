using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Marketplace.Services
{
    public class AdvertisementService
    {
        public const int SellerMaxAds = 20;
        public const int PremiumMaxAds = 40;
        private const int MaxAdditionalImages = 3;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdvertisementService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IEnumerable<SelectListItem> LoadCategoryDropDown() =>
            _context.Categories
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();

        public bool HasReachedMaxAds(string userId, bool isPremium)
        {
            var count = _context.Advertisements.Count(x => x.UserId == userId);
            var max = isPremium ? PremiumMaxAds : SellerMaxAds;
            return count >= max;
        }

        public async Task<CreateAdvertisementViewModel?> PrepareCreateViewModelAsync(string userId, bool isPremium)
        {
            if (HasReachedMaxAds(userId, isPremium)) return null;
            return new CreateAdvertisementViewModel
            {
                CategoryDropDown = LoadCategoryDropDown()
            };
        }

        public enum CreateValidation
        {
            Ok,
            CategoryInvalid,
            MaxAdsReached
        }

        public async Task<(CreateValidation Result, CreateAdvertisementViewModel? ViewModel)> CreateAsync(
            CreateAdvertisementViewModel viewModel,
            string ownerId,
            bool isPremium,
            CancellationToken cancellationToken)
        {
            if (HasReachedMaxAds(ownerId, isPremium)) return (CreateValidation.MaxAdsReached, null);

            if (viewModel.CategoryId != -1 && !await _context.Categories.AnyAsync(c => c.Id == viewModel.CategoryId, cancellationToken))
            {
                return (CreateValidation.CategoryInvalid, viewModel);
            }

            var mainImagePath = await Helper.SaveImageAsync(viewModel.Image, "advertisements", _webHostEnvironment);

            var ad = new AdvertisementModel
            {
                ImagePath = mainImagePath,
                Title = viewModel.Title,
                Description = viewModel.Description,
                Price = viewModel.Price,
                Location = viewModel.Location,
                Latitude = SanitizeCoordinate(viewModel.Latitude, -90, 90),
                Longitude = SanitizeCoordinate(viewModel.Longitude, -180, 180),
                UserId = ownerId,
                CategoryId = viewModel.CategoryId,
                DateCreatedOn = DateTime.UtcNow
            };

            await _context.Advertisements.AddAsync(ad, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var additionalFiles = new[] { viewModel.AdditionalImage1, viewModel.AdditionalImage2, viewModel.AdditionalImage3 };
            foreach (var img in additionalFiles)
            {
                if (img != null && img.Length > 0)
                {
                    var additionalPath = await Helper.SaveImageAsync(img, "advertisements", _webHostEnvironment);
                    _context.AdvertisementImages.Add(new AdvertisementImageModel
                    {
                        ImagePath = additionalPath,
                        AdvertisementId = ad.Id
                    });
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            return (CreateValidation.Ok, null);
        }

        public async Task<EditAdvertisementViewModel?> PrepareEditViewModelAsync(int id, string requesterId, bool isAdmin)
        {
            var ad = await _context.Advertisements
                .Where(x => x.Id == id)
                .Select(x => new EditAdvertisementViewModel
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
                .FirstOrDefaultAsync();

            if (ad == null || (requesterId != ad.UserId && !isAdmin)) return null;

            var additionalImages = await _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .OrderBy(x => x.Id)
                .Select(x => x.ImagePath)
                .Take(MaxAdditionalImages)
                .ToListAsync();

            var paths = new List<string> { "", "", "" };
            for (var i = 0; i < additionalImages.Count; i++) paths[i] = additionalImages[i];

            ad.CategoryDropDown = LoadCategoryDropDown();
            ad.ExistingAdditionalImagePaths = paths;
            return ad;
        }

        public enum EditValidation
        {
            Ok,
            ImageMissing,
            CategoryInvalid
        }

        public async Task<EditValidation> UpdateAsync(
            EditAdvertisementViewModel model,
            string requesterId,
            bool isAdmin,
            CancellationToken cancellationToken)
        {
            if (model.Image == null && string.IsNullOrEmpty(model.ExistingImagePath))
                return EditValidation.ImageMissing;

            if (model.CategoryId != -1 && !await _context.Categories.AnyAsync(c => c.Id == model.CategoryId, cancellationToken))
                return EditValidation.CategoryInvalid;

            var ad = await _context.Advertisements
                .Include(x => x.AdvertisementImages)
                .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

            if (ad == null || (requesterId != ad.UserId && !isAdmin)) return EditValidation.CategoryInvalid; // treat as invalid

            // 1. Main image
            if (model.Image != null)
            {
                Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);
                ad.ImagePath = await Helper.SaveImageAsync(model.Image, "advertisements", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(model.ExistingImagePath))
            {
                Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);
                ad.ImagePath = null;
            }

            ad.Title = model.Title;
            ad.Description = model.Description;
            ad.Price = model.Price;
            ad.Location = model.Location;
            ad.Latitude = SanitizeCoordinate(model.Latitude, -90, 90);
            ad.Longitude = SanitizeCoordinate(model.Longitude, -180, 180);
            ad.CategoryId = model.CategoryId;

            // 2. Slot-by-slot additional images
            var newFiles = new[] { model.AdditionalImage1, model.AdditionalImage2, model.AdditionalImage3 };
            model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
            var orderedExisting = ad.AdvertisementImages.OrderBy(x => x.Id).ToList();

            for (var i = 0; i < MaxAdditionalImages; i++)
            {
                var dbImage = i < orderedExisting.Count ? orderedExisting[i] : null;
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
                        var addPath = await Helper.SaveImageAsync(newFile, "advertisements", _webHostEnvironment);
                        _context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = addPath,
                            AdvertisementId = ad.Id
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
            return EditValidation.Ok;
        }

        public async Task<DeleteResult> DeleteAsync(int id, string requesterId, bool isAdmin, CancellationToken cancellationToken)
        {
            var ad = await _context.Advertisements
                .Include(x => x.User)
                .Include(x => x.AdvertisementImages)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (ad == null) return new DeleteResult(DeleteOutcome.NotFound, null);
            if (requesterId != ad.UserId && !isAdmin) return new DeleteResult(DeleteOutcome.NotFound, null);

            Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);
            foreach (var img in ad.AdvertisementImages)
                Helper.DeleteImage(img.ImagePath, _webHostEnvironment);

            _context.Advertisements.Remove(ad);
            await _context.SaveChangesAsync(cancellationToken);

            return new DeleteResult(DeleteOutcome.Deleted, ad.User.UserName);
        }

        public enum DeleteOutcome { Deleted, NotFound }
        public record DeleteResult(DeleteOutcome Outcome, string? OwnerUsername);

        public async Task<ShowAdvertisementViewModel?> GetShowViewModelAsync(int id, ClaimsPrincipal viewer)
        {
            var ad = await _context.Advertisements
                .Where(x => x.Id == id)
                .Select(x => new ShowAdvertisementViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Price = PriceFormatter.ToEur(x.Price),
                    Location = x.Location,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    ImagePath = x.ImagePath,
                    UserId = x.UserId,
                    DateCreatedOn = x.DateCreatedOn,
                    CategoryName = x.CategoryId.ToString()
                })
                .FirstOrDefaultAsync();

            if (ad == null) return null;

            var additionalImages = await _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => x.ImagePath)
                .ToListAsync();

            var adOwnerData = await _context.Users
                .Where(x => x.Id == ad.UserId)
                .Select(x => new { x.UserName, x.ProfilePicturePath, x.Email, x.PhoneNumber, x.ShowEmail, x.ShowPhone })
                .SingleAsync();

            ad.CategoryName = await _context.Categories
                .Where(x => x.Id == int.Parse(ad.CategoryName))
                .Select(x => x.Name)
                .SingleAsync();
            ad.AdditionalImagePaths = additionalImages;
            ad.UserName = adOwnerData.UserName ?? "";
            ad.ProfilePicturePath = adOwnerData.ProfilePicturePath;
            ad.Email = adOwnerData.Email ?? "";
            ad.PhoneNumber = adOwnerData.PhoneNumber;

            var ownerForVisibility = new ApplicationUser
            {
                Id = ad.UserId,
                Email = adOwnerData.Email,
                PhoneNumber = adOwnerData.PhoneNumber,
                ShowEmail = adOwnerData.ShowEmail,
                ShowPhone = adOwnerData.ShowPhone
            };
            var phoneView = ContactVisibilityHelper.ResolvePhone(ownerForVisibility, viewer);
            var emailView = ContactVisibilityHelper.ResolveEmail(ownerForVisibility, viewer);
            ad.DisplayPhone = phoneView.Display;
            ad.CanViewPhone = phoneView.CanView;
            ad.IsCensoredPhone = phoneView.IsCensored;
            ad.DisplayEmail = emailView.Display;
            ad.CanViewEmail = emailView.CanView;
            ad.IsCensoredEmail = emailView.IsCensored;
            ad.ViewerIsAuthenticated = viewer.Identity?.IsAuthenticated == true;
            ad.IsOwner = ad.UserId == viewer.FindFirstValue(ClaimTypes.NameIdentifier);
            ad.IsAdmin = viewer.IsInRole(Helper.AdminRole);

            return ad;
        }

        private static double? SanitizeCoordinate(double? value, double min, double max)
        {
            if (value == null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
            return value.Value >= min && value.Value <= max ? value.Value : null;
        }
    }
}
