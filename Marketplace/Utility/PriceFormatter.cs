using System.Globalization;

namespace Marketplace.Utility
{
    // Formats prices nicely for display.
    // Uses Bulgarian formatting with two decimals and a Euro sign.
    public static class PriceFormatter
    {
        private static readonly CultureInfo BgCulture = new("bg-BG");

        // Formats a decimal price like "1 234,56 Euro".
        public static string ToEur(decimal price)
        {
            return price.ToString("N2", BgCulture) + " \u20AC";
        }

        // Formats a double price the same way.
        // Converts to decimal first to keep formatting consistent.
        public static string ToEur(double price)
        {
            return ((decimal)price).ToString("N2", BgCulture) + " \u20AC";
        }
    }
}
