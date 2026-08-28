using Microsoft.AspNetCore.Identity;

namespace Marketplace.Models
{
    // This is our user model. It extends the built in Identity user with extra fields.
    // Keeps profile info and ban info together in one place.
    public class ApplicationUser : IdentityUser
    {
        // Optional profile picture path
        public string? ProfilePicturePath { get; set; }

        // Short bio or description shown on profile
        public string? Description { get; set; }

        // If true, other users can see your email. False by default to avoid spam.
        public bool ShowEmail { get; set; } = false;

        // If true, other users can see your phone number. Also false by default.
        public bool ShowPhone { get; set; } = false;

        // Account status - admins can ban or keep active. Banned users cannot sign in.
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        // Why the user was banned, if they are banned
        public string? BanReason { get; set; }

        // When the ban happened (UTC)
        public DateTime? BannedAtUtc { get; set; }

        // Which admin performed the ban
        public string? BannedByUserId { get; set; }

        // Navigation to the admin who banned this user
        public ApplicationUser? BannedByUser { get; set; }
    }
}
