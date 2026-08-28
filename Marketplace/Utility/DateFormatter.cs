using System.Globalization;

namespace Marketplace.Utility
{
    // Helps show dates in the right language and timezone for the ad location.
    // Picks a culture based on location text and converts UTC to local time.
    public static class DateFormatter
    {
        // Picks a culture from the location string.
        // Looks for known city and country names, defaults to en-GB.
        public static CultureInfo GetCulture(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return CultureInfo.InvariantCulture;
            }

            var loc = location.ToLowerInvariant();

            if (loc.Contains("bulgaria") || loc.Contains("sofia") || loc.Contains("plovdiv") ||
                loc.Contains("varna") || loc.Contains("burgas") || loc.Contains("ruse") ||
                loc.Contains("stara zagora") || loc.Contains("pleven"))
            {
                return new CultureInfo("bg-BG");
            }

            if (loc.Contains("usa") || loc.Contains("united states"))
            {
                return new CultureInfo("en-US");
            }

            return new CultureInfo("en-GB");
        }

        // Converts a UTC date to the timezone that matches the location.
        // For Bulgaria it tries FLE Standard Time, otherwise keeps UTC.
        private static DateTime ToLocationTime(DateTime utc, string? location)
        {
            if (utc.Kind == DateTimeKind.Unspecified)
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }

            var culture = GetCulture(location);

            if (culture.Name == "bg-BG")
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

                    return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
                }
                catch
                {
                    return utc.ToLocalTime();
                }
            }

            return utc;
        }

        // Shows just the date part, formatted for the location.
        public static string ToShortLocationDate(DateTime utc, string? location)
        {
            var local = ToLocationTime(utc, location);
            var culture = GetCulture(location);

            return local.ToString("d", culture);
        }

        // Shows date and time in a longer friendly format for the location.
        public static string ToLongLocationDate(DateTime utc, string? location)
        {
            var local = ToLocationTime(utc, location);
            var culture = GetCulture(location);

            return local.ToString("f", culture);
        }
    }
}
