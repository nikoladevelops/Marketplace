using System.Globalization;

namespace Marketplace.Utility
{
    public static class DateFormatter
    {
        public static CultureInfo GetCulture(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return CultureInfo.InvariantCulture;

            var loc = location.ToLowerInvariant();

            if (loc.Contains("bulgaria") || loc.Contains("sofia") || loc.Contains("plovdiv") ||
                loc.Contains("varna") || loc.Contains("burgas") || loc.Contains("ruse") ||
                loc.Contains("stara zagora") || loc.Contains("pleven"))
                return new CultureInfo("bg-BG");

            if (loc.Contains("usa") || loc.Contains("united states"))
                return new CultureInfo("en-US");

            return new CultureInfo("en-GB");
        }

        private static DateTime ToLocationTime(DateTime utc, string? location)
        {
            if (utc.Kind == DateTimeKind.Unspecified)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

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

        public static string ToShortLocationDate(DateTime utc, string? location)
        {
            var local = ToLocationTime(utc, location);
            var culture = GetCulture(location);
            return local.ToString("d", culture);
        }

        public static string ToLongLocationDate(DateTime utc, string? location)
        {
            var local = ToLocationTime(utc, location);
            var culture = GetCulture(location);
            return local.ToString("f", culture);
        }
    }
}
