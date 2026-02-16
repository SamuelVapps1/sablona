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
        public decimal? LargePackPrice { get; set; }
        public string Notes { get; set; } = "";

        /// <summary>
        /// Computed: LargePackPrice/LargePackWeightKg if available; else SmallPackPrice/SmallPackWeightKg; else null.
        /// </summary>
        public decimal? UnitPricePerKg
        {
            get
            {
                if (LargePackWeightKg.HasValue && LargePackWeightKg.Value > 0 && LargePackPrice.HasValue)
                    return LargePackPrice.Value / LargePackWeightKg.Value;
                if (SmallPackWeightKg.HasValue && SmallPackWeightKg.Value > 0 && SmallPackPrice.HasValue)
                    return SmallPackPrice.Value / SmallPackWeightKg.Value;
                return null;
            }
        }
    }
}
