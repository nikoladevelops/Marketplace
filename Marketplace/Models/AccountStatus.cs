namespace Marketplace.Models
{
    // Just two states for an account. Simple and clear.
    // Active means normal, Banned means admin blocked them.
    // This is separate from lockout and is checked at login and on each request.
    public enum AccountStatus
    {
        Active = 0,

        Banned = 1
    }
}
