using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Marketplace.Services
{
    // AccountService - handles user accounts, profiles and auth helpers.
    public class AccountService
    {
        private const int ProfilePageSize = 12;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // AccountService - wire up DB, identity and hosting env.
        public AccountService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // IsSignedIn - quick check if someone is logged in right now.
        public bool IsSignedIn => _signInManager.IsSignedIn(ClaimsPrincipal.Current);

        // FindByUsernameAsync - look up a user by name.
        public async Task<ApplicationUser?> FindByUsernameAsync(string username) =>
            await _userManager.FindByNameAsync(username);

        // PasswordSignInAsync - sign in with username and password.
        public async Task<SignInResult> PasswordSignInAsync(string username, string password, bool rememberMe) =>
            await _signInManager.PasswordSignInAsync(username, password, rememberMe, false);

        // SignOutAsync - log the current user out.
        public async Task SignOutAsync() => await _signInManager.SignOutAsync();

        // RegisterAsync - create a new user, give seller role and sign them in.
        public async Task<IdentityResult> RegisterAsync(string username, string email, string password)
        {
            var user = new ApplicationUser
            {
                UserName = username,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                return result;
            }

            await _userManager.AddToRoleAsync(user, Helper.SellerRole);
            await _signInManager.SignInAsync(user, isPersistent: false);

            return result;
        }

        // GetProfileAsync - builds the public profile page with paging and contact visibility.
        public async Task<ProfileViewModel?> GetProfileAsync(string username, ClaimsPrincipal viewer, int pageNumber = 0)
        {
            var user = await _context.Users
                .Where(x => x.UserName == username)
                .Select(x => new
                {
                    x.Id,
                    x.UserName,
                    x.ProfilePicturePath,
                    x.Description,
                    x.Email,
                    x.PhoneNumber,
                    x.ShowEmail,
                    x.ShowPhone
                })
                .SingleOrDefaultAsync();

            if (user == null)
            {
                return null;
            }

            var currentUserId = viewer.FindFirstValue(ClaimTypes.NameIdentifier);
            var isOwner = currentUserId != null && currentUserId == user.Id;
            var isAdmin = viewer.IsInRole(Helper.AdminRole);
            var isAuthed = viewer.Identity?.IsAuthenticated == true;
            var isPremium = viewer.IsInRole(Helper.PremiumRole);

            // Paging setup for this user's ads.

            var baseQuery = _context.Advertisements.Where(x => x.UserId == user.Id);

            var totalCount = baseQuery.Count();
            var maxCountPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)ProfilePageSize);

            pageNumber = Math.Clamp(pageNumber, 0, Math.Max(0, maxCountPages - 1));

            // Load one page of ads, newest first.

            var userAds = baseQuery
                .OrderByDescending(x => x.DateCreatedOn)
                .Skip(pageNumber * ProfilePageSize)
                .Take(ProfilePageSize)
                .Select(x => new SimplifiedAdvertisementViewModel
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

            // Re-hydrate into a full user so the existing visibility helper works.

            var hydrated = new ApplicationUser
            {
                Id = user.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                ShowEmail = user.ShowEmail,
                ShowPhone = user.ShowPhone
            };

            var phoneView = ContactVisibilityHelper.ResolvePhone(hydrated, viewer);
            var emailView = ContactVisibilityHelper.ResolveEmail(hydrated, viewer);

            return new ProfileViewModel
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
                PageSize = ProfilePageSize,
                MaxAdvertisements = Helper.MaxAdsForRoles(isPremium),
                IsPremium = isPremium,
                ShowEditForm = false,
                EditForm = isOwner ? new MyProfileViewModel
                {
                    ExistingProfilePicturePath = user.ProfilePicturePath,
                    Description = user.Description,
                    PhoneNumber = user.PhoneNumber,
                    ShowPhone = user.ShowPhone,
                    ShowEmail = user.ShowEmail
                } : null
            };
        }

        public enum UpdateOutcome
        {
            Success,
            InvalidModel,
            PhoneRequiredWhenShown,
            PhoneInvalid
        }

        public record UpdateResult(UpdateOutcome Outcome, ProfileViewModel? ErrorProfile = null, string? Error = null);

        // UpdateProfileAsync - saves profile edits after validating phone and picture.
        public async Task<UpdateResult> UpdateProfileAsync(string userId, MyProfileViewModel viewModel, ClaimsPrincipal viewer)
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);

            if (currentUser == null)
            {
                return new(UpdateOutcome.InvalidModel);
            }

            ProfileViewModel BuildErrorProfile()
            {
                var isAdmin = viewer.IsInRole(Helper.AdminRole);

                var phoneView = ContactVisibilityHelper.ResolvePhone(currentUser, viewer);
                var emailView = ContactVisibilityHelper.ResolveEmail(currentUser, viewer);

                return new ProfileViewModel
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
                    PageSize = ProfilePageSize,
                    Advertisements = Enumerable.Empty<SimplifiedAdvertisementViewModel>(),
                    EditForm = viewModel,
                    ShowEditForm = true,
                    DisplayPhone = phoneView.Display,
                    CanViewPhone = phoneView.CanView,
                    IsCensoredPhone = phoneView.IsCensored,
                    DisplayEmail = emailView.Display,
                    CanViewEmail = emailView.CanView,
                    IsCensoredEmail = emailView.IsCensored
                };
            }

            // Validate phone number rules.

            var phoneRaw = viewModel.PhoneNumber?.Trim();

            if (viewModel.ShowPhone)
            {
                if (string.IsNullOrWhiteSpace(phoneRaw))
                {
                    return new(UpdateOutcome.PhoneRequiredWhenShown, BuildErrorProfile());
                }

                if (!IsValidPhone(phoneRaw))
                {
                    return new(UpdateOutcome.PhoneInvalid, BuildErrorProfile());
                }
            }
            else if (!string.IsNullOrWhiteSpace(phoneRaw) && !IsValidPhone(phoneRaw))
            {
                return new(UpdateOutcome.PhoneInvalid, BuildErrorProfile());
            }

            // Apply basic fields.

            currentUser.PhoneNumber = string.IsNullOrWhiteSpace(phoneRaw) ? null : phoneRaw;
            currentUser.ShowPhone = viewModel.ShowPhone;
            currentUser.ShowEmail = viewModel.ShowEmail;
            currentUser.Description = viewModel.Description;

            // Handle profile picture - upload, keep, or delete.

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

            return new(UpdateOutcome.Success);
        }

        // GetUpdatedUsername - fetch the latest username for a user id.
        public string? GetUpdatedUsername(string userId)
        {
            var u = _context.Users.FirstOrDefault(x => x.Id == userId);

            return u?.UserName;
        }

        // IsValidPhone - simple check, 8 to 15 digits only.
        private static bool IsValidPhone(string phone) =>
            phone.Length is >= 8 and <= 15 && phone.All(char.IsDigit);
    }
}
