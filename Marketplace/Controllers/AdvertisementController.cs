using Marketplace.Models;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    public class AdvertisementController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AdvertisementController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public IActionResult Create()
        {
            var viewModel = new CreateAdvertisementViewModel()
            {
                CategoryDropDown = _context.Categories
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList()
            };
            
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(CreateAdvertisementViewModel viewModel)
        {
            if (!ModelState.IsValid) 
            {
                viewModel.CategoryDropDown = _context.Categories
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();

                return View(viewModel);
            }
            
            var advertisement = new AdvertisementModel()
            {
                Image = await GetByteArrayFromImage(viewModel.Image),
                Title = viewModel.Title,
                Description = viewModel.Description,
                Price = viewModel.Price,
                Location=viewModel.Location,
                UserId = User.FindFirst(ClaimTypes.NameIdentifier).Value,
                CategoryId=viewModel.CategoryId,
            };

            await _context.Advertisements.AddAsync(advertisement);
            await _context.SaveChangesAsync();

            if (viewModel.AdditionalImages!=null)
            {
                foreach (var img in viewModel.AdditionalImages)
                {
                    var advertisementImages = new AdvertisementImageModel()
                    {
                        Image = await GetByteArrayFromImage(img),
                        AdvertisementId = advertisement.Id
                    };

                    await _context.AdvertisementImages.AddAsync(advertisementImages);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile","Account");
        }

        private static async Task<byte[]> GetByteArrayFromImage(IFormFile file)
        {
            using (var target = new MemoryStream())
            {
                await file.CopyToAsync(target);
                return target.ToArray();
            }
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
                    ImageInBytes = x.Image,
                    UserId = x.UserId
                })
                .FirstOrDefault(x => x.Id == id);

            if (ad == null || currentLoggedInUserId != ad.UserId)
            {
                return NotFound();
            }

            var categoryDropDown = _context.Categories
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                    .ToList();

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => x.Image)
                .ToList();

            ad.CategoryDropDown = categoryDropDown;
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
                return View(viewModel);
            }

            var advertisement = _context.Advertisements.FirstOrDefault(x => x.Id == viewModel.Id);
            var currentLoggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (advertisement == null || currentLoggedInUserId != advertisement.UserId)
            {
                return NotFound();
            }

            if (viewModel.Image!=null)
            {
                // if the user has selected another image change it to the new one, otherwise don't
                advertisement.Image = await GetByteArrayFromImage(viewModel.Image);
            }
            advertisement.Title = viewModel.Title;
            advertisement.Description = viewModel.Description;
            advertisement.Price = viewModel.Price;
            advertisement.Location = viewModel.Location;
            advertisement.CategoryId = viewModel.CategoryId;

            await _context.SaveChangesAsync();

            var imgs = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == advertisement.Id)
                .ToList();


            if (viewModel.AdditionalImages != null)
            {
                _context.AdvertisementImages.RemoveRange(imgs);

                foreach (var img in viewModel.AdditionalImages)
                {
                    var advertisementImage = new AdvertisementImageModel()
                    {
                        Image = await GetByteArrayFromImage(img),
                        AdvertisementId = advertisement.Id
                    };

                    await _context.AdvertisementImages.AddAsync(advertisementImage);
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile", "Account");
        }
    }
}
