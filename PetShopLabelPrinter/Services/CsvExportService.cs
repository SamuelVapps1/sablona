using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Services
{
    /// <summary>
    /// CSV export for Alfa+ interoperability.
    /// </summary>
    public static class CsvExportService
    {
        public static readonly string[] Columns = { "Name", "Price", "SKU", "EAN", "ExpiryDate", "BarcodeEnabled", "BarcodeValue", "BarcodeFormat" };

        /// <summary>Escape field for CSV (wrap in quotes if contains delimiter or quote).</summary>
        private static string Escape(string? value, char delimiter)
        {
            if (value == null) return "";
            if (value.IndexOf(delimiter) >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>Export products to CSV file. Returns count of exported rows.</summary>
        public static int ExportToFile(IReadOnlyList<Product> products, string path, char delimiter = ';')
        {
            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            writer.WriteLine(string.Join(delimiter.ToString(), Columns));
            foreach (var p in products)
            {
                var price = p.SmallPackPrice?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
                var row = new[]
                {
                    Escape(p.ProductName, delimiter),
                    Escape(price, delimiter),
                    Escape(p.Sku, delimiter),
                    Escape(p.Ean, delimiter),
                    Escape(p.ExpiryDate, delimiter),
                    p.BarcodeEnabled ? "1" : "0",
                    Escape(p.BarcodeValue, delimiter),
                    Escape(p.BarcodeFormat ?? "EAN13", delimiter)
                };
                writer.WriteLine(string.Join(delimiter.ToString(), row));
            }
            return products.Count;
        }
    }
}
