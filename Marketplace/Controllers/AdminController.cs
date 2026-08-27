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
        private const int UsersPerPage = 20;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> AdminPanel(string searchTerm, string roleFilter = "all", int pageNumber = 0, string selectedUserId = null)
        {
            var vm = await BuildUserListAsync(searchTerm, roleFilter, pageNumber);

            if (!string.IsNullOrEmpty(selectedUserId))
            {
                var user = await _userManager.FindByIdAsync(selectedUserId);
                if (user != null)
                {
                    vm.UserId = user.Id;
                    vm.Username = user.UserName ?? "";
                    vm.IsTargetAdmin = await _userManager.IsInRoleAsync(user, Helper.AdminRole);
                    vm.IsTargetSelf = user.Id == _userManager.GetUserId(User);
                    vm.TargetStatus = user.Status;
                    vm.TargetBanReason = user.BanReason;
                }
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm, string roleFilter = "all", int pageNumber = 0)
        {
            var vm = await BuildUserListAsync(searchTerm, roleFilter, pageNumber);
            return PartialView("_AdminUserList", vm);
        }

        private async Task<AdminPanelViewModel> BuildUserListAsync(string searchTerm, string roleFilter, int pageNumber)
        {
            searchTerm = searchTerm?.Trim() ?? "";
            roleFilter = NormalizeRoleFilter(roleFilter);

            IQueryable<ApplicationUser> query = _context.Users.AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var pattern = $"%{searchTerm}%";
                query = query.Where(u => EF.Functions.ILike(u.UserName ?? "", pattern)
                                      || EF.Functions.ILike(u.Email ?? "", pattern));
            }

            if (roleFilter != "all")
            {
                var roleName = MapRoleFilterToName(roleFilter);
                // Single join: filter users by membership in the given role.
                // Translates to a single INNER JOIN against UserRoles + Roles.
                query = from u in query
                        join ur in _context.UserRoles on u.Id equals ur.UserId
                        join r in _context.Roles on ur.RoleId equals r.Id
                        where r.Name == roleName
                        select u;
            }

            // Distinct in case the same user has duplicate role assignments (defensive).
            query = query.Distinct();

            var totalCount = await query.CountAsync();
            var maxCountPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / UsersPerPage);
            pageNumber = Math.Clamp(pageNumber, 0, Math.Max(0, maxCountPages - 1));

            var pagedUsers = await query
                .OrderBy(u => u.UserName)
                .Skip(pageNumber * UsersPerPage)
                .Take(UsersPerPage)
                .ToListAsync();

            var pagedUserIds = pagedUsers.Select(u => u.Id).ToList();

            var roleAssignments = await _context.UserRoles
                .Where(ur => pagedUserIds.Contains(ur.UserId))
                .Join(_context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, r.Name })
                .ToListAsync();

            return new AdminPanelViewModel
            {
                SearchTerm = searchTerm,
                RoleFilter = roleFilter,
                PageNumber = pageNumber,
                MaxCountPages = maxCountPages,
                PageSize = UsersPerPage,
                TotalCount = totalCount,
                SearchResults = pagedUsers.Select(u => new AdminUserListItemViewModel
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    IsAdmin = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.AdminRole),
                    IsPremium = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.PremiumRole),
                    IsSeller = roleAssignments.Any(ra => ra.UserId == u.Id && ra.Name == Helper.SellerRole),
                    Status = u.Status,
                    BanReason = u.BanReason,
                    BannedAtUtc = u.BannedAtUtc
                }).ToList()
            };
        }

        private static string NormalizeRoleFilter(string roleFilter)
        {
            return roleFilter switch
            {
                "admin" or "premium" or "seller" => roleFilter,
                _ => "all"
            };
        }

        private static string MapRoleFilterToName(string roleFilter) => roleFilter switch
        {
            "admin" => Helper.AdminRole,
            "premium" => Helper.PremiumRole,
            "seller" => Helper.SellerRole,
            _ => ""
        };

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> GiveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (IsSelfAdminChange(userId, roleName))
            {
                TempData["StatusMessage"] = "\u26D4 You cannot change your own Admin role.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            await _userManager.AddToRoleAsync(user, roleName);

            TempData["StatusMessage"] = $"\u2705 Added \"{roleName}\" to {user.UserName}.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> RemoveUserRole(string userId, string roleName, string searchTerm, string roleFilter, int pageNumber)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (IsSelfAdminChange(userId, roleName))
            {
                TempData["StatusMessage"] = "\u26D4 You cannot change your own Admin role.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            await _userManager.RemoveFromRoleAsync(user, roleName);

            TempData["StatusMessage"] = $"\u2705 Removed \"{roleName}\" from {user.UserName}.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAccount(string userId, string searchTerm, string roleFilter, int pageNumber)
        {
            var meId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(meId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Admins (including yourself) cannot be deleted from the admin
            // panel. Demote first, then delete from a non-admin state — or
            // delete directly via the DB if an account truly needs to go.
            if (userId == meId)
            {
                TempData["StatusMessage"] = "\u26D4 You cannot delete your own account.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }
            if (await _userManager.IsInRoleAsync(user, Helper.AdminRole))
            {
                TempData["StatusMessage"] = "\u26D4 Admins cannot be deleted. Demote them first.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            var deletedUserName = user.UserName;

            // UserBanHistories.UserId and UserBanHistories.AdminUserId both have
            // ON DELETE RESTRICT (we don't want to silently cascade-delete the
            // audit trail of *other* admins who banned users when one admin is
            // deleted). So we explicitly remove rows that reference the user
            // being deleted before calling UserManager.DeleteAsync. Audit rows
            // for the same user id are dropped here, which is the right call
            // for a permanent account deletion.
            var banHistoryRows = await _context.UserBanHistories
                .Where(h => h.UserId == userId || h.AdminUserId == userId)
                .ToListAsync();
            if (banHistoryRows.Count > 0)
            {
                _context.UserBanHistories.RemoveRange(banHistoryRows);
                await _context.SaveChangesAsync();
            }

            await _userManager.DeleteAsync(user);

            TempData["StatusMessage"] = $"\uD83D\uDDD1️ Account \"{deletedUserName}\" was permanently deleted.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber });
        }

        private bool IsSelfAdminChange(string userId, string roleName)
        {
            return roleName == Helper.AdminRole && userId == _userManager.GetUserId(User);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> BanUser(string userId, string? reason, string searchTerm, string roleFilter, int pageNumber)
        {
            var meId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(meId)) return Challenge();

            if (userId == meId)
            {
                TempData["StatusMessage"] = "\u26D4 You cannot ban your own account.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null) return NotFound();

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
            {
                TempData["StatusMessage"] = "\u26D4 Admins cannot be banned.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            if (target.Status == AccountStatus.Banned)
            {
                TempData["StatusMessage"] = $"\u26A0\uFE0F {target.UserName} is already banned.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            target.Status = AccountStatus.Banned;
            target.BanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            target.BannedAtUtc = DateTime.UtcNow;
            target.BannedByUserId = meId;
            // Rotate the security stamp so the auth cookie for any active session
            // is invalidated and the next request is bounced by the middleware.
            await _userManager.UpdateSecurityStampAsync(target);

            _context.UserBanHistories.Add(new UserBanHistory
            {
                UserId = target.Id,
                AdminUserId = meId,
                Action = "ban",
                Reason = target.BanReason,
                PerformedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"\uD83D\uDEAB {target.UserName} has been banned.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> UnbanUser(string userId, string searchTerm, string roleFilter, int pageNumber)
        {
            var meId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(meId)) return Challenge();

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null) return NotFound();

            if (target.Status == AccountStatus.Active)
            {
                TempData["StatusMessage"] = $"\u26A0\uFE0F {target.UserName} is not banned.";
                return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
            }

            target.Status = AccountStatus.Active;
            target.BanReason = null;
            target.BannedAtUtc = null;
            target.BannedByUserId = null;
            await _userManager.UpdateSecurityStampAsync(target);

            _context.UserBanHistories.Add(new UserBanHistory
            {
                UserId = target.Id,
                AdminUserId = meId,
                Action = "unban",
                Reason = null,
                PerformedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"\u2705 {target.UserName} has been unbanned.";
            return RedirectToAction(nameof(AdminPanel), new { searchTerm, roleFilter, pageNumber, selectedUserId = userId });
        }
    }
}
