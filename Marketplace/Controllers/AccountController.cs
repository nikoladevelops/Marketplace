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

        public IActionResult Register(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return _signInManager.IsSignedIn(User) ? RedirectToAction("Profile", new { username = User.Identity?.Name }) : View(); }
        public IActionResult Login(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return _signInManager.IsSignedIn(User) ? RedirectToAction("Profile", new { username = User.Identity?.Name }) : View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(viewModel.Username, viewModel.Password, viewModel.RememberMe, false);
                if (result.Succeeded)
                {
                    // Block banned users with a specific, clear message so they
                    // know why they can't sign in (and so admins can verify the
                    // account is banned from the same screen).
                    var signedInUser = await _userManager.FindByNameAsync(viewModel.Username);
                    if (signedInUser != null && signedInUser.Status == Models.AccountStatus.Banned)
                    {
                        await _signInManager.SignOutAsync();
                        ModelState.AddModelError(string.Empty, "This account has been suspended. Contact support if you believe this is a mistake.");
                        return View(viewModel);
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Invalid log in attempt.");
            }
            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Banned() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel viewModel, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = viewModel.Username, Email = viewModel.Email };
                var result = await _userManager.CreateAsync(user, viewModel.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, Helper.SellerRole);
                    await _context.SaveChangesAsync();
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
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

            // Prepare error profile with visibility handling
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
                    ShowEmail = currentUser.ShowEmail,
                    ShowPhone = currentUser.ShowPhone,
                    IsOwner = true,
                    IsAdmin = isAdmin,
                    CurrentUserId = userId,
                    IsAuthenticated = true,
                    PageNumber = 0,
                    MaxCountPages = 0,
                    TotalCount = 0,
                    PageSize = pageSize,
                    Advertisements = Enumerable.Empty<SimplifiedAdvertisementViewModel>(),
                    EditForm = viewModel,
                    ShowEditForm = true
                };
                // Populate censored display for error view
                var phoneView = ContactVisibilityHelper.ResolvePhone(currentUser, User);
                var emailView = ContactVisibilityHelper.ResolveEmail(currentUser, User);
                vm.DisplayPhone = phoneView.Display;
                vm.CanViewPhone = phoneView.CanView;
                vm.IsCensoredPhone = phoneView.IsCensored;
                vm.DisplayEmail = emailView.Display;
                vm.CanViewEmail = emailView.CanView;
                vm.IsCensoredEmail = emailView.IsCensored;
                return vm;
            }

            if (!ModelState.IsValid)
            {
                return View("Profile", BuildErrorProfile());
            }

            // Phone validation – only required if ShowPhone is true
            var phoneRaw = viewModel.PhoneNumber?.Trim();
            if (viewModel.ShowPhone)
            {
                if (string.IsNullOrWhiteSpace(phoneRaw))
                {
                    ViewBag.PhoneError = "Phone number is required when you choose to show it.";
                    return View("Profile", BuildErrorProfile());
                }
                if (phoneRaw.Length > 15 || phoneRaw.Length < 8 || !phoneRaw.All(char.IsDigit))
                {
                    ViewBag.PhoneError = "The phone number is incorrect.";
                    return View("Profile", BuildErrorProfile());
                }
            }
            else if (!string.IsNullOrWhiteSpace(phoneRaw) && (phoneRaw.Length > 15 || phoneRaw.Length < 8 || !phoneRaw.All(char.IsDigit)))
            {
                ViewBag.PhoneError = "The phone number is incorrect.";
                return View("Profile", BuildErrorProfile());
            }

            // Persist phone & visibility
            currentUser.PhoneNumber = string.IsNullOrWhiteSpace(phoneRaw) ? null : phoneRaw;
            currentUser.ShowPhone = viewModel.ShowPhone;
            currentUser.ShowEmail = viewModel.ShowEmail;

            currentUser.Description = viewModel.Description;

            if (viewModel.ProfilePicture != null)
            {
                Helper.DeleteImage(currentUser.ProfilePicturePath, _webHostEnvironment);
                currentUser.ProfilePicturePath = await Helper.SaveImageAsync(viewModel.ProfilePicture, "profiles", _webHostEnvironment)!;
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
            var isAuthed = User.Identity?.IsAuthenticated == true;

            var isPremium = User.IsInRole(Helper.PremiumRole);
            var maxAds = Helper.MaxAdsForRoles(isPremium);

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

            var phoneView = ContactVisibilityHelper.ResolvePhone(user, User);
            var emailView = ContactVisibilityHelper.ResolveEmail(user, User);

            var vm = new ProfileViewModel
            {
                Username = user.UserName ?? username,
                ProfilePicturePath = user.ProfilePicturePath,
                Description = user.Description,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                ShowEmail = user.ShowEmail,
                ShowPhone = user.ShowPhone,
                DisplayPhone = phoneView.Display,
                CanViewPhone = phoneView.CanView,
                IsCensoredPhone = phoneView.IsCensored,
                DisplayEmail = emailView.Display,
                CanViewEmail = emailView.CanView,
                IsCensoredEmail = emailView.IsCensored,
                IsAuthenticated = isAuthed,
                IsOwner = isOwner,
                IsAdmin = isAdmin,
                CurrentUserId = currentUserId ?? "",
                Advertisements = userAds,
                PageNumber = pageNumber,
                MaxCountPages = maxCountPages,
                TotalCount = totalCount,
                PageSize = pageSize,
                MaxAdvertisements = maxAds,
                IsPremium = isPremium,
                ShowEditForm = edit && isOwner,
                EditForm = (edit && isOwner) ? new MyProfileViewModel
                {
                    ExistingProfilePicturePath = user.ProfilePicturePath,
                    Description = user.Description,
                    PhoneNumber = user.PhoneNumber,
                    ShowPhone = user.ShowPhone,
                    ShowEmail = user.ShowEmail
                } : null
            };

            return View(vm);
        }
    }
}
