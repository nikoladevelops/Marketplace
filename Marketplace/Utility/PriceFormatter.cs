using System.Globalization;

namespace Marketplace.Utility
{
    public static class PriceFormatter
    {
        private static readonly CultureInfo BgCulture = new("bg-BG");

        public static string ToEur(decimal price)
        {
            return price.ToString("N2", BgCulture) + " \u20AC";
        }

        public static string ToEur(double price)
        {
            return ((decimal)price).ToString("N2", BgCulture) + " \u20AC";
        }
    }
}
