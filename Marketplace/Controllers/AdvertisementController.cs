using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
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
    // AdvertisementController - handles creating, editing, deleting and viewing ads.
    // Most actions need login, only Show is public.
    [Authorize]
    public class AdvertisementController : Controller
    {
        private readonly IAiService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly AdvertisementService _ads;

        // Constructor - wires up AI, database and ad helpers.
        public AdvertisementController(IAiService aiService, ApplicationDbContext context, AdvertisementService ads)
        {
            _aiService = aiService;
            _context = context;
            _ads = ads;
        }

        // Create (GET) - shows the create ad form, checks if you hit your ad limit.
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (meId == null)
            {
                return Challenge();
            }

            var isPremium = User.IsInRole(Helper.PremiumRole);

            var vm = await _ads.PrepareCreateViewModelAsync(meId, isPremium);

            if (vm == null)
            {
                return View("ReachedMaximumAds");
            }

            return View(vm);
        }

        // Create (POST) - saves a new ad. Validates first so we do not lose images or text on error.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAdvertisementViewModel viewModel, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User not authenticated");

            var isPremium = User.IsInRole(Helper.PremiumRole);

            // Run client and server validation before touching the database or disk.
            // This keeps the form state intact, including the Base64 previews.
            // If the user picked files but the hidden Base64 did not get set yet (FileReader is async),
            // we fill it here from the posted files so the preview survives.
            if (!ModelState.IsValid)
            {
                await PreserveImagesForReRenderAsync(viewModel);
                ClearImageBase64ModelState();
                viewModel.CategoryDropDown = _ads.LoadCategoryDropDown();
                return View(viewModel);
            }

            var (result, returnedVm) = await _ads.CreateAsync(viewModel, meId, isPremium, cancellationToken);

            if (result == AdvertisementService.CreateValidation.MaxAdsReached)
            {
                return View("ReachedMaximumAds");
            }

            if (result == AdvertisementService.CreateValidation.CategoryInvalid)
            {
                await PreserveImagesForReRenderAsync(viewModel);

                returnedVm!.CategoryDropDown = _ads.LoadCategoryDropDown();

                // Copy the Base64 previews over so the returned VM shows them.
                // Use the same data URL format as a freshly picked file, so the next submit treats it as a new upload.
                if (returnedVm != null)
                {
                    returnedVm.MainImageBase64 = viewModel.MainImageBase64;
                    returnedVm.MainImageFileName = viewModel.MainImageFileName;
                    returnedVm.AdditionalImageBase64_1 = viewModel.AdditionalImageBase64_1;
                    returnedVm.AdditionalImageBase64_2 = viewModel.AdditionalImageBase64_2;
                    returnedVm.AdditionalImageBase64_3 = viewModel.AdditionalImageBase64_3;
                    returnedVm.AdditionalImageFileName1 = viewModel.AdditionalImageFileName1;
                    returnedVm.AdditionalImageFileName2 = viewModel.AdditionalImageFileName2;
                    returnedVm.AdditionalImageFileName3 = viewModel.AdditionalImageFileName3;
                }

                // View helpers prefer ModelState over the ViewModel, so clear the old empty entries.
                ClearImageBase64ModelState();

                ModelState.AddModelError(nameof(viewModel.CategoryId), "You need to select a category.");

                return View(returnedVm);
            }

            if (result == AdvertisementService.CreateValidation.ImageMissing)
            {
                await PreserveImagesForReRenderAsync(viewModel);
                ClearImageBase64ModelState();
                viewModel.CategoryDropDown = _ads.LoadCategoryDropDown();

                ModelState.AddModelError("Image", "The main advertisement image is mandatory.");

                return View(viewModel);
            }

            return RedirectToAction("Profile", "Account", new { username = User.Identity?.Name ?? "me" });
        }

        // Edit (GET) - loads an ad for editing, only owner or admin can open it.
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var isAdmin = User.IsInRole(Helper.AdminRole);

            var vm = await _ads.PrepareEditViewModelAsync(id, meId, isAdmin);

            if (vm == null)
            {
                return NotFound();
            }

            return View("Edit", vm);
        }

        // Edit (POST) - saves changes to an ad. Keeps your typed data and image previews if validation fails.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditAdvertisementViewModel model, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var isAdmin = User.IsInRole(Helper.AdminRole);

            // Check data annotations first (title length, price range, etc.) before hitting the DB.
            if (!ModelState.IsValid)
            {
                await PreserveImagesForReRenderAsync(model);
                ClearImageBase64ModelState();
                model.CategoryDropDown = _ads.LoadCategoryDropDown();

                model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };

                return View(model);
            }

            var result = await _ads.UpdateAsync(model, meId, isAdmin, cancellationToken);

            if (result == AdvertisementService.EditValidation.ImageMissing)
            {
                ModelState.AddModelError("Image", "The main advertisement image is mandatory.");
            }

            if (result == AdvertisementService.EditValidation.CategoryInvalid)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "You need to select a category.");
            }

            if (result != AdvertisementService.EditValidation.Ok)
            {
                await PreserveImagesForReRenderAsync(model);
                ClearImageBase64ModelState();
                model.CategoryDropDown = _ads.LoadCategoryDropDown();

                model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };

                return View(model);
            }

            return RedirectToAction("Profile", "Account", new { username = User.Identity?.Name ?? "me" });
        }

        // PreserveImagesForReRenderAsync (Create)
        // If the user picked files but the hidden Base64 was not set yet (race, JS disabled),
        // we convert the posted files here so the preview survives when we return the view with errors.
        private async Task PreserveImagesForReRenderAsync(CreateAdvertisementViewModel vm)
        {
            if ((vm.Image != null && vm.Image.Length > 0) && string.IsNullOrWhiteSpace(vm.MainImageBase64))
            {
                vm.MainImageBase64 = await ToDataUrlAsync(vm.Image);
                vm.MainImageFileName = vm.Image.FileName;
            }

            if ((vm.AdditionalImage1 != null && vm.AdditionalImage1.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_1))
            {
                vm.AdditionalImageBase64_1 = await ToDataUrlAsync(vm.AdditionalImage1);
                vm.AdditionalImageFileName1 = vm.AdditionalImage1.FileName;
            }

            if ((vm.AdditionalImage2 != null && vm.AdditionalImage2.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_2))
            {
                vm.AdditionalImageBase64_2 = await ToDataUrlAsync(vm.AdditionalImage2);
                vm.AdditionalImageFileName2 = vm.AdditionalImage2.FileName;
            }

            if ((vm.AdditionalImage3 != null && vm.AdditionalImage3.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_3))
            {
                vm.AdditionalImageBase64_3 = await ToDataUrlAsync(vm.AdditionalImage3);
                vm.AdditionalImageFileName3 = vm.AdditionalImage3.FileName;
            }
        }

        // PreserveImagesForReRenderAsync (Edit)
        private async Task PreserveImagesForReRenderAsync(EditAdvertisementViewModel vm)
        {
            if ((vm.Image != null && vm.Image.Length > 0) && string.IsNullOrWhiteSpace(vm.MainImageBase64))
            {
                vm.MainImageBase64 = await ToDataUrlAsync(vm.Image);
                vm.MainImageFileName = vm.Image.FileName;
            }

            if ((vm.AdditionalImage1 != null && vm.AdditionalImage1.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_1))
            {
                vm.AdditionalImageBase64_1 = await ToDataUrlAsync(vm.AdditionalImage1);
                vm.AdditionalImageFileName1 = vm.AdditionalImage1.FileName;
            }

            if ((vm.AdditionalImage2 != null && vm.AdditionalImage2.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_2))
            {
                vm.AdditionalImageBase64_2 = await ToDataUrlAsync(vm.AdditionalImage2);
                vm.AdditionalImageFileName2 = vm.AdditionalImage2.FileName;
            }

            if ((vm.AdditionalImage3 != null && vm.AdditionalImage3.Length > 0) && string.IsNullOrWhiteSpace(vm.AdditionalImageBase64_3))
            {
                vm.AdditionalImageBase64_3 = await ToDataUrlAsync(vm.AdditionalImage3);
                vm.AdditionalImageFileName3 = vm.AdditionalImage3.FileName;
            }
        }

        private async Task<string?> ToDataUrlAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                return null;
            }

            try
            {
                var bytes = await Helper.GetByteArrayFromImage(file);
                var base64 = Convert.ToBase64String(bytes);
                var mime = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

                return $"data:{mime};base64,{base64}";
            }
            catch
            {
                return null;
            }
        }

        // ClearImageBase64ModelState
        // Html helpers prefer ModelState over the ViewModel. When we fill Base64 from the posted files
        // after ModelState is already invalid, the hidden inputs would still render the old empty string.
        // Removing the entries forces them to use the ViewModel values we just set.
        private void ClearImageBase64ModelState()
        {
            ModelState.Remove("MainImageBase64");
            ModelState.Remove("MainImageFileName");
            ModelState.Remove("AdditionalImageBase64_1");
            ModelState.Remove("AdditionalImageFileName1");
            ModelState.Remove("AdditionalImageBase64_2");
            ModelState.Remove("AdditionalImageFileName2");
            ModelState.Remove("AdditionalImageBase64_3");
            ModelState.Remove("AdditionalImageFileName3");
        }

        // Delete - removes an ad, owner or admin can do it, then redirects to the right profile.
        [HttpPost]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var isAdmin = User.IsInRole(Helper.AdminRole);

            var result = await _ads.DeleteAsync(id, meId, isAdmin, cancellationToken);

            if (result.Outcome == AdvertisementService.DeleteOutcome.NotFound)
            {
                return NotFound();
            }

            var redirectUsername = isAdmin
                ? result.OwnerUsername
                : User.Identity?.Name ?? result.OwnerUsername;

            return RedirectToAction("Profile", "Account", new { username = redirectUsername });
        }

        // Show - displays a single ad, anyone can view it.
        [AllowAnonymous]
        public async Task<IActionResult> Show(int id)
        {
            var vm = await _ads.GetShowViewModelAsync(id, User);

            if (vm == null)
            {
                return NotFound();
            }

            return View("Show", vm);
        }

        // GenerateListingAI - lets AI fill in title and description from your uploaded images.
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
    }
}
