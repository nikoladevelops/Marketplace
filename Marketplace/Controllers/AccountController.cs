using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    public class AccountController:Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }
        public IActionResult Register() 
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("MyProfile");
            }

            return View();
        }
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("MyProfile");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(viewModel.Username, viewModel.Password, viewModel.RememberMe, false);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index","Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid log in attempt.");
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser()
                {
                    UserName = viewModel.Username,
                    Email = viewModel.Email
                };

                var result = await _userManager.CreateAsync(user, viewModel.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, Helper.SellerRole);
                    await _context.SaveChangesAsync();
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index","Home");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public IActionResult MyProfile(bool hasError)
        {
            var currentLoggedInUserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var currentUserAllAds = _context.Advertisements
                .Where(x => x.UserId == currentLoggedInUserId)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImageInBase64=Convert.ToBase64String(x.ImageData)
                })
                .ToList();

            var currentUserData = _context.Users
                .Where(x => x.Id == currentLoggedInUserId)
                .Select(x => new
                {
                    ProfilePicture = x.ProfilePicture,
                    Description = x.Description
                })
                .Single();

            ViewBag.ProfilePicture=currentUserData.ProfilePicture;
            ViewBag.Description=currentUserData.Description;

            if (hasError)
            {
                ViewBag.ErrorMessage = "Description can NOT be more than 250 characters";
            }
            return View(currentUserAllAds);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(string description, IFormFile profilePicture)
        {
            if (description!=null && description.Length>250)
            {
                return RedirectToAction("MyProfile","Account",new { hasError = true });
            }

            var currentLoggedInUserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var currentUser = _context.Users.First(x => x.Id == currentLoggedInUserId);
            currentUser.Description=description;
            if (profilePicture!=null)
            {
                currentUser.ProfilePicture = await Helper.GetByteArrayFromImage(profilePicture);
            }
            else
            {
                currentUser.ProfilePicture = null;
            }
            await _context.SaveChangesAsync();

            return RedirectToAction("MyProfile","Account");
        }

        [Route("/Users/{username}")]
        public IActionResult Profile(string username)
        {
            var user = _context.Users
               .SingleOrDefault(x => x.UserName == username);

            if (user == null)
            {
                return NotFound();
            }

            var userAds = _context.Advertisements
                .Where(x => x.UserId == user.Id)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImageInBase64 = Convert.ToBase64String(x.ImageData)
                })
                .ToList();

           

            ViewBag.ProfilePicture = user.ProfilePicture;
            ViewBag.Description = user.Description;

            return View(userAds);
        }
    }

}
