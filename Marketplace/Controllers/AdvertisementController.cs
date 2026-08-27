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
    [Authorize]
    public class AdvertisementController : Controller
    {
        private readonly IAiService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly AdvertisementService _ads;

        public AdvertisementController(IAiService aiService, ApplicationDbContext context, AdvertisementService ads)
        {
            _aiService = aiService;
            _context = context;
            _ads = ads;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (meId == null) return Challenge();
            var isPremium = User.IsInRole(Helper.PremiumRole);
            var vm = await _ads.PrepareCreateViewModelAsync(meId, isPremium);
            if (vm == null) return View("ReachedMaximumAds");
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAdvertisementViewModel viewModel, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User not authenticated");
            var isPremium = User.IsInRole(Helper.PremiumRole);
            var (result, returnedVm) = await _ads.CreateAsync(viewModel, meId, isPremium, cancellationToken);

            if (result == AdvertisementService.CreateValidation.MaxAdsReached) return View("ReachedMaximumAds");
            if (result == AdvertisementService.CreateValidation.CategoryInvalid)
            {
                returnedVm!.CategoryDropDown = _ads.LoadCategoryDropDown();
                ModelState.AddModelError(nameof(viewModel.CategoryId), "You need to select a category.");
                return View(returnedVm);
            }
            return RedirectToAction("Profile", "Account", new { username = User.Identity?.Name ?? "me" });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole(Helper.AdminRole);
            var vm = await _ads.PrepareEditViewModelAsync(id, meId, isAdmin);
            if (vm == null) return NotFound();
            return View("Edit", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditAdvertisementViewModel model, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole(Helper.AdminRole);
            var result = await _ads.UpdateAsync(model, meId, isAdmin, cancellationToken);

            if (result == AdvertisementService.EditValidation.ImageMissing)
                ModelState.AddModelError("Image", "The main advertisement image is mandatory.");
            if (result == AdvertisementService.EditValidation.CategoryInvalid)
                ModelState.AddModelError(nameof(model.CategoryId), "You need to select a category.");

            if (result != AdvertisementService.EditValidation.Ok)
            {
                model.CategoryDropDown = _ads.LoadCategoryDropDown();
                model.ExistingAdditionalImagePaths ??= new List<string> { "", "", "" };
                return View(model);
            }

            return RedirectToAction("Profile", "Account", new { username = User.Identity?.Name ?? "me" });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var meId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole(Helper.AdminRole);
            var result = await _ads.DeleteAsync(id, meId, isAdmin, cancellationToken);
            if (result.Outcome == AdvertisementService.DeleteOutcome.NotFound) return NotFound();

            var redirectUsername = isAdmin
                ? result.OwnerUsername
                : User.Identity?.Name ?? result.OwnerUsername;
            return RedirectToAction("Profile", "Account", new { username = redirectUsername });
        }

        [AllowAnonymous]
        public async Task<IActionResult> Show(int id)
        {
            var vm = await _ads.GetShowViewModelAsync(id, User);
            if (vm == null) return NotFound();
            return View("Show", vm);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GenerateListingAI(List<IFormFile> images, CancellationToken cancellationToken)
        {
            if (images == null || !images.Any(f => f != null && f.Length > 0))
                return Json(new { success = false, message = "No valid images provided." });

            var result = await _aiService.GenerateListingFromImagesAsync(images, cancellationToken);
            if (result == null)
                return Json(new { success = false, message = "AI generation failed or LM Studio is unresponsive." });

            return Json(new { success = true, data = result });
        }
    }
}
