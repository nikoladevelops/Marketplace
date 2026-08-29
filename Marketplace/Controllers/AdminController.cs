using Marketplace.Models;
using Marketplace.Services;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Controllers
{
    // AdminController - only for admins, lets you manage users, roles, bans and deletes.
    [Authorize(Roles = Helper.AdminRole)]
    public class AdminController : Controller
    {
        private readonly UserAdministrationService _admin;
        private readonly ChatService _chat;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        // Constructor - plugs in the admin and chat services plus user manager.
        public AdminController(UserAdministrationService admin, ChatService chat, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _admin = admin;
            _chat = chat;
            _userManager = userManager;
        }

        // AdminPanel - main admin page with search, role, report and blocked filters plus paging.
        public async Task<IActionResult> AdminPanel(string searchTerm, string roleFilter = "all", int pageNumber = 0, string selectedUserId = null, string reportFilter = "all", string blockedFilter = "all")
        {
            var meId = _userManager.GetUserId(User);

            var vm = await _admin.SearchAsync(searchTerm, roleFilter, pageNumber, selectedUserId, meId, reportFilter, blockedFilter);

            return View(vm);
        }

        // SearchUsers - returns a partial list of users for live search on the admin panel.
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm, string roleFilter = "all", int pageNumber = 0, string reportFilter = "all", string blockedFilter = "all")
        {
            var vm = await _admin.SearchListAsync(searchTerm, roleFilter, pageNumber, reportFilter, blockedFilter);

            return PartialView("_AdminUserList", vm);
        }

        // Reports - show reports for a user, newest first.
        [HttpGet]
        public async Task<IActionResult> Reports(string userId)
        {
            var reports = await _admin.GetReportsForUserAsync(userId);

            return PartialView("_ReportList", reports);
        }

        // ChatLog - admin view of the exact chat between reporter and reported user.
        // Bypasses blocks - read only, for audit. Only for reported threads.
        [HttpGet]
        [Authorize(Roles = Helper.AdminRole)]
        public async Task<IActionResult> ChatLog(int reportId)
        {
            var dto = await _chat.GetAdminChatLogForReportAsync(reportId);

            if (dto == null)
            {
                return NotFound();
            }

            return PartialView("_ChatLog", dto);
        }

        // ResolveReport - admin marks a report as resolved (dismissed or banned).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveReport(int reportId, string actionType, string searchTerm, string roleFilter, int pageNumber, string reportFilter = "all", string blockedFilter = "all", string selectedUserId = null)
        {
            var adminId = _userManager.GetUserId(User) ?? "";

            var action = actionType == "ban" ? ReportAction.Banned : ReportAction.Dismissed;

            await _admin.ResolveReportAsync(reportId, adminId, action);

            // If banning, also ban the reported user with the report description as reason.
            if (action == ReportAction.Banned)
            {
                var report = await _admin.GetReportByIdAsync(reportId);

                if (report != null)
                {
                    await _admin.BanAsync(report.ReportedUserId, report.Description, adminId);
                }
            }

            TempData["StatusMessage"] = action == ReportAction.Banned ? "Report resolved and user banned." : "Report dismissed.";

            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId, reportFilter, blockedFilter });
        }

        // GiveUserRole - gives a user a new role like admin or premium.
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> GiveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";

            var outcome = await _admin.ChangeRoleAsync(userId, roleName, adminId);

            TempData["StatusMessage"] = outcome.Message;

            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        // RemoveUserRole - takes a role away from a user.
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> RemoveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";

            var outcome = await _admin.RemoveRoleAsync(userId, roleName, adminId);

            TempData["StatusMessage"] = outcome.Message;

            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        // DeleteAccount - permanently removes a user account.
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(string userId, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";

            var outcome = await _admin.DeleteAsync(userId, adminId);

            TempData["StatusMessage"] = outcome.Message;

            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        // BanUser - bans a user with an optional reason.
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> BanUser(string userId, string? reason, string searchTerm, string roleFilter, int pageNumber)
        {
            var adminId = _userManager.GetUserId(User) ?? "";

            var outcome = await _admin.BanAsync(userId, reason, adminId);

            TempData["StatusMessage"] = outcome.Message;

            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        // UnbanUser - lifts a ban so the user can log in again.
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
