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
    // AdvertisementService - does CRUD for ads, images and listing pages.
    public class AdvertisementService
    {
        public const int SellerMaxAds = 20;
        public const int PremiumMaxAds = 40;
        private const int MaxAdditionalImages = 3;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // AdvertisementService - set up DB and file hosting.
        public AdvertisementService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // LoadCategoryDropDown - get categories for the create/edit dropdown.
        public IEnumerable<SelectListItem> LoadCategoryDropDown() =>
            _context.Categories
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();

        // HasReachedMaxAds - checks if user hit their ad limit.
        public bool HasReachedMaxAds(string userId, bool isPremium)
        {
            var count = _context.Advertisements.Count(x => x.UserId == userId);
            var max = isPremium ? PremiumMaxAds : SellerMaxAds;

            return count >= max;
        }

        // PrepareCreateViewModelAsync - gets an empty create form or null if limit reached.
        public async Task<CreateAdvertisementViewModel?> PrepareCreateViewModelAsync(string userId, bool isPremium)
        {
            if (HasReachedMaxAds(userId, isPremium))
            {
                return null;
            }

            return new CreateAdvertisementViewModel
            {
                CategoryDropDown = LoadCategoryDropDown()
            };
        }

        public enum CreateValidation
        {
            Ok,
            CategoryInvalid,
            MaxAdsReached,
            ImageMissing
        }

        // CreateAsync - makes a new ad after checking limits and saving images.
        public async Task<(CreateValidation Result, CreateAdvertisementViewModel? ViewModel)> CreateAsync(
            CreateAdvertisementViewModel viewModel,
            string ownerId,
            bool isPremium,
            CancellationToken cancellationToken)
        {
            if (HasReachedMaxAds(ownerId, isPremium))
            {
                return (CreateValidation.MaxAdsReached, null);
            }

            if (viewModel.CategoryId != -1 && !await _context.Categories.AnyAsync(c => c.Id == viewModel.CategoryId, cancellationToken))
            {
                return (CreateValidation.CategoryInvalid, viewModel);
            }

            // Main image is required - accept either a fresh file or the Base64 preview carried over after an error.
            bool hasMainFile = viewModel.Image != null && viewModel.Image.Length > 0;
            bool hasMainBase64 = !string.IsNullOrWhiteSpace(viewModel.MainImageBase64);

            if (!hasMainFile && !hasMainBase64)
            {
                return (CreateValidation.ImageMissing, viewModel);
            }

            // Save main image first. Prefer the real file, fall back to Base64.

            string? mainImagePath = null;

            if (hasMainFile)
            {
                mainImagePath = await Helper.SaveImageAsync(viewModel.Image, "advertisements", _webHostEnvironment);
            }
            else
            {
                mainImagePath = await Helper.SaveBase64ImageAsync(viewModel.MainImageBase64, viewModel.MainImageFileName, "advertisements", _webHostEnvironment);
            }

            if (string.IsNullOrEmpty(mainImagePath))
            {
                return (CreateValidation.ImageMissing, viewModel);
            }

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

            // Save up to three extra images - each slot can be a fresh file or a Base64 fallback.

            var additionalFiles = new[] { viewModel.AdditionalImage1, viewModel.AdditionalImage2, viewModel.AdditionalImage3 };
            var additionalBase64 = new[] { viewModel.AdditionalImageBase64_1, viewModel.AdditionalImageBase64_2, viewModel.AdditionalImageBase64_3 };
            var additionalNames = new[] { viewModel.AdditionalImageFileName1, viewModel.AdditionalImageFileName2, viewModel.AdditionalImageFileName3 };

            for (int i = 0; i < additionalFiles.Length; i++)
            {
                var file = additionalFiles[i];

                if (file != null && file.Length > 0)
                {
                    var additionalPath = await Helper.SaveImageAsync(file, "advertisements", _webHostEnvironment);

                    if (!string.IsNullOrEmpty(additionalPath))
                    {
                        _context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = additionalPath,
                            AdvertisementId = ad.Id
                        });
                    }

                    continue;
                }

                var b64 = additionalBase64[i];

                if (!string.IsNullOrWhiteSpace(b64))
                {
                    var base64Path = await Helper.SaveBase64ImageAsync(b64, additionalNames[i], "advertisements", _webHostEnvironment);

                    if (!string.IsNullOrEmpty(base64Path))
                    {
                        _context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = base64Path,
                            AdvertisementId = ad.Id
                        });
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return (CreateValidation.Ok, null);
        }

        // PrepareEditViewModelAsync - loads existing ad for editing if owner or admin.
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

            if (ad == null || (requesterId != ad.UserId && !isAdmin))
            {
                return null;
            }

            var additionalImages = await _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .OrderBy(x => x.Id)
                .Select(x => x.ImagePath)
                .Take(MaxAdditionalImages)
                .ToListAsync();

            var paths = new List<string> { "", "", "" };

            for (var i = 0; i < additionalImages.Count; i++)
            {
                paths[i] = additionalImages[i];
            }

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

        // UpdateAsync - updates an ad and handles image add, replace or remove per slot.
        public async Task<EditValidation> UpdateAsync(
            EditAdvertisementViewModel model,
            string requesterId,
            bool isAdmin,
            CancellationToken cancellationToken)
        {
            bool hasNewMainFile = model.Image != null && model.Image.Length > 0;
            bool hasNewMainBase64 = !string.IsNullOrWhiteSpace(model.MainImageBase64);
            bool hasExistingMain = !string.IsNullOrEmpty(model.ExistingImagePath);

            if (!hasNewMainFile && !hasNewMainBase64 && !hasExistingMain)
            {
                return EditValidation.ImageMissing;
            }

            if (model.CategoryId != -1 && !await _context.Categories.AnyAsync(c => c.Id == model.CategoryId, cancellationToken))
            {
                return EditValidation.CategoryInvalid;
            }

            var ad = await _context.Advertisements
                .Include(x => x.AdvertisementImages)
                .FirstOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

            if (ad == null || (requesterId != ad.UserId && !isAdmin))
            {
                return EditValidation.CategoryInvalid;
            }

            // 1. Handle main image. Prefer a fresh file, then Base64, otherwise keep or clear.

            if (hasNewMainFile)
            {
                Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);

                ad.ImagePath = await Helper.SaveImageAsync(model.Image, "advertisements", _webHostEnvironment);
            }
            else if (hasNewMainBase64)
            {
                Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);

                ad.ImagePath = await Helper.SaveBase64ImageAsync(model.MainImageBase64, model.MainImageFileName, "advertisements", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(model.ExistingImagePath))
            {
                Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);

                ad.ImagePath = null;
            }

            // Update text fields and coords.

            ad.Title = model.Title;
            ad.Description = model.Description;
            ad.Price = model.Price;
            ad.Location = model.Location;
            ad.Latitude = SanitizeCoordinate(model.Latitude, -90, 90);
            ad.Longitude = SanitizeCoordinate(model.Longitude, -180, 180);
            ad.CategoryId = model.CategoryId;

            // 2. Handle additional images slot by slot. Each slot can be a new file, a Base64 fallback, or cleared.

            var newFiles = new[] { model.AdditionalImage1, model.AdditionalImage2, model.AdditionalImage3 };
            var newBase64 = new[] { model.AdditionalImageBase64_1, model.AdditionalImageBase64_2, model.AdditionalImageBase64_3 };
            var newNames = new[] { model.AdditionalImageFileName1, model.AdditionalImageFileName2, model.AdditionalImageFileName3 };

            if (model.ExistingAdditionalImagePaths == null)
            {
                model.ExistingAdditionalImagePaths = new List<string> { "", "", "" };
            }

            var orderedExisting = ad.AdvertisementImages.OrderBy(x => x.Id).ToList();

            for (var i = 0; i < MaxAdditionalImages; i++)
            {
                var dbImage = i < orderedExisting.Count ? orderedExisting[i] : null;
                var newFile = newFiles[i];
                var b64 = newBase64[i];
                var existingPathTracker = i < model.ExistingAdditionalImagePaths.Count ? model.ExistingAdditionalImagePaths[i] : "";

                bool hasFile = newFile != null && newFile.Length > 0;
                bool hasB64 = !string.IsNullOrWhiteSpace(b64);

                if (hasFile)
                {
                    if (dbImage != null)
                    {
                        Helper.DeleteImage(dbImage.ImagePath, _webHostEnvironment);

                        dbImage.ImagePath = await Helper.SaveImageAsync(newFile, "advertisements", _webHostEnvironment);
                    }
                    else
                    {
                        var addPath = await Helper.SaveImageAsync(newFile, "advertisements", _webHostEnvironment);

                        if (!string.IsNullOrEmpty(addPath))
                        {
                            _context.AdvertisementImages.Add(new AdvertisementImageModel
                            {
                                ImagePath = addPath,
                                AdvertisementId = ad.Id
                            });
                        }
                    }
                }
                else if (hasB64)
                {
                    var b64Path = await Helper.SaveBase64ImageAsync(b64, newNames[i], "advertisements", _webHostEnvironment);

                    if (string.IsNullOrEmpty(b64Path))
                    {
                        continue;
                    }

                    if (dbImage != null)
                    {
                        Helper.DeleteImage(dbImage.ImagePath, _webHostEnvironment);

                        dbImage.ImagePath = b64Path;
                    }
                    else
                    {
                        _context.AdvertisementImages.Add(new AdvertisementImageModel
                        {
                            ImagePath = b64Path,
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

        // DeleteAsync - removes ad and its images if owner or admin.
        public async Task<DeleteResult> DeleteAsync(int id, string requesterId, bool isAdmin, CancellationToken cancellationToken)
        {
            var ad = await _context.Advertisements
                .Include(x => x.User)
                .Include(x => x.AdvertisementImages)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (ad == null)
            {
                return new DeleteResult(DeleteOutcome.NotFound, null);
            }

            if (requesterId != ad.UserId && !isAdmin)
            {
                return new DeleteResult(DeleteOutcome.NotFound, null);
            }

            Helper.DeleteImage(ad.ImagePath, _webHostEnvironment);

            foreach (var img in ad.AdvertisementImages)
            {
                Helper.DeleteImage(img.ImagePath, _webHostEnvironment);
            }

            _context.Advertisements.Remove(ad);

            await _context.SaveChangesAsync(cancellationToken);

            return new DeleteResult(DeleteOutcome.Deleted, ad.User.UserName);
        }

        public enum DeleteOutcome { Deleted, NotFound }
        public record DeleteResult(DeleteOutcome Outcome, string? OwnerUsername);

        // GetShowViewModelAsync - builds the detail page with contact visibility.
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

            if (ad == null)
            {
                return null;
            }

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

        // SanitizeCoordinate - keeps coords in range or returns null if bad.
        private static double? SanitizeCoordinate(double? value, double min, double max)
        {
            if (value == null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                return null;
            }

            return value.Value >= min && value.Value <= max ? value.Value : null;
        }
    }
}
