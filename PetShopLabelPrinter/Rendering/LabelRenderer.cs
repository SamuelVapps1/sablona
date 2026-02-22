using System;
using System.Windows;
using System.Windows.Media;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Renders one label with template-configurable size and section widths.
    /// </summary>
    public class LabelRenderer
    {
        public const double LabelWidthMm = 150;
        public const double LabelHeightMm = 38;

        private readonly TemplateSettings _settings;

        public LabelRenderer(TemplateSettings settings)
        {
            _settings = settings ?? new TemplateSettings();
        }

        public void Draw(DrawingContext dc, Product product, double offsetXMm, double offsetYMm)
        {
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelHeightMm;
            var isNarrow = labelWidthMm < 90;
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : _settings.RightColWidthMm;
            if (isNarrow)
                rightWidthMm = Math.Max(20, Math.Min(26, labelWidthMm * 0.32));
            rightWidthMm = Math.Max(10, Math.Min(labelWidthMm - 10, rightWidthMm));
            var leftWidthMm = labelWidthMm - rightWidthMm;

            var ox = Units.MmToWpfUnits(offsetXMm);
            var oy = Units.MmToWpfUnits(offsetYMm);
            var w = Units.MmToWpfUnits(labelWidthMm);
            var h = Units.MmToWpfUnits(labelHeightMm);

            var rect = new Rect(ox, oy, w, h);
            var scaleX = Clamp(_settings.CalibrationScaleX, 0.90, 1.10);
            var scaleY = Clamp(_settings.CalibrationScaleY, 0.90, 1.10);
            var offsetX = Units.MmToWpfUnits(Clamp(_settings.OffsetXMm, -5, 5));
            var offsetY = Units.MmToWpfUnits(Clamp(_settings.OffsetYMm, -5, 5));

            // Final output calibration transform: scale around label center, then translate by mm offsets.
            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            dc.PushTransform(new ScaleTransform(scaleX, scaleY, rect.Left + rect.Width / 2.0, rect.Top + rect.Height / 2.0));

            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;
            var borderW = Units.MmToWpfUnits(borderMm);
            var pen = new Pen(Brushes.Black, borderW);
            // Keep border fully inside allocated label rect.
            var half = borderW / 2.0;
            var borderRect = new Rect(rect.Left + half, rect.Top + half, Math.Max(0, rect.Width - borderW), Math.Max(0, rect.Height - borderW));
            dc.DrawRectangle(null, pen, borderRect);

            var pad = Units.MmToWpfUnits(_settings.PaddingMm);
            var leftW = Units.MmToWpfUnits(leftWidthMm);
            var rightW = Units.MmToWpfUnits(rightWidthMm);

            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;
            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));

            var layout = LabelBottomLayout.ComputeLayout(labelWidthMm, labelHeightMm, _settings.PaddingMm, hasBarcode, hasMeta);
            var bottomReserveMm = layout.BarcodeAreaHeightMm + layout.MetaAreaHeightMm;
            var bottomReserve = Units.MmToWpfUnits(bottomReserveMm);
            var mainHeight = Units.MmToWpfUnits(layout.MainContentHeightMm);

            var leftRect = new Rect(rect.Left + pad, rect.Top + pad, leftW - pad * 2, mainHeight);
            var rightRect = new Rect(rect.Left + leftW, rect.Top, rightW - pad, rect.Height - bottomReserve);

            DrawLeftColumn(dc, product, leftRect, isNarrow);
            DrawRightColumn(dc, product, rightRect);

            var bottomY = rect.Bottom - pad - bottomReserve;
            if (layout.ShowBarcode)
            {
                var qz = Units.MmToWpfUnits(layout.QuietZoneMm);
                var contentW = rect.Width - pad * 2 - qz * 2;
                var bcWidthMm = isNarrow
                    ? Clamp((labelWidthMm - _settings.PaddingMm * 2 - layout.QuietZoneMm * 2) * 0.65, 38, 55)
                    : (labelWidthMm - _settings.PaddingMm * 2 - layout.QuietZoneMm * 2);
                var bcWidth = Math.Min(contentW, Units.MmToWpfUnits(bcWidthMm));
                var bcLeft = rect.Left + pad + qz + Math.Max(0, (contentW - bcWidth) / 2.0);
                var bcHeight = Units.MmToWpfUnits(layout.BarcodeHeightMm);
                var bcRect = new Rect(bcLeft, bottomY, bcWidth, bcHeight);
                BarcodeRenderer.DrawToWpf(dc, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13", bcRect, product.BarcodeShowText);
                bottomY += Units.MmToWpfUnits(layout.BarcodeAreaHeightMm);
            }
            if (layout.ShowMeta)
            {
                DrawMetaRow(dc, product, rect.Left + pad, bottomY, rect.Width - pad * 2);
            }

            dc.Pop();
            dc.Pop();
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void DrawMetaRow(DrawingContext dc, Product product, double x, double y, double width)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var font = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var sizePt = LabelBottomLayout.MetaFontSizePt;
            var size = sizePt * 96.0 / 72.0;
            var lineH = Units.MmToWpfUnits(LabelBottomLayout.MetaLineHeightMm);

            var line1Parts = new System.Collections.Generic.List<string>();
            if (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean)) line1Parts.Add("EAN: " + product.Ean);
            if (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku)) line1Parts.Add("SKU: " + product.Sku);
            var line1 = line1Parts.Count > 0 ? string.Join("  |  ", line1Parts) : null;

            var line2 = product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate)
                ? "SP: " + FormatExpiry(product.ExpiryDate) : null;

            if (!string.IsNullOrEmpty(line1))
            {
                var ft = new FormattedText(line1, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, font, size, Brushes.Black, dpi);
                ft.MaxTextWidth = width;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(x, y));
                y += lineH;
            }
            if (!string.IsNullOrEmpty(line2))
            {
                var ft = new FormattedText(line2, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, font, size, Brushes.Black, dpi);
                ft.MaxTextWidth = width;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(ft, new Point(x, y));
            }
        }

        private static string FormatExpiry(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "";
            if (DateTime.TryParse(iso, out var d)) return d.ToString("dd.MM.yyyy");
            return iso;
        }

        private void DrawLeftColumn(DrawingContext dc, Product product, Rect rect, bool isNarrow)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }

            var y = rect.Top;
            var lineH = Units.MmToWpfUnits(2);

            // ProductName: max 2 lines, auto-reduce font to min if overflow
            var maxTwoLinesHeight = rect.Height * (isNarrow ? 0.62 : 0.5);
            var pnSize = _settings.ProductNameFontSizePt * 96.0 / 72.0;
            var minSize = Math.Max((isNarrow ? 9.0 : _settings.ProductNameMinFontSizePt), _settings.ProductNameMinFontSizePt) * 96.0 / 72.0;
            FormattedText? pnText = null;
            for (var trySize = pnSize; trySize >= minSize; trySize -= 1)
            {
                var pnFont = new Typeface(
                    new FontFamily(_settings.ProductNameFontFamily),
                    FontStyles.Normal,
                    _settings.ProductNameBold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal);
                pnText = new FormattedText(
                    product.ProductName ?? "",
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    pnFont,
                    trySize,
                    Brushes.Black,
                    dpi);
                pnText.MaxTextWidth = rect.Width;
                pnText.MaxTextHeight = maxTwoLinesHeight;
                pnText.Trimming = TextTrimming.CharacterEllipsis;
                if (pnText.Height <= maxTwoLinesHeight + 1) break;
            }
            if (pnText != null)
            {
                var pnX = GetAlignX(rect, pnText.Width, _settings.ProductNameAlign);
                dc.DrawText(pnText, new Point(rect.Left + pnX, y));
                y += pnText.Height + lineH;
            }

            // VariantText: multiline wrap within label boundary
            var vtFont = new Typeface(
                new FontFamily(_settings.VariantTextFontFamily),
                FontStyles.Normal,
                _settings.VariantTextBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var vtSize = _settings.VariantTextFontSizePt * 96.0 / 72.0;
            var vtAvailableHeight = rect.Bottom - y - lineH;
            var vtText = new FormattedText(
                product.VariantText ?? "",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                vtFont,
                vtSize,
                Brushes.Black,
                dpi);
            vtText.MaxTextWidth = rect.Width;
            vtText.MaxTextHeight = Math.Max(vtSize, vtAvailableHeight);
            vtText.Trimming = TextTrimming.None;
            var vtX = GetAlignX(rect, vtText.Width, _settings.VariantTextAlign);
            dc.DrawText(vtText, new Point(rect.Left + vtX, y));
        }

        private double GetAlignX(Rect rect, double textWidth, int align)
        {
            if (align == 1) return (rect.Width - textWidth) / 2;
            if (align == 2) return rect.Width - textWidth;
            return 0;
        }

        private void DrawRightColumn(DrawingContext dc, Product product, Rect rect)
        {
            var topH = Units.MmToWpfUnits(_settings.RightTopHeightMm);
            var midH = Units.MmToWpfUnits(_settings.RightMiddleHeightMm);
            var botH = Units.MmToWpfUnits(_settings.RightBottomHeightMm);
            var total = topH + midH + botH;
            if (total <= 0)
            {
                topH = rect.Height / 3;
                midH = rect.Height / 3;
                botH = rect.Height / 3;
            }
            else
            {
                var scale = rect.Height / total;
                topH *= scale;
                midH *= scale;
                botH *= scale;
            }
            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;
            var pen = new Pen(Brushes.Black, Units.MmToWpfUnits(borderMm));

            var topRect = new Rect(rect.Left, rect.Top, rect.Width, topH);
            var midRect = new Rect(rect.Left, rect.Top + topH, rect.Width, midH);
            var botRect = new Rect(rect.Left, rect.Top + topH + midH, rect.Width, botH);

            if (_settings.ShowSeparatorBetweenPacks)
                dc.DrawLine(pen, new Point(rect.Left, rect.Top + topH), new Point(rect.Right, rect.Top + topH));
            if (_settings.ShowBottomSeparator)
                dc.DrawLine(pen, new Point(rect.Left, rect.Top + topH + midH), new Point(rect.Right, rect.Top + topH + midH));

            var dpi = Application.Current?.MainWindow != null
                ? VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip
                : 1.0;

            // Top: SmallPackLabel + SmallPackPrice
            DrawSection(dc, product.SmallPackLabel ?? "", Formatting.FormatPrice(product.SmallPackPrice),
                topRect, dpi);

            // Middle: LargePackLabel + LargePackPrice
            DrawSection(dc, product.LargePackLabel ?? "", Formatting.FormatPrice(product.LargePackPrice),
                midRect, dpi);

            // Bottom: allow manually entered unit price text for store-specific wording.
            var unitText = string.IsNullOrWhiteSpace(product.UnitPriceText)
                ? Formatting.FormatUnitPrice(product.UnitPricePerKg)
                : product.UnitPriceText;
            var uFont = new Typeface(
                new FontFamily(_settings.UnitPriceSmallFontFamily),
                FontStyles.Normal,
                _settings.UnitPriceSmallBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var uSize = _settings.UnitPriceSmallFontSizePt * 96.0 / 72.0;
            var uFormatted = new FormattedText(
                unitText,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                uFont,
                uSize,
                Brushes.Black,
                dpi);
            uFormatted.MaxTextWidth = botRect.Width - Units.MmToWpfUnits(_settings.PaddingMm) * 2;
            var uX = GetAlignX(botRect, uFormatted.Width, _settings.UnitPriceSmallAlign);
            dc.DrawText(uFormatted, new Point(botRect.Left + uX + Units.MmToWpfUnits(_settings.PaddingMm), botRect.Top + (botRect.Height - uFormatted.Height) / 2));
        }

        private void DrawSection(DrawingContext dc, string label, string price, Rect rect, double dpi)
        {
            var pad = Units.MmToWpfUnits(_settings.PaddingMm);
            var labelFont = new Typeface(
                new FontFamily(_settings.PackLabelSmallFontFamily),
                FontStyles.Normal,
                _settings.PackLabelSmallBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var labelSize = _settings.PackLabelSmallFontSizePt * 96.0 / 72.0;
            var labelFormatted = new FormattedText(
                label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                labelFont,
                labelSize,
                Brushes.Black,
                dpi);
            labelFormatted.MaxTextWidth = rect.Width - pad * 2;
            var labelX = GetAlignX(rect, labelFormatted.Width, _settings.PackLabelSmallAlign);
            dc.DrawText(labelFormatted, new Point(rect.Left + pad + labelX, rect.Top + 1));

            var priceFont = new Typeface(
                new FontFamily(_settings.PriceBigFontFamily),
                FontStyles.Normal,
                _settings.PriceBigBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var priceSize = _settings.PriceBigFontSizePt * 96.0 / 72.0;
            var priceFormatted = new FormattedText(
                price,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                priceFont,
                priceSize,
                Brushes.Black,
                dpi);
            var priceX = GetAlignX(rect, priceFormatted.Width, _settings.PriceBigAlign);
            dc.DrawText(priceFormatted, new Point(rect.Left + pad + priceX, rect.Top + labelFormatted.Height + 1));
        }
    }
}
