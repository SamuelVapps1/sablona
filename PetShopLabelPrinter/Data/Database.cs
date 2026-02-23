using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Data
{
    public class Database
    {
        private readonly string _dbPath;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        public Database()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PetShopLabelPrinter");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "labels.db");
        }

        public void Initialize()
        {
            using var conn = CreateConnection();
            conn.Open();

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductName TEXT NOT NULL,
                    VariantText TEXT,
                    SmallPackLabel TEXT,
                    SmallPackWeightKg REAL,
                    SmallPackPrice REAL,
                    LargePackLabel TEXT,
                    LargePackWeightKg REAL,
                    LargePackPrice REAL,
                    UnitPriceOverride REAL,
                    UnitPriceText TEXT,
                    Notes TEXT
                )");

            // Migrations for existing DBs.
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN UnitPriceOverride REAL"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN UnitPriceText TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN Ean TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN Sku TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN ExpiryDate TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN ShowEan INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN ShowSku INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN ShowExpiry INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN BarcodeEnabled INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN BarcodeValue TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN BarcodeFormat TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN BarcodeShowText INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN TemplateId INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN PackWeightValue REAL"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN PackWeightUnit TEXT"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN LargePackWeightValue REAL"); } catch { }
            try { Execute(conn, "ALTER TABLE Products ADD COLUMN LargePackWeightUnit TEXT"); } catch { }

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS PrintHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PrintedAt TEXT NOT NULL,
                    JobType TEXT,
                    ProductNames TEXT,
                    TotalLabels INTEGER,
                    PdfPath TEXT
                )");
            try { Execute(conn, "ALTER TABLE PrintHistory ADD COLUMN JobType TEXT"); } catch { }

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS TemplateSettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Version INTEGER,
                    JsonData TEXT
                )");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS AppSettings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT
                )");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS LabelTemplates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    WidthMm REAL NOT NULL,
                    HeightMm REAL NOT NULL,
                    PaddingMm REAL NOT NULL,
                    OffsetXmm REAL NOT NULL DEFAULT 0,
                    OffsetYmm REAL NOT NULL DEFAULT 0,
                    ScaleX REAL NOT NULL DEFAULT 1.0,
                    ScaleY REAL NOT NULL DEFAULT 1.0,
                    ShowEanDefault INTEGER,
                    ShowSkuDefault INTEGER,
                    ShowExpiryDefault INTEGER,
                    BarcodeEnabledDefault INTEGER
                )");
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN ShowEanDefault INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN ShowSkuDefault INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN ShowExpiryDefault INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN BarcodeEnabledDefault INTEGER"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN OffsetXmm REAL NOT NULL DEFAULT 0"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN OffsetYmm REAL NOT NULL DEFAULT 0"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN ScaleX REAL NOT NULL DEFAULT 1.0"); } catch { }
            try { Execute(conn, "ALTER TABLE LabelTemplates ADD COLUMN ScaleY REAL NOT NULL DEFAULT 1.0"); } catch { }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM LabelTemplates";
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    Execute(conn, @"INSERT INTO LabelTemplates (Name, WidthMm, HeightMm, PaddingMm, OffsetXmm, OffsetYmm, ScaleX, ScaleY, ShowEanDefault, ShowSkuDefault, ShowExpiryDefault, BarcodeEnabledDefault)
                        VALUES ('Granule 150x38', 150, 38, 2, 0, 0, 1.0, 1.0, 0, 0, 0, 0)");
                }
            }

            // Insert default template if not exists
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM TemplateSettings";
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count == 0)
                {
                    var def = new TemplateSettings();
                    SaveTemplateSettings(conn, def);
                }
            }

            // Seed sample products if empty
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM Products";
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    var seed = new[]
                    {
                        ("Royal Canin", "Medium Adult", "Balenie 1 kg", 1m, 8.50m, "Balenie 17 kg", 17m, 42.90m),
                        ("Purina Pro Plan", "Adult", "Balenie 0,4 kg", 0.4m, 4.20m, "Balenie 14 kg", 14m, 38.50m),
                        ("Brit Care", "Grain Free", "Balenie 2 kg", 2m, 12.00m, "Balenie 12 kg", 12m, 32.00m)
                    };
                    foreach (var (pn, vt, spl, spw, spp, lpl, lpw, lpp) in seed)
                    {
                        using var ins = conn.CreateCommand();
                        ins.CommandText = @"INSERT INTO Products (ProductName, VariantText, SmallPackLabel, SmallPackWeightKg, SmallPackPrice, LargePackLabel, LargePackWeightKg, LargePackPrice, UnitPriceOverride, UnitPriceText, Notes, Ean, Sku, ExpiryDate, ShowEan, ShowSku, ShowExpiry, BarcodeEnabled, BarcodeValue, BarcodeFormat, BarcodeShowText)
                            VALUES (@pn, @vt, @spl, @spw, @spp, @lpl, @lpw, @lpp, NULL, '', '', NULL, NULL, NULL, 0, 0, 0, 0, NULL, 'EAN13', 1)";
                        ins.Parameters.AddWithValue("@pn", pn);
                        ins.Parameters.AddWithValue("@vt", vt);
                        ins.Parameters.AddWithValue("@spl", spl);
                        ins.Parameters.AddWithValue("@spw", spw);
                        ins.Parameters.AddWithValue("@spp", spp);
                        ins.Parameters.AddWithValue("@lpl", lpl);
                        ins.Parameters.AddWithValue("@lpw", lpw);
                        ins.Parameters.AddWithValue("@lpp", lpp);
                        ins.ExecuteNonQuery();
                    }
                }
            }
        }

        private System.Data.SQLite.SQLiteConnection CreateConnection()
        {
            return new System.Data.SQLite.SQLiteConnection($"Data Source={_dbPath};Version=3;");
        }

        private void Execute(System.Data.SQLite.SQLiteConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        public System.Data.SQLite.SQLiteConnection GetConnection() => CreateConnection();

        public Product? GetProduct(long id)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadProduct(r) : null;
        }

        public void InsertProduct(Product p)
        {
            using var conn = CreateConnection();
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO Products (ProductName, VariantText, SmallPackLabel, SmallPackWeightKg, SmallPackPrice,
                        LargePackLabel, LargePackWeightKg, LargePackPrice, UnitPriceOverride, UnitPriceText, Notes,
                        Ean, Sku, ExpiryDate, ShowEan, ShowSku, ShowExpiry, BarcodeEnabled, BarcodeValue, BarcodeFormat, BarcodeShowText,
                        TemplateId, PackWeightValue, PackWeightUnit, LargePackWeightValue, LargePackWeightUnit)
                    VALUES (@pn, @vt, @spl, @spw, @spp, @lpl, @lpw, @lpp, @upo, @upt, @notes,
                        @ean, @sku, @exp, @se, @ss, @sx, @be, @bv, @bf, @bst, @tid, @pwv, @pwu, @lpwv, @lpwu)";
                AddProductParams(cmd, p);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT last_insert_rowid()";
                p.Id = Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public void UpdateProduct(Product p)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Products SET ProductName=@pn, VariantText=@vt, SmallPackLabel=@spl, SmallPackWeightKg=@spw,
                    SmallPackPrice=@spp, LargePackLabel=@lpl, LargePackWeightKg=@lpw, LargePackPrice=@lpp,
                    UnitPriceOverride=@upo, UnitPriceText=@upt, Notes=@notes,
                    Ean=@ean, Sku=@sku, ExpiryDate=@exp, ShowEan=@se, ShowSku=@ss, ShowExpiry=@sx,
                    BarcodeEnabled=@be, BarcodeValue=@bv, BarcodeFormat=@bf, BarcodeShowText=@bst,
                    TemplateId=@tid, PackWeightValue=@pwv, PackWeightUnit=@pwu, LargePackWeightValue=@lpwv, LargePackWeightUnit=@lpwu
                WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", p.Id);
            AddProductParams(cmd, p);
            cmd.ExecuteNonQuery();
        }

        public void DeleteProduct(long id)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Products WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Find product for import: by SKU first, then by EAN. Returns null if no match.</summary>
        public Product? FindProductForImport(string? sku, string? ean)
        {
            var skuNorm = NormalizeForMatch(sku);
            var eanNorm = NormalizeForMatch(ean);
            if (string.IsNullOrEmpty(skuNorm) && string.IsNullOrEmpty(eanNorm)) return null;

            using var conn = CreateConnection();
            conn.Open();
            if (!string.IsNullOrEmpty(skuNorm))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM Products WHERE TRIM(COALESCE(Sku,'')) = @sku";
                cmd.Parameters.AddWithValue("@sku", skuNorm);
                using var r = cmd.ExecuteReader();
                if (r.Read()) return ReadProduct(r);
            }
            if (!string.IsNullOrEmpty(eanNorm))
            {
                var eanDigits = new string((eanNorm ?? "").Where(char.IsDigit).ToArray());
                if (eanDigits.Length > 0)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM Products WHERE REPLACE(REPLACE(REPLACE(COALESCE(Ean,''),' ',''),'-',''),'\t','') = @ean";
                    cmd.Parameters.AddWithValue("@ean", eanDigits);
                    using var r = cmd.ExecuteReader();
                    if (r.Read()) return ReadProduct(r);
                }
            }
            return null;
        }

        private static string? NormalizeForMatch(string? s)
        {
            var t = s?.Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }

        public System.Collections.Generic.List<Product> SearchProducts(string search)
        {
            var list = new System.Collections.Generic.List<Product>();
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            var term = $"%{search?.Trim() ?? ""}%";
            cmd.CommandText = @"
                SELECT * FROM Products
                WHERE ProductName LIKE @s OR VariantText LIKE @s OR Notes LIKE @s
                   OR Ean LIKE @s OR Sku LIKE @s
                ORDER BY ProductName, VariantText";
            cmd.Parameters.AddWithValue("@s", term);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(ReadProduct(r));
            return list;
        }

        private void AddProductParams(System.Data.SQLite.SQLiteCommand cmd, Product p)
        {
            cmd.Parameters.AddWithValue("@pn", p.ProductName ?? "");
            cmd.Parameters.AddWithValue("@vt", p.VariantText ?? "");
            cmd.Parameters.AddWithValue("@spl", p.SmallPackLabel ?? "");
            cmd.Parameters.AddWithValue("@spw", p.SmallPackWeightKg);
            cmd.Parameters.AddWithValue("@spp", p.SmallPackPrice);
            cmd.Parameters.AddWithValue("@lpl", p.LargePackLabel ?? "");
            cmd.Parameters.AddWithValue("@lpw", p.LargePackWeightKg);
            cmd.Parameters.AddWithValue("@lpp", p.LargePackPrice);
            cmd.Parameters.AddWithValue("@upo", p.UnitPriceOverride);
            cmd.Parameters.AddWithValue("@upt", p.UnitPriceText ?? "");
            cmd.Parameters.AddWithValue("@notes", p.Notes ?? "");
            cmd.Parameters.AddWithValue("@ean", string.IsNullOrWhiteSpace(p.Ean) ? (object)DBNull.Value : p.Ean);
            cmd.Parameters.AddWithValue("@sku", string.IsNullOrWhiteSpace(p.Sku) ? (object)DBNull.Value : p.Sku);
            cmd.Parameters.AddWithValue("@exp", string.IsNullOrWhiteSpace(p.ExpiryDate) ? (object)DBNull.Value : p.ExpiryDate);
            cmd.Parameters.AddWithValue("@se", p.ShowEan ? 1 : 0);
            cmd.Parameters.AddWithValue("@ss", p.ShowSku ? 1 : 0);
            cmd.Parameters.AddWithValue("@sx", p.ShowExpiry ? 1 : 0);
            cmd.Parameters.AddWithValue("@be", p.BarcodeEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@bv", string.IsNullOrWhiteSpace(p.BarcodeValue) ? (object)DBNull.Value : p.BarcodeValue);
            cmd.Parameters.AddWithValue("@bf", p.BarcodeFormat ?? "EAN13");
            cmd.Parameters.AddWithValue("@bst", p.BarcodeShowText ? 1 : 0);
            cmd.Parameters.AddWithValue("@tid", p.TemplateId.HasValue ? (object)p.TemplateId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pwv", p.PackWeightValue.HasValue ? (object)p.PackWeightValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pwu", string.IsNullOrWhiteSpace(p.PackWeightUnit) ? "kg" : p.PackWeightUnit);
            cmd.Parameters.AddWithValue("@lpwv", p.LargePackWeightValue.HasValue ? (object)p.LargePackWeightValue.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@lpwu", string.IsNullOrWhiteSpace(p.LargePackWeightUnit) ? "kg" : p.LargePackWeightUnit);
        }

        private Product ReadProduct(IDataReader r)
        {
            var idx = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++)
                idx[r.GetName(i)] = i;

            var p = new Product
            {
                Id = r.GetInt64(idx["Id"]),
                ProductName = r.GetString(idx["ProductName"]),
                VariantText = GetString(r, idx, "VariantText"),
                SmallPackLabel = GetString(r, idx, "SmallPackLabel"),
                SmallPackWeightKg = GetDecimalNull(r, idx, "SmallPackWeightKg"),
                SmallPackPrice = GetDecimalNull(r, idx, "SmallPackPrice"),
                LargePackLabel = GetString(r, idx, "LargePackLabel"),
                LargePackWeightKg = GetDecimalNull(r, idx, "LargePackWeightKg"),
                LargePackPrice = GetDecimalNull(r, idx, "LargePackPrice"),
                UnitPriceOverride = idx.ContainsKey("UnitPriceOverride") ? GetDecimalNull(r, idx, "UnitPriceOverride") : null,
                UnitPriceText = idx.ContainsKey("UnitPriceText") ? GetString(r, idx, "UnitPriceText") : "",
                Notes = GetString(r, idx, "Notes"),
                Ean = idx.ContainsKey("Ean") ? GetStringNull(r, idx, "Ean") : null,
                Sku = idx.ContainsKey("Sku") ? GetStringNull(r, idx, "Sku") : null,
                ExpiryDate = idx.ContainsKey("ExpiryDate") ? GetStringNull(r, idx, "ExpiryDate") : null,
                ShowEan = idx.ContainsKey("ShowEan") && GetInt(r, idx, "ShowEan") != 0,
                ShowSku = idx.ContainsKey("ShowSku") && GetInt(r, idx, "ShowSku") != 0,
                ShowExpiry = idx.ContainsKey("ShowExpiry") && GetInt(r, idx, "ShowExpiry") != 0,
                BarcodeEnabled = idx.ContainsKey("BarcodeEnabled") && GetInt(r, idx, "BarcodeEnabled") != 0,
                BarcodeValue = idx.ContainsKey("BarcodeValue") ? GetStringNull(r, idx, "BarcodeValue") : null,
                BarcodeFormat = idx.ContainsKey("BarcodeFormat") ? (GetString(r, idx, "BarcodeFormat") ?? "EAN13") : "EAN13",
                BarcodeShowText = !idx.ContainsKey("BarcodeShowText") || GetInt(r, idx, "BarcodeShowText") != 0,
                TemplateId = idx.ContainsKey("TemplateId") ? GetIntNull(r, idx, "TemplateId") : null,
                PackWeightValue = idx.ContainsKey("PackWeightValue") ? GetDecimalNull(r, idx, "PackWeightValue") : null,
                PackWeightUnit = idx.ContainsKey("PackWeightUnit") ? (GetString(r, idx, "PackWeightUnit") ?? "kg") : "kg",
                LargePackWeightValue = idx.ContainsKey("LargePackWeightValue") ? GetDecimalNull(r, idx, "LargePackWeightValue") : null,
                LargePackWeightUnit = idx.ContainsKey("LargePackWeightUnit") ? (GetString(r, idx, "LargePackWeightUnit") ?? "kg") : "kg"
            };
            if (!p.PackWeightValue.HasValue && p.SmallPackWeightKg.HasValue)
            {
                p.PackWeightValue = p.SmallPackWeightKg.Value;
                p.PackWeightUnit = "kg";
            }
            if (!p.LargePackWeightValue.HasValue && p.LargePackWeightKg.HasValue)
            {
                p.LargePackWeightValue = p.LargePackWeightKg.Value;
                p.LargePackWeightUnit = "kg";
            }
            if (p.LargePackWeightValue.HasValue && !p.LargePackWeightKg.HasValue)
            {
                p.LargePackWeightKg = string.Equals(p.LargePackWeightUnit, "g", StringComparison.OrdinalIgnoreCase)
                    ? p.LargePackWeightValue.Value / 1000m
                    : p.LargePackWeightValue.Value;
            }
            return p;
        }

        private static string GetString(IDataReader r, System.Collections.Generic.Dictionary<string, int> idx, string name)
        {
            var i = idx[name];
            return r.IsDBNull(i) ? "" : r.GetString(i);
        }

        private static decimal? GetDecimalNull(IDataReader r, System.Collections.Generic.Dictionary<string, int> idx, string name)
        {
            var i = idx[name];
            return r.IsDBNull(i) ? null : (decimal?)r.GetDecimal(i);
        }

        private static string? GetStringNull(IDataReader r, System.Collections.Generic.Dictionary<string, int> idx, string name)
        {
            if (!idx.TryGetValue(name, out var i)) return null;
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        private static int GetInt(IDataReader r, System.Collections.Generic.Dictionary<string, int> idx, string name)
        {
            if (!idx.TryGetValue(name, out var i)) return 0;
            return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        }

        private static int? GetIntNull(IDataReader r, System.Collections.Generic.Dictionary<string, int> idx, string name)
        {
            if (!idx.TryGetValue(name, out var i)) return null;
            return r.IsDBNull(i) ? null : (int?)Convert.ToInt32(r.GetValue(i));
        }

        public TemplateSettings GetTemplateSettings()
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT JsonData FROM TemplateSettings WHERE Id = 1";
            using var r = cmd.ExecuteReader();
            if (r.Read() && !r.IsDBNull(0))
            {
                var json = r.GetString(0);
                return JsonSerializer.Deserialize<TemplateSettings>(json, JsonOptions) ?? new TemplateSettings();
            }
            return new TemplateSettings();
        }

        public void SaveTemplateSettings(TemplateSettings s)
        {
            using var conn = CreateConnection();
            conn.Open();
            SaveTemplateSettings(conn, s);
        }

        private void SaveTemplateSettings(System.Data.SQLite.SQLiteConnection conn, TemplateSettings s)
        {
            var json = JsonSerializer.Serialize(s, JsonOptions);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO TemplateSettings (Id, Version, JsonData) VALUES (1, @v, @j)";
            cmd.Parameters.AddWithValue("@v", s.Version);
            cmd.Parameters.AddWithValue("@j", json);
            cmd.ExecuteNonQuery();
        }

        public void AddPrintHistory(PrintHistoryItem h)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO PrintHistory (PrintedAt, JobType, ProductNames, TotalLabels, PdfPath)
                VALUES (@at, @type, @names, @total, @path)";
            cmd.Parameters.AddWithValue("@at", h.PrintedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@type", h.JobType ?? "PRINT");
            cmd.Parameters.AddWithValue("@names", h.ProductNames ?? "");
            cmd.Parameters.AddWithValue("@total", h.TotalLabels);
            cmd.Parameters.AddWithValue("@path", h.PdfPath ?? "");
            cmd.ExecuteNonQuery();
        }

        public System.Collections.Generic.List<PrintHistoryItem> GetPrintHistory(int limit = 50)
        {
            var list = new System.Collections.Generic.List<PrintHistoryItem>();
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, PrintedAt, IFNULL(JobType,''), ProductNames, TotalLabels, PdfPath FROM PrintHistory ORDER BY Id DESC LIMIT @lim";
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PrintHistoryItem
                {
                    Id = r.GetInt64(0),
                    PrintedAt = DateTime.Parse(r.GetString(1)),
                    JobType = r.IsDBNull(2) || string.IsNullOrWhiteSpace(r.GetString(2)) ? "PRINT" : r.GetString(2),
                    ProductNames = r.IsDBNull(3) ? "" : r.GetString(3),
                    TotalLabels = r.GetInt32(4),
                    PdfPath = r.IsDBNull(5) ? "" : r.GetString(5)
                });
            }
            return list;
        }

        public void ClearPrintHistory()
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM PrintHistory";
            cmd.ExecuteNonQuery();
        }

        public string? GetSetting(string key)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @k";
            cmd.Parameters.AddWithValue("@k", key);
            var v = cmd.ExecuteScalar();
            return v as string;
        }

        public void SetSetting(string key, string value)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO AppSettings (Key, Value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        public A4SheetSettings GetA4SheetSettings()
        {
            var json = GetSetting("A4SheetSettingsJson");
            if (string.IsNullOrWhiteSpace(json))
                return new A4SheetSettings();
            try
            {
                return JsonSerializer.Deserialize<A4SheetSettings>(json, JsonOptions) ?? new A4SheetSettings();
            }
            catch
            {
                return new A4SheetSettings();
            }
        }

        public void SaveA4SheetSettings(A4SheetSettings settings)
        {
            var json = JsonSerializer.Serialize(settings ?? new A4SheetSettings(), JsonOptions);
            SetSetting("A4SheetSettingsJson", json);
        }

        public System.Collections.Generic.List<LabelTemplate> GetLabelTemplates()
        {
            var list = new System.Collections.Generic.List<LabelTemplate>();
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, WidthMm, HeightMm, PaddingMm, IFNULL(OffsetXmm,0), IFNULL(OffsetYmm,0), IFNULL(ScaleX,1.0), IFNULL(ScaleY,1.0), IFNULL(ShowEanDefault,0), IFNULL(ShowSkuDefault,0), IFNULL(ShowExpiryDefault,0), IFNULL(BarcodeEnabledDefault,0) FROM LabelTemplates ORDER BY Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new LabelTemplate
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    WidthMm = r.GetDouble(2),
                    HeightMm = r.GetDouble(3),
                    PaddingMm = r.GetDouble(4),
                    OffsetXmm = r.GetDouble(5),
                    OffsetYmm = r.GetDouble(6),
                    ScaleX = r.GetDouble(7),
                    ScaleY = r.GetDouble(8),
                    ShowEanDefault = r.GetInt32(9) != 0,
                    ShowSkuDefault = r.GetInt32(10) != 0,
                    ShowExpiryDefault = r.GetInt32(11) != 0,
                    BarcodeEnabledDefault = r.GetInt32(12) != 0
                });
            }
            return list;
        }

        public LabelTemplate? GetLabelTemplate(int id)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, WidthMm, HeightMm, PaddingMm, IFNULL(OffsetXmm,0), IFNULL(OffsetYmm,0), IFNULL(ScaleX,1.0), IFNULL(ScaleY,1.0), IFNULL(ShowEanDefault,0), IFNULL(ShowSkuDefault,0), IFNULL(ShowExpiryDefault,0), IFNULL(BarcodeEnabledDefault,0) FROM LabelTemplates WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? new LabelTemplate
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                WidthMm = r.GetDouble(2),
                HeightMm = r.GetDouble(3),
                PaddingMm = r.GetDouble(4),
                OffsetXmm = r.GetDouble(5),
                OffsetYmm = r.GetDouble(6),
                ScaleX = r.GetDouble(7),
                ScaleY = r.GetDouble(8),
                ShowEanDefault = r.GetInt32(9) != 0,
                ShowSkuDefault = r.GetInt32(10) != 0,
                ShowExpiryDefault = r.GetInt32(11) != 0,
                BarcodeEnabledDefault = r.GetInt32(12) != 0
            } : null;
        }

        public void InsertLabelTemplate(LabelTemplate t)
        {
            using var conn = CreateConnection();
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO LabelTemplates (Name, WidthMm, HeightMm, PaddingMm, OffsetXmm, OffsetYmm, ScaleX, ScaleY, ShowEanDefault, ShowSkuDefault, ShowExpiryDefault, BarcodeEnabledDefault)
                    VALUES (@n, @w, @h, @p, @ox, @oy, @sxn, @syn, @se, @ss, @sx, @be)";
                cmd.Parameters.AddWithValue("@n", t.Name ?? "");
                cmd.Parameters.AddWithValue("@w", t.WidthMm);
                cmd.Parameters.AddWithValue("@h", t.HeightMm);
                cmd.Parameters.AddWithValue("@p", t.PaddingMm);
                cmd.Parameters.AddWithValue("@ox", t.OffsetXmm);
                cmd.Parameters.AddWithValue("@oy", t.OffsetYmm);
                cmd.Parameters.AddWithValue("@sxn", t.ScaleX);
                cmd.Parameters.AddWithValue("@syn", t.ScaleY);
                cmd.Parameters.AddWithValue("@se", t.ShowEanDefault ? 1 : 0);
                cmd.Parameters.AddWithValue("@ss", t.ShowSkuDefault ? 1 : 0);
                cmd.Parameters.AddWithValue("@sx", t.ShowExpiryDefault ? 1 : 0);
                cmd.Parameters.AddWithValue("@be", t.BarcodeEnabledDefault ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT last_insert_rowid()";
                t.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void UpdateLabelTemplate(LabelTemplate t)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE LabelTemplates SET Name=@n, WidthMm=@w, HeightMm=@h, PaddingMm=@p,
                OffsetXmm=@ox, OffsetYmm=@oy, ScaleX=@sxn, ScaleY=@syn,
                ShowEanDefault=@se, ShowSkuDefault=@ss, ShowExpiryDefault=@sx, BarcodeEnabledDefault=@be WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", t.Id);
            cmd.Parameters.AddWithValue("@n", t.Name ?? "");
            cmd.Parameters.AddWithValue("@w", t.WidthMm);
            cmd.Parameters.AddWithValue("@h", t.HeightMm);
            cmd.Parameters.AddWithValue("@p", t.PaddingMm);
            cmd.Parameters.AddWithValue("@ox", t.OffsetXmm);
            cmd.Parameters.AddWithValue("@oy", t.OffsetYmm);
            cmd.Parameters.AddWithValue("@sxn", t.ScaleX);
            cmd.Parameters.AddWithValue("@syn", t.ScaleY);
            cmd.Parameters.AddWithValue("@se", t.ShowEanDefault ? 1 : 0);
            cmd.Parameters.AddWithValue("@ss", t.ShowSkuDefault ? 1 : 0);
            cmd.Parameters.AddWithValue("@sx", t.ShowExpiryDefault ? 1 : 0);
            cmd.Parameters.AddWithValue("@be", t.BarcodeEnabledDefault ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void DeleteLabelTemplate(int id)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM LabelTemplates WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
