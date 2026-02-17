using System;
using System.Globalization;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Euro locale: decimal comma, € symbol.
    /// </summary>
    public static class Formatting
    {
        private static readonly CultureInfo EuroCulture = new CultureInfo("sk-SK"); // Slovak uses comma + €

        /// <summary>
        /// Price: 2 decimals, decimal comma, € symbol.
        /// </summary>
        public static string FormatPrice(decimal? price)
        {
            if (!price.HasValue) return "";
            var rounded = decimal.Round(price.Value, 2, MidpointRounding.AwayFromZero);
            return rounded.ToString("N2", EuroCulture) + " €";
        }

        /// <summary>
        /// Unit price: 2 decimals, decimal comma, € symbol.
        /// </summary>
        public static string FormatUnitPrice(decimal? pricePerKg)
        {
            if (!pricePerKg.HasValue) return "";
            var rounded = decimal.Round(pricePerKg.Value, 2, MidpointRounding.AwayFromZero);
            return "1 kg = " + rounded.ToString("N2", EuroCulture) + " €";
        }
    }
}
