using System;

namespace PetShopLabelPrinter.Rendering
{
    public static class RetailLayoutConfig
    {
        public const double WideThresholdMm = 110;
        public const double NarrowThresholdMm = 90;

        public const double GapRatio = 0.015;
        public const double GapMinMm = 1.5;
        public const double GapMaxMm = 2.5;

        public const double WideLeftRatio = 0.58;
        public const double WideLeftMinRatio = 0.55;
        public const double WideLeftMaxRatio = 0.62;
        public const double NarrowLeftRatio = 0.62;

        public const double WideBarcodeWidthRatio = 0.85;
        public const double WideBarcodeWidthMinMm = 55;
        public const double WideBarcodeWidthMaxMm = 75;
        public const double WideBarcodeHeightRatio = 0.32;
        public const double WideBarcodeHeightMinMm = 10;
        public const double WideBarcodeHeightMaxMm = 13;

        public const double NarrowBarcodeWidthRatio = 0.55;
        public const double NarrowBarcodeWidthMinMm = 34;
        public const double NarrowBarcodeWidthMaxMm = 48;
        public const double NarrowBarcodeHeightRatio = 0.28;
        public const double NarrowBarcodeHeightMinMm = 9;
        public const double NarrowBarcodeHeightMaxMm = 12;

        public const double QuietZoneMm = 2;
        public const double BarcodeTextPt = 6.5;
        public const double MetaPt = 5.7;

        public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
