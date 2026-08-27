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
    public class AccountController : Controller
    {
        private readonly AccountService _accounts;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(AccountService accounts, SignInManager<ApplicationUser> signInManager)
        {
            _accounts = accounts;
            _signInManager = signInManager;
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
                var result = await _accounts.RegisterAsync(viewModel.Username, viewModel.Email, viewModel.Password);
                if (result.Succeeded)
                {
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
            await _accounts.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(MyProfileViewModel viewModel)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Challenge();

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

        [Route("/Users/{username}")]
        public async Task<IActionResult> Profile(string username, int pageNumber = 0, bool edit = false)
        {
            var vm = await _accounts.GetProfileAsync(username, User, pageNumber);
            if (vm == null) return NotFound();
            if (edit) vm.ShowEditForm = vm.IsOwner;
            return View(vm);
        }
    }
}
