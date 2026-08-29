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

    // UserAdministrationService - admin tools for roles, bans and user search.
    public class UserAdministrationService
    {
        public const int UsersPerPage = 20;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // UserAdministrationService - set up DB and user manager.
        public UserAdministrationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // SearchAsync - search users and also load the selected user detail.
        public async Task<AdminPanelViewModel> SearchAsync(string? searchTerm, string? roleFilter, int pageNumber, string? selectedUserId, string? viewerId = null, string? reportFilter = "all", string? blockedFilter = "all")
        {
            var list = await BuildUserListAsync(searchTerm, roleFilter, pageNumber, reportFilter, blockedFilter);

            await PopulateSelectedAsync(list, selectedUserId, viewerId);

            return list;
        }

        // SearchListAsync - search users for AJAX, list only, no selected user.
        public async Task<AdminPanelViewModel> SearchListAsync(string? searchTerm, string? roleFilter, int pageNumber, string? reportFilter = "all", string? blockedFilter = "all")
        {
            // AJAX endpoint: only the list, no selected-user hydration.

            return await BuildUserListAsync(searchTerm, roleFilter, pageNumber, reportFilter, blockedFilter);
        }

        // GetReportsForUserAsync - returns all reports for a user, newest first.
        public async Task<List<ChatReport>> GetReportsForUserAsync(string userId)
        {
            return await _context.ChatReports
                .AsNoTracking()
                .Where(r => r.ReportedUserId == userId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Include(r => r.Reporter)
                .Include(r => r.ReportedUser)
                .Include(r => r.Advertisement)
                .ToListAsync();
        }

        public async Task<ChatReport?> GetReportByIdAsync(int reportId)
        {
            return await _context.ChatReports.FindAsync(reportId);
        }

        // ResolveReportAsync - admin resolves a report with an action.
        public async Task<bool> ResolveReportAsync(int reportId, string adminId, ReportAction action)
        {
            var report = await _context.ChatReports.FindAsync(reportId);

            if (report == null)
            {
                return false;
            }

            if (report.Status == ReportStatus.Resolved)
            {
                return false;
            }

            report.Status = ReportStatus.Resolved;
            report.ReviewedByAdminId = adminId;
            report.ReviewedAtUtc = DateTime.UtcNow;
            report.ActionTaken = action;

            await _context.SaveChangesAsync();

            return true;
        }

        // ChangeRoleAsync - give a user a new role.
        public async Task<AdminActionOutcome> ChangeRoleAsync(string targetUserId, string roleName, string adminUserId)
        {
            if (targetUserId == adminUserId && roleName == Helper.AdminRole)
            {
                return new(AdminActionResult.SelfModificationBlocked, "You cannot change your own Admin role.");
            }

            var user = await _userManager.FindByIdAsync(targetUserId);

            if (user == null)
            {
                return new(AdminActionResult.UserNotFound);
            }

            var already = await _userManager.IsInRoleAsync(user, roleName);

            if (already)
            {
                return new(AdminActionResult.AlreadyInTargetState, $"{user.UserName} already has the {roleName} role.");
            }

            await _userManager.AddToRoleAsync(user, roleName);

            return new(AdminActionResult.Success, $"Added \"{roleName}\" to {user.UserName}.");
        }

        // RemoveRoleAsync - take a role away from a user.
        public async Task<AdminActionOutcome> RemoveRoleAsync(string targetUserId, string roleName, string adminUserId)
        {
            if (targetUserId == adminUserId && roleName == Helper.AdminRole)
            {
                return new(AdminActionResult.SelfModificationBlocked, "You cannot change your own Admin role.");
            }

            var user = await _userManager.FindByIdAsync(targetUserId);

            if (user == null)
            {
                return new(AdminActionResult.UserNotFound);
            }

            var has = await _userManager.IsInRoleAsync(user, roleName);

            if (!has)
            {
                return new(AdminActionResult.AlreadyInTargetState, $"{user.UserName} does not have the {roleName} role.");
            }

            await _userManager.RemoveFromRoleAsync(user, roleName);

            return new(AdminActionResult.Success, $"Removed \"{roleName}\" from {user.UserName}.");
        }

        // BanAsync - ban a user and log it, blocks self-ban and admin ban.
        public async Task<AdminActionOutcome> BanAsync(string targetUserId, string? reason, string adminUserId)
        {
            if (targetUserId == adminUserId)
            {
                return new(AdminActionResult.SelfModificationBlocked, "You cannot ban your own account.");
            }

            var target = await _userManager.FindByIdAsync(targetUserId);

            if (target == null)
            {
                return new(AdminActionResult.UserNotFound);
            }

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
            {
                return new(AdminActionResult.TargetIsAdmin, "Admins cannot be banned.");
            }

            if (target.Status == AccountStatus.Banned)
            {
                return new(AdminActionResult.AlreadyInTargetState, $"{target.UserName} is already banned.");
            }

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

        // UnbanAsync - lift a ban and log it.
        public async Task<AdminActionOutcome> UnbanAsync(string targetUserId, string adminUserId)
        {
            if (targetUserId == adminUserId)
            {
                return new(AdminActionResult.SelfModificationBlocked, "You cannot unban your own account.");
            }

            var target = await _userManager.FindByIdAsync(targetUserId);

            if (target == null)
            {
                return new(AdminActionResult.UserNotFound);
            }

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
            {
                return new(AdminActionResult.TargetIsAdmin, "Admins cannot be unbanned - they are never banned.");
            }

            if (target.Status == AccountStatus.Active)
            {
                return new(AdminActionResult.AlreadyInTargetState, $"{target.UserName} is not banned.");
            }

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

        // DeleteAsync - permanently delete a user, cleans ban history first.
        public async Task<AdminActionOutcome> DeleteAsync(string targetUserId, string adminUserId)
        {
            if (targetUserId == adminUserId)
            {
                return new(AdminActionResult.SelfModificationBlocked, "You cannot delete your own account.");
            }

            var target = await _userManager.FindByIdAsync(targetUserId);

            if (target == null)
            {
                return new(AdminActionResult.UserNotFound);
            }

            if (await _userManager.IsInRoleAsync(target, Helper.AdminRole))
            {
                return new(AdminActionResult.TargetIsAdmin, "Admins cannot be deleted. Demote them first.");
            }

            var deletedUserName = target.UserName;

            // Use a transaction so we never leave orphaned audit rows if the user delete fails.
            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // Clean ban history first (Restrict).

                var banHistoryRows = await _context.UserBanHistories
                    .Where(h => h.UserId == targetUserId || h.AdminUserId == targetUserId)
                    .ToListAsync();

                if (banHistoryRows.Count > 0)
                {
                    _context.UserBanHistories.RemoveRange(banHistoryRows);
                }

                // If the deleted user was an admin who reviewed reports, clear the reviewer reference (SetNull).

                var reviewedReports = await _context.ChatReports
                    .Where(r => r.ReviewedByAdminId == targetUserId)
                    .ToListAsync();

                foreach (var r in reviewedReports)
                {
                    r.ReviewedByAdminId = null;
                }

                // Clean chat reports that reference this user directly or via their ads (Restrict on Reporter/Reported/Advertisement).

                var adIdsForUser = await _context.Advertisements
                    .Where(a => a.UserId == targetUserId)
                    .Select(a => a.Id)
                    .ToListAsync();

                var reportsForUser = await _context.ChatReports
                    .Where(r => r.ReporterId == targetUserId || r.ReportedUserId == targetUserId || adIdsForUser.Contains(r.AdvertisementId))
                    .ToListAsync();

                if (reportsForUser.Count > 0)
                {
                    _context.ChatReports.RemoveRange(reportsForUser);
                }

                // Clean chat messages where user participated (Cascade would wipe but we do explicitly for clarity).

                var messagesForUser = await _context.ChatMessages
                    .Where(m => m.SenderId == targetUserId || m.ReceiverId == targetUserId)
                    .ToListAsync();

                if (messagesForUser.Count > 0)
                {
                    _context.ChatMessages.RemoveRange(messagesForUser);
                }

                // Clean blocks where user is blocker or blocked.

                var blocksForUser = await _context.UserBlocks
                    .Where(b => b.BlockerId == targetUserId || b.BlockedId == targetUserId)
                    .ToListAsync();

                if (blocksForUser.Count > 0)
                {
                    _context.UserBlocks.RemoveRange(blocksForUser);
                }

                await _context.SaveChangesAsync();

                // Finally delete the user - advertisements will cascade via UserId.
                var deleteResult = await _userManager.DeleteAsync(target);

                if (!deleteResult.Succeeded)
                {
                    await tx.RollbackAsync();

                    return new(AdminActionResult.UserNotFound, $"Could not delete \"{deletedUserName}\".");
                }

                await tx.CommitAsync();
            }
            catch (Exception)
            {
                await tx.RollbackAsync();

                throw;
            }

            return new(AdminActionResult.Success, $"Account \"{deletedUserName}\" was permanently deleted.");
        }

        // BuildUserListAsync - builds paged user list with search, role, report and blocked filters.
        private async Task<AdminPanelViewModel> BuildUserListAsync(string? searchTerm, string? roleFilter, int pageNumber, string? reportFilter = "all", string? blockedFilter = "all")
        {
            searchTerm = (searchTerm ?? "").Trim();
            roleFilter = NormalizeRoleFilter(roleFilter);
            reportFilter = NormalizeReportFilter(reportFilter);
            blockedFilter = NormalizeBlockedFilter(blockedFilter);

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

            // Filter by reported state - total reports count is used later, but we filter here.
            if (reportFilter == "reported")
            {
                query = query.Where(u => _context.ChatReports.Any(r => r.ReportedUserId == u.Id));
            }

            if (blockedFilter == "blocked")
            {
                query = query.Where(u => _context.UserBlocks.Any(b => b.BlockedId == u.Id));
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

            // Counts for badges - total reports and blocked by count
            var reportCounts = await _context.ChatReports
                .Where(r => pagedUserIds.Contains(r.ReportedUserId))
                .GroupBy(r => r.ReportedUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var blockedCounts = await _context.UserBlocks
                .Where(b => pagedUserIds.Contains(b.BlockedId))
                .GroupBy(b => b.BlockedId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            return new AdminPanelViewModel
            {
                SearchTerm = searchTerm,
                RoleFilter = roleFilter,
                ReportFilter = reportFilter,
                BlockedFilter = blockedFilter,
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
                    BannedAtUtc = u.BannedAtUtc,
                    ReportCount = reportCounts.GetValueOrDefault(u.Id, 0),
                    BlockedByCount = blockedCounts.GetValueOrDefault(u.Id, 0)
                }).ToList()
            };
        }

        // PopulateSelectedAsync - fills in the selected user panel.
        private async Task PopulateSelectedAsync(AdminPanelViewModel vm, string? selectedUserId, string? viewerId)
        {
            if (string.IsNullOrEmpty(selectedUserId))
            {
                return;
            }

            var user = await _userManager.FindByIdAsync(selectedUserId);

            if (user == null)
            {
                return;
            }

            vm.UserId = user.Id;
            vm.Username = user.UserName ?? "";
            vm.IsTargetAdmin = await _userManager.IsInRoleAsync(user, Helper.AdminRole);
            vm.IsTargetSelf = viewerId != null && user.Id == viewerId;
            vm.TargetStatus = user.Status;
            vm.TargetBanReason = user.BanReason;
        }

        // NormalizeRoleFilter - keep only known filters, else "all".
        private static string NormalizeRoleFilter(string? roleFilter) => roleFilter switch
        {
            "admin" or "premium" or "seller" => roleFilter,
            _ => "all"
        };

        private static string NormalizeReportFilter(string? reportFilter) => reportFilter switch
        {
            "reported" => "reported",
            _ => "all"
        };

        private static string NormalizeBlockedFilter(string? blockedFilter) => blockedFilter switch
        {
            "blocked" => "blocked",
            _ => "all"
        };

        // MapRoleFilterToName - turns filter string into real role name.
        private static string MapRoleFilterToName(string roleFilter) => roleFilter switch
        {
            "admin" => Helper.AdminRole,
            "premium" => Helper.PremiumRole,
            "seller" => Helper.SellerRole,
            _ => ""
        };
    }
}
