using System.Security.Claims;
using Marketplace.Models;
using Marketplace.Utility;

namespace Marketplace.Utility
{
    // Decides what contact info a viewer can see.
    // Hides, censors, or shows phone and email based on auth, ownership, and admin role.
    // Easy to extend later if you want more visibility levels.
    public static class ContactVisibilityHelper
    {
        // Hides most digits of a phone number, leaving last 3 visible.
        // Returns dots if the input is empty or has no digits.
        public static string CensorPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return "••••";
            }

            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.Length == 0)
            {
                return "••••";
            }

            if (digits.Length <= 4)
            {
                return new string('•', digits.Length);
            }

            return new string('•', digits.Length - 3) + digits[^3..];
        }

        // Hides the middle part of an email username.
        // Keeps first and last char visible and leaves domain untouched.
        public static string CensorEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                return "•••@•••";
            }

            var parts = email.Split('@', 2);
            var user = parts[0];
            var domain = parts[1];

            if (string.IsNullOrEmpty(user))
            {
                return $"•••@{domain}";
            }

            if (user.Length == 1)
            {
                return $"{user[0]}•••@{domain}";
            }

            if (user.Length == 2)
            {
                return $"{user[0]}•{user[1]}@{domain}";
            }

            return $"{user[0]}{new string('•', Math.Max(1, user.Length - 2))}{user[^1]}@{domain}";
        }

        // What the UI gets back for a contact field.
        public readonly record struct ContactView(string? Display, bool CanView, bool IsCensored, bool IsHidden);

        // Decides how to show the owner's phone to the current viewer.
        // Owner and admin see the real number, guests see censored, hidden stays hidden.
        public static ContactView ResolvePhone(ApplicationUser owner, ClaimsPrincipal viewer)
        {
            var raw = owner.PhoneNumber;
            var show = owner.ShowPhone;
            var isAuthed = viewer?.Identity?.IsAuthenticated == true;
            var isOwner = isAuthed && viewer != null && viewer.FindFirstValue(ClaimTypes.NameIdentifier) == owner.Id;
            var isAdmin = viewer?.IsInRole(Helper.AdminRole) == true;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new(null, false, false, true);
            }

            if (!show && !isOwner && !isAdmin)
            {
                return new(null, false, false, true);
            }

            if (isOwner || isAdmin)
            {
                return new(raw, true, false, false);
            }

            if (!isAuthed)
            {
                return new(CensorPhone(raw), false, true, false);
            }

            // Authenticated non-owner and show is true
            return new(raw, true, false, false);
        }

        // Decides how to show the owner's email to the current viewer.
        public static ContactView ResolveEmail(ApplicationUser owner, ClaimsPrincipal viewer)
        {
            var raw = owner.Email;
            var show = owner.ShowEmail;
            var isAuthed = viewer?.Identity?.IsAuthenticated == true;
            var isOwner = isAuthed && viewer != null && viewer.FindFirstValue(ClaimTypes.NameIdentifier) == owner.Id;
            var isAdmin = viewer?.IsInRole(Helper.AdminRole) == true;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new(null, false, false, true);
            }

            if (!show && !isOwner && !isAdmin)
            {
                return new(null, false, false, true);
            }

            if (isOwner || isAdmin)
            {
                return new(raw, true, false, false);
            }

            if (!isAuthed)
            {
                return new(CensorEmail(raw), false, true, false);
            }

            return new(raw, true, false, false);
        }

        // Overload for view models where owner data is already projected.
        // Same rules as above but takes raw strings instead of ApplicationUser.
        public static ContactView ResolvePhone(string? rawPhone, bool showPhone, ClaimsPrincipal viewer, string ownerId)
        {
            var isAuthed = viewer?.Identity?.IsAuthenticated == true;
            var isOwner = isAuthed && viewer != null && viewer.FindFirstValue(ClaimTypes.NameIdentifier) == ownerId;
            var isAdmin = viewer?.IsInRole(Helper.AdminRole) == true;

            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                return new(null, false, false, true);
            }

            if (!showPhone && !isOwner && !isAdmin)
            {
                return new(null, false, false, true);
            }

            if (isOwner || isAdmin)
            {
                return new(rawPhone, true, false, false);
            }

            if (!isAuthed)
            {
                return new(CensorPhone(rawPhone), false, true, false);
            }

            return new(rawPhone, true, false, false);
        }

        // Overload for email when only raw values are available.
        public static ContactView ResolveEmail(string? rawEmail, bool showEmail, ClaimsPrincipal viewer, string ownerId)
        {
            var isAuthed = viewer?.Identity?.IsAuthenticated == true;
            var isOwner = isAuthed && viewer != null && viewer.FindFirstValue(ClaimTypes.NameIdentifier) == ownerId;
            var isAdmin = viewer?.IsInRole(Helper.AdminRole) == true;

            if (string.IsNullOrWhiteSpace(rawEmail))
            {
                return new(null, false, false, true);
            }

            if (!showEmail && !isOwner && !isAdmin)
            {
                return new(null, false, false, true);
            }

            if (isOwner || isAdmin)
            {
                return new(rawEmail, true, false, false);
            }

            if (!isAuthed)
            {
                return new(CensorEmail(rawEmail), false, true, false);
            }

            return new(rawEmail, true, false, false);
        }
    }
}
