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
        public async Task<IActionResult> Create(AdvertisementViewModel viewModel, IEnumerable<SelectListItem> dropDown)
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
    }
}
