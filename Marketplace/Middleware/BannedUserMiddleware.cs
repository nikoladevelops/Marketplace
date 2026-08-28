using System.Security.Claims;
using Marketplace.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Middleware
{
    // Checks every request if the logged in user is banned or deleted.
    // If so, signs them out and redirects to the right page.
    // Runs after authentication so User is already set.
    public class BannedUserMiddleware
    {
        private readonly RequestDelegate _next;

        // Paths that should never redirect, to avoid loops.
        private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/Account/Banned",
            "/Account/LogOut",
            "/Account/Login",
            "/Account/Register"
        };

        // Creates the middleware with the next step in the pipeline.
        public BannedUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Runs on each request. Looks up the user status fresh from the DB.
        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var path = context.Request.Path.Value ?? "";

                if (!IsExempt(path))
                {
                    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!string.IsNullOrEmpty(userId))
                    {
                        // Look up the user from the DB so we always see the latest state.
                        // An admin may have banned or deleted the account since the cookie
                        // was issued, and the SecurityStamp validator runs on an interval
                        // (default 30 min) so we want a tighter guarantee.
                        //
                        // Single round-trip: a nullable projection of the status. A missing
                        // user surfaces as null, which tells us it was deleted.

                        var status = await userManager.Users
                            .Where(u => u.Id == userId)
                            .Select(u => (AccountStatus?)u.Status)
                            .FirstOrDefaultAsync();

                        if (status == null)
                        {
                            // Account was deleted while the cookie was still valid.
                            await signInManager.SignOutAsync();
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Account/Login");

                            return;
                        }

                        if (status == AccountStatus.Banned)
                        {
                            await signInManager.SignOutAsync();

                            // Defensive: also clear the auth cookie directly.
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Account/Banned");

                            return;
                        }
                    }
                }
            }

            await _next(context);
        }

        // Returns true if the path should be skipped by this middleware.
        private static bool IsExempt(string path)
        {
            if (ExemptPaths.Contains(path))
            {
                return true;
            }

            // Let static files and error pages through so styling still loads on /Account/Banned.
            if (path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/img/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
