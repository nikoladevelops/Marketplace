using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Register() => _signInManager.IsSignedIn(User) ? RedirectToAction("MyProfile") : View();
        public IActionResult Login() => _signInManager.IsSignedIn(User) ? RedirectToAction("MyProfile") : View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(viewModel.Username, viewModel.Password, viewModel.RememberMe, false);
                if (result.Succeeded) return RedirectToAction("Index", "Home");
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
                var user = new ApplicationUser { UserName = viewModel.Username, Email = viewModel.Email };
                var result = await _userManager.CreateAsync(user, viewModel.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, Helper.SellerRole);
                    await _context.SaveChangesAsync();
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
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
        public IActionResult MyAdvertisements()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var ads = _context.Advertisements
                .Where(x => x.UserId == userId)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImagePath = x.ImagePath,
                    Category = x.Category.Name,
                    Location = x.Location
                })
                .ToList();

            return View(ads);
        }

        [Authorize]
        public IActionResult MyProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var profileVM = _context.Users
                .Where(x => x.Id == userId)
                .Select(x => new MyProfileViewModel()
                {
                    ExistingProfilePicturePath = x.ProfilePicturePath,
                    Description = x.Description,
                    PhoneNumber = x.PhoneNumber,
                    PhoneNumberAgreement = x.PhoneNumber != null
                })
                .Single();

            return View(profileVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(MyProfileViewModel viewModel)
        {
            if (!ModelState.IsValid) return View("MyProfile", viewModel);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var currentUser = _context.Users.First(x => x.Id == userId);
            currentUser.Description = viewModel.Description;

            if (viewModel.PhoneNumber != null)
            {
                if (!viewModel.PhoneNumberAgreement)
                {
                    ViewBag.AgreementError = "You need to click the checkbox";
                    return View("MyProfile", viewModel);
                }
                if (viewModel.PhoneNumber.Length > 15 || viewModel.PhoneNumber.Length < 8 || !viewModel.PhoneNumber.All(char.IsDigit))
                {
                    ViewBag.PhoneError = "The phone number is incorrect.";
                    return View("MyProfile", viewModel);
                }
                currentUser.PhoneNumber = viewModel.PhoneNumber;
            }
            else
            {
                currentUser.PhoneNumber = null;
            }

            if (viewModel.ProfilePicture != null)
            {
                // Case 1: User uploaded a brand-new image
                Helper.DeleteImage(currentUser.ProfilePicturePath, _webHostEnvironment);
                currentUser.ProfilePicturePath = await Helper.SaveImageAsync(viewModel.ProfilePicture, "profiles", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(viewModel.ExistingProfilePicturePath) && !string.IsNullOrEmpty(currentUser.ProfilePicturePath))
            {
                // Case 2: User clicked "Delete" (Existing path was cleared out by JS)
                Helper.DeleteImage(currentUser.ProfilePicturePath, _webHostEnvironment);
                currentUser.ProfilePicturePath = null;
            }
            // Case 3: User made no changes to the picture -> do nothing, keep existing path.

            await _context.SaveChangesAsync();
            return RedirectToAction("MyProfile", "Account");
        }

        [Route("/Users/{username}")]
        public IActionResult Profile(string username)
        {
            var user = _context.Users.SingleOrDefault(x => x.UserName == username);
            if (user == null) return NotFound();

            var userAds = _context.Advertisements
                .Where(x => x.UserId == user.Id)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImagePath = x.ImagePath,
                    Location = x.Location,
                    Category = x.Category.Name
                })
                .ToList();

            ViewBag.ProfilePicturePath = user.ProfilePicturePath;
            ViewBag.Description = user.Description;
            ViewBag.PhoneNumber = user.PhoneNumber;
            ViewBag.Email = user.Email;

            return View(userAds);
        }
    }
}