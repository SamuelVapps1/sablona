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
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : _settings.RightColWidthMm;
            rightWidthMm = Math.Max(10, Math.Min(labelWidthMm - 10, rightWidthMm));
            var leftWidthMm = labelWidthMm - rightWidthMm;

            var ox = Units.MmToWpfUnits(offsetXMm + _settings.OffsetXMm);
            var oy = Units.MmToWpfUnits(offsetYMm + _settings.OffsetYMm);
            var w = Units.MmToWpfUnits(labelWidthMm);
            var h = Units.MmToWpfUnits(labelHeightMm);

            var rect = new Rect(ox, oy, w, h);

            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;
            var pen = new Pen(Brushes.Black, Units.MmToWpfUnits(borderMm));
            dc.DrawRectangle(null, pen, rect);

            var pad = Units.MmToWpfUnits(_settings.PaddingMm);
            var leftW = Units.MmToWpfUnits(leftWidthMm);
            var rightW = Units.MmToWpfUnits(rightWidthMm);
            var leftRect = new Rect(rect.Left + pad, rect.Top + pad, leftW - pad * 2, rect.Height - pad * 2);
            var rightRect = new Rect(rect.Left + leftW, rect.Top, rightW - pad, rect.Height);

            // Left column: ProductName, VariantText
            DrawLeftColumn(dc, product, leftRect);

            // Right column: Top/Middle/Bottom sections
            DrawRightColumn(dc, product, rightRect);
        }

        private void DrawLeftColumn(DrawingContext dc, Product product, Rect rect)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }

            var y = rect.Top;
            var lineH = Units.MmToWpfUnits(2);

            // ProductName: max 2 lines, auto-reduce font to min if overflow
            var maxTwoLinesHeight = rect.Height * 0.5;
            var pnSize = _settings.ProductNameFontSizePt * 96.0 / 72.0;
            var minSize = _settings.ProductNameMinFontSizePt * 96.0 / 72.0;
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

            // VariantText: max 1 line, truncate with ellipsis
            var vtFont = new Typeface(
                new FontFamily(_settings.VariantTextFontFamily),
                FontStyles.Normal,
                _settings.VariantTextBold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            var vtSize = _settings.VariantTextFontSizePt * 96.0 / 72.0;
            var vtText = new FormattedText(
                product.VariantText ?? "",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                vtFont,
                vtSize,
                Brushes.Black,
                dpi);
            vtText.MaxTextWidth = rect.Width;
            vtText.MaxTextHeight = vtSize * 1.3;
            vtText.Trimming = TextTrimming.CharacterEllipsis;
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
