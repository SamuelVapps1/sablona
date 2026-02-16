using System.Globalization;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Euro locale: decimal comma, € symbol.
    /// </summary>
    public static class Formatting
    {
        private static readonly CultureInfo EuroCulture = new CultureInfo("sk-SK"); // Slovak uses comma + €

        public static string FormatPrice(decimal? price)
        {
            if (!price.HasValue) return "";
            return price.Value.ToString("N2", EuroCulture) + " €";
        }

        public static string FormatUnitPrice(decimal? pricePerKg)
        {
            if (!pricePerKg.HasValue) return "";
            return "1 kg = " + pricePerKg.Value.ToString("N2", EuroCulture) + " €";
        }
    }
}
