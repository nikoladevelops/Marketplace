using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    [Authorize(Roles = Helper.AdminRole)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult AdminPanel()
        {
            return View();
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public IActionResult SearchUser(string username)
        {
            var user = _context.Users
                .SingleOrDefault(x => x.UserName == username);

            var vm = new AdminPanelViewModel()
            {
                Username = username
            };

            if (user == null)
            {
                vm.UserNotFound = true;
                return View("AdminPanel", vm);
            }

            vm.UserId = user.Id;
            return View("AdminPanel",vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> GiveUserRole(string userId, string roleName)
        {
            var user = _context.Users
                .SingleOrDefault(x => x.Id == userId);

            if (user==null)
            {
                return NotFound();
            }

            await _userManager.AddToRoleAsync(user, roleName);

            var vm = new AdminPanelViewModel() { UserAccountUpdated = true };

            return View("AdminPanel", vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> RemoveUserRole(string userId, string roleName)
        {
            var user = _context.Users
                .SingleOrDefault(x => x.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.RemoveFromRoleAsync(user, roleName);

            var vm = new AdminPanelViewModel() { UserAccountUpdated = true };

            return View("AdminPanel", vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(string userId)
        {
            var user = _context.Users
                .SingleOrDefault(x => x.Id == userId);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.DeleteAsync(user);

            var vm = new AdminPanelViewModel() { UserAccountUpdated = true };

            return View("AdminPanel", vm);
        }

    }
}
