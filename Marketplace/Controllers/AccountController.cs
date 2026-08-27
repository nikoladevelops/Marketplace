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

        public IActionResult Register() => _signInManager.IsSignedIn(User) ? RedirectToAction("Profile", new { username = User.Identity?.Name }) : View();
        public IActionResult Login() => _signInManager.IsSignedIn(User) ? RedirectToAction("Profile", new { username = User.Identity?.Name }) : View();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(MyProfileViewModel viewModel)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge();
            var currentUser = _context.Users.FirstOrDefault(x => x.Id == userId);
            if (currentUser == null) return NotFound();

            // Validate and prepare ProfileViewModel for re-render on error (unified profile)
            ProfileViewModel BuildErrorProfile()
            {
                var isAdmin = User.IsInRole(Helper.AdminRole);
                const int pageSize = 12;
                var vm = new ProfileViewModel
                {
                    Username = currentUser.UserName ?? "",
                    ProfilePicturePath = currentUser.ProfilePicturePath,
                    Description = currentUser.Description,
                    Email = currentUser.Email,
                    PhoneNumber = currentUser.PhoneNumber,
                    IsOwner = true,
                    IsAdmin = isAdmin,
                    CurrentUserId = userId,
                    PageNumber = 0,
                    MaxCountPages = 0,
                    TotalCount = 0,
                    PageSize = pageSize,
                    Advertisements = Enumerable.Empty<SimplifiedAdvertisementViewModel>(),
                    EditForm = viewModel,
                    ShowEditForm = true
                };
                return vm;
            }

            if (!ModelState.IsValid)
            {
                return View("Profile", BuildErrorProfile());
            }

            // Phone validation
            if (viewModel.PhoneNumber != null)
            {
                if (!viewModel.PhoneNumberAgreement)
                {
                    ViewBag.AgreementError = "You need to click the checkbox";
                    return View("Profile", BuildErrorProfile());
                }
                if (viewModel.PhoneNumber.Length > 15 || viewModel.PhoneNumber.Length < 8 || !viewModel.PhoneNumber.All(char.IsDigit))
                {
                    ViewBag.PhoneError = "The phone number is incorrect.";
                    return View("Profile", BuildErrorProfile());
                }
                currentUser.PhoneNumber = viewModel.PhoneNumber;
            }
            else
            {
                currentUser.PhoneNumber = null;
            }

            currentUser.Description = viewModel.Description;

            if (viewModel.ProfilePicture != null)
            {
                Helper.DeleteImage(currentUser.ProfilePicturePath, _webHostEnvironment);
                currentUser.ProfilePicturePath = await Helper.SaveImageAsync(viewModel.ProfilePicture, "profiles", _webHostEnvironment);
            }
            else if (string.IsNullOrEmpty(viewModel.ExistingProfilePicturePath) && !string.IsNullOrEmpty(currentUser.ProfilePicturePath))
            {
                Helper.DeleteImage(currentUser.ProfilePicturePath, _webHostEnvironment);
                currentUser.ProfilePicturePath = null;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Profile", new { username = currentUser.UserName });
        }

        [Route("/Users/{username}")]
        public IActionResult Profile(string username, int pageNumber = 0, bool edit = false)
        {
            var user = _context.Users.SingleOrDefault(x => x.UserName == username);
            if (user == null) return NotFound();

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isOwner = currentUserId != null && currentUserId == user.Id;
            var isAdmin = User.IsInRole(Helper.AdminRole);

            const int pageSize = 12;
            var baseQuery = _context.Advertisements.Where(x => x.UserId == user.Id);
            var totalCount = baseQuery.Count();
            var maxCountPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            pageNumber = Math.Clamp(pageNumber, 0, Math.Max(0, maxCountPages - 1));

            var userAds = baseQuery
                .OrderByDescending(x => x.DateCreatedOn)
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImagePath = x.ImagePath,
                    Location = x.Location,
                    Category = x.Category.Name,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? "",
                    DateCreatedOn = x.DateCreatedOn
                })
                .ToList();

            // Phone visibility: show if set; for anonymous always show if exists, for owner/admin always show own
            string? visiblePhone = user.PhoneNumber;
            // If you want to hide phone for visitors when not agreed, you could check a dedicated flag;
            // currently agreement is implied by phone != null, so show always when exists.

            var vm = new ProfileViewModel
            {
                Username = user.UserName ?? username,
                ProfilePicturePath = user.ProfilePicturePath,
                Description = user.Description,
                Email = user.Email,
                PhoneNumber = visiblePhone,
                IsOwner = isOwner,
                IsAdmin = isAdmin,
                CurrentUserId = currentUserId ?? "",
                Advertisements = userAds,
                PageNumber = pageNumber,
                MaxCountPages = maxCountPages,
                TotalCount = totalCount,
                PageSize = pageSize,
                ShowEditForm = edit && isOwner,
                EditForm = (edit && isOwner) ? new MyProfileViewModel
                {
                    ExistingProfilePicturePath = user.ProfilePicturePath,
                    Description = user.Description,
                    PhoneNumber = user.PhoneNumber,
                    PhoneNumberAgreement = user.PhoneNumber != null
                } : null
            };

            return View(vm);
        }
    }
}
