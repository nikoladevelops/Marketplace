namespace Marketplace.Models
{
    /// <summary>
    /// Account-level status, distinct from <see cref="Microsoft.AspNetCore.Identity.IdentityUser"/>
    /// lockout. Set by admins; enforced at login and on every request via middleware.
    /// </summary>
    public enum AccountStatus
    {
        Active = 0,
        Banned = 1
    }
}
