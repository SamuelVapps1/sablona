using System;
using System.Data;
using System.IO;
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
                    Notes TEXT
                )");

            // Migration: add UnitPriceOverride if missing (existing DBs)
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Products)";
                    using var r = cmd.ExecuteReader();
                    var hasOverride = false;
                    while (r.Read())
                    {
                        var name = r.GetString(1);
                        if (name == "UnitPriceOverride") { hasOverride = true; break; }
                    }
                    if (!hasOverride)
                        Execute(conn, "ALTER TABLE Products ADD COLUMN UnitPriceOverride REAL");
                }
            }
            catch { /* ignore migration errors */ }

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS PrintHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PrintedAt TEXT NOT NULL,
                    ProductNames TEXT,
                    TotalLabels INTEGER,
                    PdfPath TEXT
                )");

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
                        ins.CommandText = @"INSERT INTO Products (ProductName, VariantText, SmallPackLabel, SmallPackWeightKg, SmallPackPrice, LargePackLabel, LargePackWeightKg, LargePackPrice, UnitPriceOverride, Notes)
                            VALUES (@pn, @vt, @spl, @spw, @spp, @lpl, @lpw, @lpp, NULL, '')";
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
                        LargePackLabel, LargePackWeightKg, LargePackPrice, UnitPriceOverride, Notes)
                    VALUES (@pn, @vt, @spl, @spw, @spp, @lpl, @lpw, @lpp, @upo, @notes)";
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
                    UnitPriceOverride=@upo, Notes=@notes
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
            cmd.Parameters.AddWithValue("@notes", p.Notes ?? "");
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
                Notes = GetString(r, idx, "Notes")
            };
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
                INSERT INTO PrintHistory (PrintedAt, ProductNames, TotalLabels, PdfPath)
                VALUES (@at, @names, @total, @path)";
            cmd.Parameters.AddWithValue("@at", h.PrintedAt.ToString("o"));
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
            cmd.CommandText = "SELECT Id, PrintedAt, ProductNames, TotalLabels, PdfPath FROM PrintHistory ORDER BY Id DESC LIMIT @lim";
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PrintHistoryItem
                {
                    Id = r.GetInt64(0),
                    PrintedAt = DateTime.Parse(r.GetString(1)),
                    ProductNames = r.IsDBNull(2) ? "" : r.GetString(2),
                    TotalLabels = r.GetInt32(3),
                    PdfPath = r.IsDBNull(4) ? "" : r.GetString(4)
                });
            }
            return list;
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
    }
}
