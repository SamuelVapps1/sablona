using System;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Dynamic sizing for label bottom area (barcode + meta row).
    /// Values are proportional to label size and clamped for scanner reliability.
    /// </summary>
    public static class LabelBottomLayout
    {
        private const double MinMainContentHeightMm = 10;
        private const double BarcodeHeightMinMm = 10;
        private const double BarcodeHeightMaxMm = 18;
        private const double MetaAreaMinMm = 5;
        private const double MetaAreaMaxMm = 8;
        private const double QuietZoneMinMm = 2;
        private const double QuietZoneMaxMm = 4;
        private const double BarcodeTextGapMm = 2;

        /// <summary>Meta row font size (pt). Small but readable.</summary>
        public const double MetaFontSizePt = 6;

        /// <summary>Meta line height (mm). Two-line layout.</summary>
        public const double MetaLineHeightMm = 3.5;

        /// <summary>Clamp value between min and max.</summary>
        private static double Clamp(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));

        /// <summary>Barcode bar height (mm). Proportional to label height, clamped 10–18.</summary>
        public static double GetBarcodeHeightMm(double widthMm, double heightMm) =>
            Clamp(heightMm * 0.35, BarcodeHeightMinMm, BarcodeHeightMaxMm);

        /// <summary>Total barcode area height: bars + gap for human-readable text.</summary>
        public static double GetBarcodeAreaHeightMm(double widthMm, double heightMm) =>
            GetBarcodeHeightMm(widthMm, heightMm) + BarcodeTextGapMm;

        /// <summary>Meta area height (mm). Proportional, clamped 5–8.</summary>
        public static double GetMetaAreaHeightMm(double widthMm, double heightMm) =>
            Clamp(heightMm * 0.18, MetaAreaMinMm, MetaAreaMaxMm);

        /// <summary>Quiet zone left/right of barcode (mm). Proportional to width, clamped 2–4.</summary>
        public static double GetQuietZoneMm(double widthMm, double heightMm) =>
            Clamp(widthMm * 0.02, QuietZoneMinMm, QuietZoneMaxMm);

        /// <summary>
        /// Computes layout for bottom area. Enforces minimum main content height.
        /// Priority: main content (price+title) > barcode > meta. Meta can be auto-hidden when space is tight.
        /// </summary>
        public static BottomLayoutResult ComputeLayout(double widthMm, double heightMm, double paddingMm,
            bool hasBarcode, bool hasMeta)
        {
            var pad = paddingMm;
            var availableHeight = heightMm - pad * 2;

            var barcodeAreaMm = hasBarcode ? GetBarcodeAreaHeightMm(widthMm, heightMm) : 0;
            var metaAreaMm = hasMeta ? GetMetaAreaHeightMm(widthMm, heightMm) : 0;

            var mainHeight = availableHeight - barcodeAreaMm - metaAreaMm;

            if (mainHeight >= MinMainContentHeightMm)
                return new BottomLayoutResult
                {
                    MainContentHeightMm = mainHeight,
                    BarcodeAreaHeightMm = barcodeAreaMm,
                    MetaAreaHeightMm = metaAreaMm,
                    ShowBarcode = hasBarcode,
                    ShowMeta = hasMeta,
                    BarcodeHeightMm = hasBarcode ? GetBarcodeHeightMm(widthMm, heightMm) : 0,
                    QuietZoneMm = GetQuietZoneMm(widthMm, heightMm)
                };

            // Not enough space. Hide meta first.
            metaAreaMm = 0;
            mainHeight = availableHeight - barcodeAreaMm;
            if (mainHeight >= MinMainContentHeightMm)
                return new BottomLayoutResult
                {
                    MainContentHeightMm = mainHeight,
                    BarcodeAreaHeightMm = barcodeAreaMm,
                    MetaAreaHeightMm = 0,
                    ShowBarcode = hasBarcode,
                    ShowMeta = false,
                    BarcodeHeightMm = hasBarcode ? GetBarcodeHeightMm(widthMm, heightMm) : 0,
                    QuietZoneMm = GetQuietZoneMm(widthMm, heightMm)
                };

            // Still not enough. Reduce barcode height.
            var minBarcodeArea = hasBarcode ? BarcodeHeightMinMm + BarcodeTextGapMm : 0;
            barcodeAreaMm = Math.Min(barcodeAreaMm, Math.Max(0, availableHeight - MinMainContentHeightMm));
            if (barcodeAreaMm < minBarcodeArea && hasBarcode)
                barcodeAreaMm = minBarcodeArea;
            mainHeight = availableHeight - barcodeAreaMm;

            if (mainHeight < MinMainContentHeightMm && hasBarcode)
            {
                barcodeAreaMm = 0;
                mainHeight = availableHeight;
            }

            var bcHeight = barcodeAreaMm > 0 ? Math.Max(BarcodeHeightMinMm, barcodeAreaMm - BarcodeTextGapMm) : 0;
            return new BottomLayoutResult
            {
                MainContentHeightMm = mainHeight,
                BarcodeAreaHeightMm = barcodeAreaMm,
                MetaAreaHeightMm = 0,
                ShowBarcode = barcodeAreaMm > 0,
                ShowMeta = false,
                BarcodeHeightMm = bcHeight,
                QuietZoneMm = GetQuietZoneMm(widthMm, heightMm)
            };
        }
    }

    public struct BottomLayoutResult
    {
        public double MainContentHeightMm { get; set; }
        public double BarcodeAreaHeightMm { get; set; }
        public double MetaAreaHeightMm { get; set; }
        public bool ShowBarcode { get; set; }
        public bool ShowMeta { get; set; }
        public double BarcodeHeightMm { get; set; }
        public double QuietZoneMm { get; set; }
    }
}
