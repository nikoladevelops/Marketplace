using Marketplace.Models;
using Marketplace.Utility;
using Marketplace.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Services
{
    public enum AdminActionResult
    {
        Success,
        SelfModificationBlocked,
        TargetIsAdmin,
        AlreadyInTargetState,
        UserNotFound
    }

    public record AdminActionOutcome(AdminActionResult Result, string? Message = null);

    public class UserAdministrationService
    {
        public const int UsersPerPage = 20;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserAdministrationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<AdminPanelViewModel> SearchAsync(string? searchTerm, string? roleFilter, int pageNumber, string? selectedUserId, string? viewerId = null)
        {
            var list = await BuildUserListAsync(searchTerm, roleFilter, pageNumber);
            await PopulateSelectedAsync(list, selectedUserId, viewerId);
            return list;
        }

        public async Task<AdminPanelViewModel> SearchListAsync(string? searchTerm, string? roleFilter, int pageNumber)
        {
            // AJAX endpoint: only the list, no selected-user hydration.
            return await BuildUserListAsync(searchTerm, roleFilter, pageNumber);
        }

        public async Task<AdminActionOutcome> ChangeRoleAsync(string targetUserId, string roleName, string adminUserId)
        {
            if (targetUserId == adminUserId && roleName == Helper.AdminRole)
                return new(AdminActionResult.SelfModificationBlocked, "You cannot change your own Admin role.");

            var user = await _userManager.FindByIdAsync(targetUserId);
            if (user == null) return new(AdminActionResult.UserNotFound);

            var already = await _userManager.IsInRoleAsync(user, roleName);
            if (already) return new(AdminActionResult.AlreadyInTargetState, $"{user.UserName} already has the {roleName} role.");

            await _userManager.AddToRoleAsync(user, roleName);
            return new(AdminActionResult.Success, $"Added \"{roleName}\" to {user.UserName}.");
        }

        public async Task<AdminActionOutcome> RemoveRoleAsync(string targetUserId, string roleName, string adminUserId)
        {
            if (targetUserId == adminUserId && roleName == Helper.AdminRole)
                return new(AdminActionResult.SelfModificationBlocked, "You cannot change your own Admin role.");

            var user = await _userManager.FindByIdAsync(targetUserId);
            if (user == null) return new(AdminActionResult.UserNotFound);

            var has = await _userManager.IsInRoleAsync(user, roleName);
            if (!has) return new(AdminActionResult.AlreadyInTargetState, $"{user.UserName} does not have the {roleName} role.");

            await _userManager.RemoveFromRoleAsync(user, roleName);
            return new(AdminActionResult.Success, $"Removed \"{roleName}\" from {user.UserName}.");
        }

        public async Task<AdminActionOutcome> BanAsync(string targetUserId, string? reason, string adminUserId)
        {
            if (targetUserId == adminUserId)
                return new(AdminActionResult.SelfModificationBlocked, "You cannot ban your own account.");

            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null) return new(AdminActionResult.UserNotFound);

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
                return new(AdminActionResult.TargetIsAdmin, "Admins cannot be banned.");

            if (target.Status == AccountStatus.Banned)
                return new(AdminActionResult.AlreadyInTargetState, $"{target.UserName} is already banned.");

            target.Status = AccountStatus.Banned;
            target.BanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            target.BannedAtUtc = DateTime.UtcNow;
            target.BannedByUserId = adminUserId;
            // Rotate the security stamp so any active auth cookie is invalidated
            // and the BannedUserMiddleware kicks the user on the next request.
            await _userManager.UpdateSecurityStampAsync(target);

            _context.UserBanHistories.Add(new UserBanHistory
            {
                UserId = target.Id,
                AdminUserId = adminUserId,
                Action = "ban",
                Reason = target.BanReason,
                PerformedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return new(AdminActionResult.Success, $"{target.UserName} has been banned.");
        }

        public async Task<AdminActionOutcome> UnbanAsync(string targetUserId, string adminUserId)
        {
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null) return new(AdminActionResult.UserNotFound);

            if (target.Status == AccountStatus.Active)
                return new(AdminActionResult.AlreadyInTargetState, $"{target.UserName} is not banned.");

            target.Status = AccountStatus.Active;
            target.BanReason = null;
            target.BannedAtUtc = null;
            target.BannedByUserId = null;
            await _userManager.UpdateSecurityStampAsync(target);

            _context.UserBanHistories.Add(new UserBanHistory
            {
                UserId = target.Id,
                AdminUserId = adminUserId,
                Action = "unban",
                Reason = null,
                PerformedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return new(AdminActionResult.Success, $"{target.UserName} has been unbanned.");
        }

        public async Task<AdminActionOutcome> DeleteAsync(string targetUserId, string adminUserId)
        {
            if (targetUserId == adminUserId)
                return new(AdminActionResult.SelfModificationBlocked, "You cannot delete your own account.");

            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null) return new(AdminActionResult.UserNotFound);

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
                return new(AdminActionResult.TargetIsAdmin, "Admins cannot be deleted. Demote them first.");

            var deletedUserName = target.UserName;

            var banHistoryRows = await _context.UserBanHistories
                .Where(h => h.UserId == targetUserId || h.AdminUserId == targetUserId)
                .ToListAsync();
            if (banHistoryRows.Count > 0)
            {
                _context.UserBanHistories.RemoveRange(banHistoryRows);
                await _context.SaveChangesAsync();
            }

            await _userManager.DeleteAsync(target);
            return new(AdminActionResult.Success, $"Account \"{deletedUserName}\" was permanently deleted.");
        }

        private async Task<AdminPanelViewModel> BuildUserListAsync(string? searchTerm, string? roleFilter, int pageNumber)
        {
            searchTerm = (searchTerm ?? "").Trim();
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
                query = from u in query
                        join ur in _context.UserRoles on u.Id equals ur.UserId
                        join r in _context.Roles on ur.RoleId equals r.Id
                        where r.Name == roleName
                        select u;
            }

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

        private async Task PopulateSelectedAsync(AdminPanelViewModel vm, string? selectedUserId, string? viewerId)
        {
            if (string.IsNullOrEmpty(selectedUserId)) return;
            var user = await _userManager.FindByIdAsync(selectedUserId);
            if (user == null) return;

            vm.UserId = user.Id;
            vm.Username = user.UserName ?? "";
            vm.IsTargetAdmin = await _userManager.IsInRoleAsync(user, Helper.AdminRole);
            vm.IsTargetSelf = viewerId != null && user.Id == viewerId;
            vm.TargetStatus = user.Status;
            vm.TargetBanReason = user.BanReason;
        }

        private static string NormalizeRoleFilter(string? roleFilter) => roleFilter switch
        {
            "admin" or "premium" or "seller" => roleFilter,
            _ => "all"
        };

        private static string MapRoleFilterToName(string roleFilter) => roleFilter switch
        {
            "admin" => Helper.AdminRole,
            "premium" => Helper.PremiumRole,
            "seller" => Helper.SellerRole,
            _ => ""
        };
    }
}
