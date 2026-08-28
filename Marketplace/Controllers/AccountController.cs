using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Marketplace.Controllers
{
    // AccountController - handles sign up, login, logout and user profiles.
    // This is the front door for accounts, keeps login flow tidy.
    public class AccountController : Controller
    {
        private readonly AccountService _accounts;
        private readonly SignInManager<ApplicationUser> _signInManager;

        // Constructor - wires up the account service and sign in manager.
        public AccountController(AccountService accounts, SignInManager<ApplicationUser> signInManager)
        {
            _accounts = accounts;
            _signInManager = signInManager;
        }

        // Register (GET) - shows the sign up page, or sends you to your profile if you are already logged in.
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Profile", new { username = User.Identity?.Name });
            }

            return View();
        }

        // Login (GET) - shows the login page, or sends you to your profile if you are already logged in.
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Profile", new { username = User.Identity?.Name });
            }

            return View();
        }

        // Login (POST) - checks your username and password, handles banned accounts and redirects on success.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _accounts.PasswordSignInAsync(viewModel.Username, viewModel.Password, viewModel.RememberMe);

                if (result.Succeeded)
                {
                    var signedInUser = await _accounts.FindByUsernameAsync(viewModel.Username);

                    if (signedInUser != null && signedInUser.Status == AccountStatus.Banned)
                    {
                        await _accounts.SignOutAsync();

                        ModelState.AddModelError(string.Empty, "This account has been suspended. Contact support if you believe this is a mistake.");

                        return View(viewModel);
                    }

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid log in attempt.");
            }

            return View(viewModel);
        }

        // Banned - shows a simple page that tells the user their account is banned.
        [HttpGet]
        public IActionResult Banned()
        {
            return View();
        }

        // Register (POST) - creates a new account and logs you in, shows errors if something is wrong.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel viewModel, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _accounts.RegisterAsync(viewModel.Username, viewModel.Email, viewModel.Password);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(viewModel);
        }

        // LogOut - signs you out and sends you back to the home page.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> LogOut()
        {
            await _accounts.SignOutAsync();

            return RedirectToAction("Index", "Home");
        }

        // Update - saves edits to your profile, shows friendly errors for phone validation.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(MyProfileViewModel viewModel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var result = await _accounts.UpdateProfileAsync(userId, viewModel, User);

            switch (result.Outcome)
            {
                case AccountService.UpdateOutcome.Success:
                    var username = _accounts.GetUpdatedUsername(userId);

                    return RedirectToAction("Profile", new { username });

                case AccountService.UpdateOutcome.PhoneRequiredWhenShown:
                    ViewBag.PhoneError = "Phone number is required when you choose to show it.";

                    break;

                case AccountService.UpdateOutcome.PhoneInvalid:
                    ViewBag.PhoneError = "The phone number is incorrect.";

                    break;

                case AccountService.UpdateOutcome.InvalidModel:
                    ViewBag.PhoneError ??= "Please correct the errors below.";

                    break;
            }

            return View("Profile", result.ErrorProfile);
        }

        // Profile - shows a user's public profile and their ads, can toggle edit mode for the owner.
        [Route("/Users/{username}")]
        public async Task<IActionResult> Profile(string username, int pageNumber = 0, bool edit = false)
        {
            var vm = await _accounts.GetProfileAsync(username, User, pageNumber);

            if (vm == null)
            {
                return NotFound();
            }

            if (edit)
            {
                vm.ShowEditForm = vm.IsOwner;
            }

            return View(vm);
        }
    }
}
