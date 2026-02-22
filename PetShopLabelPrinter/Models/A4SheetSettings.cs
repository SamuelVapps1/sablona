namespace PetShopLabelPrinter.Models
{
    /// <summary>
    /// Global A4 sheet layout/calibration settings used by print and PDF export.
    /// </summary>
    public class A4SheetSettings
    {
        public double SheetWidthMm { get; set; } = 210;
        public double SheetHeightMm { get; set; } = 297;
        public double SheetMarginMm { get; set; } = 8;
        public double GapMm { get; set; } = 2;
        public string Orientation { get; set; } = "Portrait";

        public double CalibrationScaleX { get; set; } = 1.0;
        public double CalibrationScaleY { get; set; } = 1.0;
        public double CalibrationOffsetXmm { get; set; } = 0;
        public double CalibrationOffsetYmm { get; set; } = 0;
        public bool DebugLayout { get; set; } = false;
    }
}
