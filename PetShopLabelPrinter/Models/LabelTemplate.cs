namespace PetShopLabelPrinter.Models
{
    /// <summary>
    /// Label size preset for different formats (granules, cans, etc.).
    /// </summary>
    public class LabelTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double PaddingMm { get; set; }
        public double OffsetXmm { get; set; } = 0;
        public double OffsetYmm { get; set; } = 0;
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;

        public bool ShowEanDefault { get; set; }
        public bool ShowSkuDefault { get; set; }
        public bool ShowExpiryDefault { get; set; }
        public bool BarcodeEnabledDefault { get; set; }
    }
}
