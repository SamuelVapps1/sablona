using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Services
{
    /// <summary>
    /// CSV import/export for Alfa+ interoperability.
    /// </summary>
    public class CsvImportService
    {
        private readonly Database _db;

        public CsvImportService(Database db)
        {
            _db = db;
        }

        public const string MappingPresetKey = "CsvImportMappingPreset";

        public static readonly string[] TargetFields = { "Name", "Price", "SKU", "EAN", "ExpiryDate" };
        public static readonly string[] TargetFieldLabels = { "Názov (ProductName)", "Cena (SmallPackPrice)", "SKU", "EAN", "Dátum spotreby" };

        /// <summary>Detect delimiter from first line: comma or semicolon.</summary>
        public static char DetectDelimiter(string firstLine)
        {
            var commaCount = firstLine.Count(c => c == ',');
            var semicolonCount = firstLine.Count(c => c == ';');
            return semicolonCount >= commaCount ? ';' : ',';
        }

        /// <summary>Parse CSV line with given delimiter. Handles quoted fields.</summary>
        public static string[] ParseLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (inQuotes)
                {
                    current.Append(c);
                }
                else if (c == delimiter)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        /// <summary>Read CSV with UTF-8, fallback to default encoding. Returns raw lines for re-parsing with different delimiter.</summary>
        public static (List<string> RawLines, List<string[]> Rows, char Delimiter, Encoding Encoding) ReadCsv(string path)
        {
            var encodings = new[] { Encoding.UTF8, Encoding.GetEncoding(1250), Encoding.Default };
            foreach (var enc in encodings)
            {
                try
                {
                    var lines = File.ReadAllLines(path, enc);
                    if (lines.Length == 0) return (new List<string>(), new List<string[]>(), ',', enc);
                    var delimiter = DetectDelimiter(lines[0]);
                    var rows = lines.Select(l => ParseLine(l, delimiter)).ToList();
                    return (lines.ToList(), rows, delimiter, enc);
                }
                catch { }
            }
            return (new List<string>(), new List<string[]>(), ',', Encoding.UTF8);
        }

        /// <summary>Apply barcode handshake: if BarcodeEnabled and BarcodeValue empty, set from EAN/SKU.</summary>
        public static void ApplyBarcodeHandshake(Product p)
        {
            if (!p.BarcodeEnabled) return;
            if (!string.IsNullOrWhiteSpace(p.BarcodeValue)) return; // user override

            var source = !string.IsNullOrWhiteSpace(p.Ean) ? p.Ean : p.Sku;
            if (string.IsNullOrWhiteSpace(source)) return;

            p.BarcodeValue = source.Trim();
            var digits = new string(p.BarcodeValue.Where(char.IsDigit).ToArray());
            p.BarcodeFormat = (digits.Length == 12 || digits.Length == 13) ? "EAN13" : "CODE128";
            p.BarcodeValue = BarcodeRenderer.NormalizeBarcodeValue(p.BarcodeValue, p.BarcodeFormat);
        }

        /// <summary>Import rows with given mapping. Returns (created, updated) counts.</summary>
        public (int Created, int Updated) Import(List<string[]> rows, Dictionary<string, int> mapping, bool skipHeader)
        {
            var start = skipHeader && rows.Count > 1 ? 1 : 0;
            var created = 0;
            var updated = 0;

            for (var i = start; i < rows.Count; i++)
            {
                var row = rows[i];
                var name = GetCell(row, mapping, "Name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var sku = NormalizeTrim(GetCell(row, mapping, "SKU"));
                var ean = NormalizeEan(GetCell(row, mapping, "EAN"));
                var priceStr = GetCell(row, mapping, "Price");
                var expiry = NormalizeTrim(GetCell(row, mapping, "ExpiryDate"));

                var existing = _db.FindProductForImport(sku, ean);
                Product p;
                if (existing != null)
                {
                    p = existing;
                    updated++;
                }
                else
                {
                    p = new Product { Quantity = 1 };
                    created++;
                }

                p.ProductName = name.Trim();
                if (decimal.TryParse(priceStr?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    p.SmallPackPrice = price;
                p.Sku = string.IsNullOrWhiteSpace(sku) ? null : sku;
                p.Ean = string.IsNullOrWhiteSpace(ean) ? null : ean;
                p.ExpiryDate = string.IsNullOrWhiteSpace(expiry) ? null : expiry;
                p.ShowEan = !string.IsNullOrWhiteSpace(p.Ean);
                p.ShowSku = !string.IsNullOrWhiteSpace(p.Sku);
                p.ShowExpiry = !string.IsNullOrWhiteSpace(p.ExpiryDate);
                p.BarcodeEnabled = !string.IsNullOrWhiteSpace(p.Ean) || !string.IsNullOrWhiteSpace(p.Sku);

                ApplyBarcodeHandshake(p);

                if (p.Id == 0)
                {
                    p.SmallPackLabel = "Balenie 1 kg";
                    p.SmallPackWeightKg = 1;
                    p.LargePackLabel = "";
                    p.LargePackWeightKg = null;
                    p.LargePackPrice = null;
                    _db.InsertProduct(p);
                }
                else
                {
                    _db.UpdateProduct(p);
                }
            }
            return (created, updated);
        }

        private static string? GetCell(string[] row, Dictionary<string, int> mapping, string field)
        {
            if (!mapping.TryGetValue(field, out var col) || col < 0 || col >= row.Length)
                return null;
            return row[col];
        }

        private static string? NormalizeTrim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string? NormalizeEan(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var digits = new string(s.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? s.Trim() : digits;
        }

        public void SaveMappingPreset(Dictionary<string, int> mapping, char delimiter, bool skipHeader)
        {
            var preset = new CsvMappingPreset
            {
                Mapping = mapping,
                Delimiter = delimiter.ToString(),
                SkipHeader = skipHeader
            };
            _db.SetSetting(MappingPresetKey, System.Text.Json.JsonSerializer.Serialize(preset));
        }

        public (Dictionary<string, int>? Mapping, char Delimiter, bool SkipHeader) LoadMappingPreset()
        {
            var json = _db.GetSetting(MappingPresetKey);
            if (string.IsNullOrWhiteSpace(json)) return (null, ';', true);
            try
            {
                var preset = System.Text.Json.JsonSerializer.Deserialize<CsvMappingPreset>(json);
                if (preset?.Mapping == null) return (null, ';', true);
                var delim = preset.Delimiter?.Length == 1 ? preset.Delimiter[0] : ';';
                return (preset.Mapping, delim, preset.SkipHeader);
            }
            catch { return (null, ';', true); }
        }
    }

    public class CsvMappingPreset
    {
        public Dictionary<string, int>? Mapping { get; set; }
        public string? Delimiter { get; set; }
        public bool SkipHeader { get; set; }
    }
}
