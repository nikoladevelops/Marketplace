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

        
        public IActionResult Create()
        {
            var viewModel = new AdvertisementViewModel()
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
        public async Task<IActionResult> Create(AdvertisementViewModel viewModel)
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

            foreach (var img in viewModel.AdditionalImages)
            {
                var advertisementImages = new AdvertisementImages()
                {
                    Image = await GetByteArrayFromImage(img),
                    AdvertisementId = advertisement.Id
                };

                await _context.AdvertisementImages.AddAsync(advertisementImages);
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

        public IActionResult Edit(int id)
        {
            //TODO MAKE IT SO THAT THE USER THAT IS LOGGED IN CAN EDIT ONLY THEIR OWN ADS
            
            var categoryDropDown = _context.Categories
                    .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                    .ToList();

            var additionalImages = _context.AdvertisementImages
                .Where(x => x.AdvertisementId == id)
                .Select(x => Convert.ToBase64String(x.Image))
                .ToList();

            var ad = _context.Advertisements
                .Select(x => new AdvertisementViewModel() {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    Price = x.Price,
                    Location = x.Location,
                    CategoryId = x.CategoryId,
                    ImageInBase64 = Convert.ToBase64String(x.Image),
                    AdditionalImagesInBase64 = additionalImages,
                    CategoryDropDown = categoryDropDown
                })
                .FirstOrDefault(x=>x.Id==id);

            if (ad==null)
            {
                return BadRequest();
            }


            return View("Edit",ad);
        }
    }
}
