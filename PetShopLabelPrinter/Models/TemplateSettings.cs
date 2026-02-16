namespace PetShopLabelPrinter.Models
{
    /// <summary>
    /// Universal template settings. Version field allows future multi-template support.
    /// </summary>
    public class TemplateSettings
    {
        public int Version { get; set; } = 1;

        // Fonts
        public string ProductNameFontFamily { get; set; } = "Arial";
        public double ProductNameFontSizePt { get; set; } = 14;
        public double ProductNameMinFontSizePt { get; set; } = 8;
        public bool ProductNameBold { get; set; } = true;

        public string VariantTextFontFamily { get; set; } = "Arial";
        public double VariantTextFontSizePt { get; set; } = 9;
        public bool VariantTextBold { get; set; } = false;

        public string PriceBigFontFamily { get; set; } = "Arial";
        public double PriceBigFontSizePt { get; set; } = 12;
        public bool PriceBigBold { get; set; } = true;

        public string PackLabelSmallFontFamily { get; set; } = "Arial";
        public double PackLabelSmallFontSizePt { get; set; } = 7;
        public bool PackLabelSmallBold { get; set; } = false;

        public string UnitPriceSmallFontFamily { get; set; } = "Arial";
        public double UnitPriceSmallFontSizePt { get; set; } = 7;
        public bool UnitPriceSmallBold { get; set; } = false;

        // Alignments: 0=Left, 1=Center, 2=Right
        public int ProductNameAlign { get; set; } = 0;
        public int VariantTextAlign { get; set; } = 0;
        public int PriceBigAlign { get; set; } = 2;
        public int PackLabelSmallAlign { get; set; } = 0;
        public int UnitPriceSmallAlign { get; set; } = 0;

        // Layout mm
        public double LeftColWidthMm { get; set; } = 90;
        public double RightColWidthMm { get; set; } = 58;
        public double RightTopHeightMm { get; set; } = 12;
        public double RightMiddleHeightMm { get; set; } = 13;
        public double RightBottomHeightMm { get; set; } = 13;
        public double PaddingMm { get; set; } = 2;
        public double LineThicknessMm { get; set; } = 0.25;

        // A4 batch
        public bool CropMarksEnabled { get; set; } = false;

        // Calibration
        public double OffsetXMm { get; set; } = 0;
        public double OffsetYMm { get; set; } = 0;
    }
}
