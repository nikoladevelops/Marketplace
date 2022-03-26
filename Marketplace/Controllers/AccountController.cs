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
        public IActionResult MyAdvertisements()
        {
            var currentLoggedInUserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var currentUserAllAds = _context.Advertisements
                .Where(x => x.UserId == currentLoggedInUserId)
                .Select(x => new SimplifiedAdvertisementViewModel()
                {
                    Id = x.Id,
                    Title = x.Title,
                    Price = x.Price,
                    ImageInBase64=Convert.ToBase64String(x.ImageData),
                    Category=x.Category.Name,
                    Location=x.Location
                })
                .ToList();

            return View(currentUserAllAds);
        }

        [Authorize]
        public IActionResult MyProfile()
        {
            var currentLoggedInUserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var currentUserDataVM = _context.Users
                .Where(x => x.Id == currentLoggedInUserId)
                .Select(x => new MyProfileViewModel()
                {
                    ProfilePictureInBytes = x.ProfilePicture,
                    Description = x.Description,
                    PhoneNumber = x.PhoneNumber,
                    PhoneNumberAgreement = x.PhoneNumber == null ? false : true
                })
                .Single();

            return View(currentUserDataVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Update(MyProfileViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View("MyProfile",viewModel);
            }

            var currentLoggedInUserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var currentUser = _context.Users.First(x => x.Id == currentLoggedInUserId);
            currentUser.Description=viewModel.Description;
            
            if (viewModel.PhoneNumber!=null)
            {
                if (viewModel.PhoneNumberAgreement)
                {
                    //verify that it's actually a phone number
                    bool isInvalidLength = viewModel.PhoneNumber.Length > 15 || viewModel.PhoneNumber.Length < 8;
                    bool hasSymbols = !(int.TryParse(viewModel.PhoneNumber, out int num));

                    if (isInvalidLength || hasSymbols)
                    {
                        ViewBag.PhoneError = "The phone number is incorrect.";
                        return View("MyProfile", viewModel);
                    }
                    
                    currentUser.PhoneNumber = viewModel.PhoneNumber;
                }
                else
                {
                    ViewBag.AgreementError = "You need to click the checkbox";
                    return View("MyProfile", viewModel);
                }
            }
            else
            {
                currentUser.PhoneNumber = null;
            }

            if (viewModel.ProfilePicture!=null)
            {
                currentUser.ProfilePicture = await Helper.GetByteArrayFromImage(viewModel.ProfilePicture);
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
                    ImageInBase64 = Convert.ToBase64String(x.ImageData),
                    Location=x.Location,
                    Category=x.Category.Name
                })
                .ToList();

           

            ViewBag.ProfilePicture = user.ProfilePicture;
            ViewBag.Description = user.Description;
            ViewBag.PhoneNumber = user.PhoneNumber;
            ViewBag.Email = user.Email;

            return View(userAds);
        }
    }

}
