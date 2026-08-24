using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IActionResult> AdminPanel(string searchTerm, string selectedUserId)
        {
            var vm = new AdminPanelViewModel
            {
                SearchTerm = searchTerm
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var pattern = $"%{searchTerm.Trim()}%";
                var matchedUsers = await _context.Users
                    .Where(u => EF.Functions.ILike(u.UserName ?? "", pattern)
                                || EF.Functions.ILike(u.Email ?? "", pattern))
                    .OrderBy(u => u.UserName)
                    .Take(50)
                    .ToListAsync();

                var matchedIds = matchedUsers.Select(u => u.Id).ToList();
                var roleAssignments = await _context.UserRoles
                    .Where(ur => matchedIds.Contains(ur.UserId))
                    .Join(_context.Roles,
                        ur => ur.RoleId,
                        r => r.Id,
                        (ur, r) => new { ur.UserId, r.Name })
                    .ToListAsync();

                vm.SearchResults = matchedUsers.Select(u => new AdminUserListItemViewModel
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    IsAdmin = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.AdminRole),
                    IsPremium = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.PremiumRole),
                    IsSeller = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.SellerRole)
                }).ToList();
            }

            if (!string.IsNullOrEmpty(selectedUserId))
            {
                var user = await _userManager.FindByIdAsync(selectedUserId);
                if (user != null)
                {
                    vm.UserId = user.Id;
                    vm.Username = user.UserName ?? "";
                }
            }

            return View(vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> GiveUserRole(string userId, string roleName, string searchTerm)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (IsSelfAdminChange(userId, roleName))
            {
                TempData["StatusMessage"] = "\u26D4 You cannot change your own Admin role.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, selectedUserId = userId });
            }

            await _userManager.AddToRoleAsync(user, roleName);

            TempData["StatusMessage"] = $"\u2705 Added \"{roleName}\" to {user.UserName}.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> RemoveUserRole(string userId, string roleName, string searchTerm)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (IsSelfAdminChange(userId, roleName))
            {
                TempData["StatusMessage"] = "\u26D4 You cannot change your own Admin role.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, selectedUserId = userId });
            }

            await _userManager.RemoveFromRoleAsync(user, roleName);

            TempData["StatusMessage"] = $"\u2705 Removed \"{roleName}\" from {user.UserName}.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(string userId, string searchTerm)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var deletedUserName = user.UserName;
            await _userManager.DeleteAsync(user);

            TempData["StatusMessage"] = $"\uD83D\uDDD1️ Account \"{deletedUserName}\" was permanently deleted.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm });
        }

        private bool IsSelfAdminChange(string userId, string roleName)
        {
            return roleName == Helper.AdminRole && userId == _userManager.GetUserId(User);
        }
    }
}
