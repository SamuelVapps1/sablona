namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Converts mm to WPF units (1/96 inch). WPF uses 96 DPI.
    /// </summary>
    public static class Units
    {
        public const double MmPerInch = 25.4;
        public const double WpfUnitsPerInch = 96;
        public const double MmToWpf = WpfUnitsPerInch / MmPerInch;

        public static double MmToWpfUnits(double mm) => mm * MmToWpf;
        public static double WpfToMm(double wpf) => wpf / MmToWpf;
    }
}
