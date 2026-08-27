using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    [Authorize(Roles = Helper.AdminRole)]
    public class AdminController : Controller
    {
        private readonly UserAdministrationService _admin;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public AdminController(UserAdministrationService admin, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _admin = admin;
            _userManager = userManager;
        }

        public async Task<IActionResult> AdminPanel(string searchTerm, string roleFilter = "all", int pageNumber = 0, string selectedUserId = null)
        {
            var meId = _userManager.GetUserId(User);
            var vm = await _admin.SearchAsync(searchTerm, roleFilter, pageNumber, selectedUserId, meId);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm, string roleFilter = "all", int pageNumber = 0)
        {
            var vm = await _admin.SearchListAsync(searchTerm, roleFilter, pageNumber);
            return PartialView("_AdminUserList", vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> GiveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";
            var outcome = await _admin.ChangeRoleAsync(userId, roleName, adminId);
            TempData["StatusMessage"] = outcome.Message;
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> RemoveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";
            var outcome = await _admin.RemoveRoleAsync(userId, roleName, adminId);
            TempData["StatusMessage"] = outcome.Message;
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(string userId, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";
            var outcome = await _admin.DeleteAsync(userId, adminId);
            TempData["StatusMessage"] = outcome.Message;
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> BanUser(string userId, string? reason, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";
            var outcome = await _admin.BanAsync(userId, reason, adminId);
            TempData["StatusMessage"] = outcome.Message;
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> UnbanUser(string userId, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";
            var outcome = await _admin.UnbanAsync(userId, adminId);
            TempData["StatusMessage"] = outcome.Message;
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }
    }
}
