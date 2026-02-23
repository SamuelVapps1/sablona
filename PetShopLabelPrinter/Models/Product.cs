namespace PetShopLabelPrinter.Models
{
    public class Product
    {
        public long Id { get; set; }
        public string ProductName { get; set; } = "";
        public string VariantText { get; set; } = "";
        public string SmallPackLabel { get; set; } = "";
        public decimal? SmallPackWeightKg { get; set; }
        public decimal? SmallPackPrice { get; set; }
        public string LargePackLabel { get; set; } = "";
        public decimal? LargePackWeightKg { get; set; }
        public decimal? LargePackWeightValue { get; set; }
        public string LargePackWeightUnit { get; set; } = "kg";
        public decimal? LargePackPrice { get; set; }
        public decimal? UnitPriceOverride { get; set; }
        public string UnitPriceText { get; set; } = "";
        public string Notes { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public bool IsActiveForPrint { get; set; } = false;
        public int? TemplateId { get; set; }
        public string TemplateName { get; set; } = "";

        public string? Ean { get; set; }
        public string? Sku { get; set; }
        public string? ExpiryDate { get; set; }
        public bool ShowEan { get; set; }
        public bool ShowSku { get; set; }
        public bool ShowExpiry { get; set; }

        public bool BarcodeEnabled { get; set; }
        public string? BarcodeValue { get; set; }
        public string BarcodeFormat { get; set; } = "EAN13";
        public bool BarcodeShowText { get; set; } = true;
        public decimal? PackWeightValue { get; set; }
        public string PackWeightUnit { get; set; } = "kg";

        /// <summary>
        /// Computed: LargePackPrice/LargePackWeightKg if available; else SmallPackPrice/SmallPackWeightKg; else null.
        /// </summary>
        public decimal? UnitPricePerKgComputed
        {
            get
            {
                if (PackWeightValue.HasValue && PackWeightValue.Value > 0 && SmallPackPrice.HasValue)
                {
                    var weightKg = PackWeightUnit == "g"
                        ? PackWeightValue.Value / 1000m
                        : PackWeightValue.Value;
                    if (weightKg > 0) return SmallPackPrice.Value / weightKg;
                }
                if (LargePackWeightValue.HasValue && LargePackWeightValue.Value > 0 && LargePackPrice.HasValue)
                {
                    var largeWeightKg = LargePackWeightUnit == "g"
                        ? LargePackWeightValue.Value / 1000m
                        : LargePackWeightValue.Value;
                    if (largeWeightKg > 0) return LargePackPrice.Value / largeWeightKg;
                }
                if (LargePackWeightKg.HasValue && LargePackWeightKg.Value > 0 && LargePackPrice.HasValue)
                    return LargePackPrice.Value / LargePackWeightKg.Value;
                if (SmallPackWeightKg.HasValue && SmallPackWeightKg.Value > 0 && SmallPackPrice.HasValue)
                    return SmallPackPrice.Value / SmallPackWeightKg.Value;
                return null;
            }
        }

        /// <summary>
        /// Effective unit price: UnitPriceOverride if set; else computed from large pack (fallback small).
        /// </summary>
        public decimal? UnitPricePerKg => UnitPriceOverride ?? UnitPricePerKgComputed;
    }
}
