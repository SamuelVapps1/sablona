using System;
using PdfSharp.Drawing;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Renders labels to PDF using PdfSharp. ProductName: max 2 lines, auto-reduce font.
    /// VariantText: max 1 line, truncate with ellipsis.
    /// </summary>
    public class PdfLabelRenderer
    {
        private readonly TemplateSettings _settings;

        public PdfLabelRenderer(TemplateSettings settings)
        {
            _settings = settings ?? new TemplateSettings();
        }

        public void Draw(XGraphics gfx, Product product, double offsetXMm, double offsetYMm)
        {
            var ox = offsetXMm + _settings.OffsetXMm;
            var oy = offsetYMm + _settings.OffsetYMm;
            var w = LabelRenderer.LabelWidthMm;
            var h = LabelRenderer.LabelHeightMm;

            // Outer border
            var pen = new XPen(XColors.Black, _settings.LineThicknessMm);
            gfx.DrawRectangle(pen, XUnit.FromMillimeter(ox), XUnit.FromMillimeter(oy),
                XUnit.FromMillimeter(w), XUnit.FromMillimeter(h));

            var pad = _settings.PaddingMm;
            var leftW = _settings.LeftColWidthMm;
            var rightW = _settings.RightColWidthMm;

            // Left column
            DrawLeftColumn(gfx, product, ox + pad, oy + pad, leftW - pad * 2, h - pad * 2);

            // Right column
            var rightX = ox + leftW;
            DrawRightColumn(gfx, product, rightX, oy, rightW - pad, h);
        }

        private void DrawLeftColumn(XGraphics gfx, Product product, double x, double y, double w, double h)
        {
            var maxTwoLinesHeight = h * 0.5;
            var pnSize = _settings.ProductNameFontSizePt;
            var minSize = Math.Max(6, _settings.ProductNameMinFontSizePt);

            // ProductName: max 2 lines, auto-reduce font
            var trySize = Math.Max(pnSize, minSize);
            XFont pnFont = new XFont(_settings.ProductNameFontFamily, trySize,
                _settings.ProductNameBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var pnRect = new XRect(x, y, w, maxTwoLinesHeight);
            var pnSizeMeasured = gfx.MeasureString(product.ProductName ?? "", pnFont, pnRect, XStringFormats.TopLeft);
            for (trySize = pnSize; trySize >= minSize; trySize -= 1)
            {
                pnFont = new XFont(_settings.ProductNameFontFamily, trySize,
                    _settings.ProductNameBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                pnSizeMeasured = gfx.MeasureString(product.ProductName ?? "", pnFont, pnRect, XStringFormats.TopLeft);
                if (pnSizeMeasured.Height <= maxTwoLinesHeight + 0.5) break;
            }
            gfx.DrawString(product.ProductName ?? "", pnFont, XBrushes.Black, pnRect, XStringFormats.TopLeft);
            y += pnSizeMeasured.Height + 2;

            // VariantText: max 1 line, truncate with ellipsis
            var vtFont = new XFont(_settings.VariantTextFontFamily, _settings.VariantTextFontSizePt,
                _settings.VariantTextBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var vtText = product.VariantText ?? "";
            var vtMeasured = gfx.MeasureString(vtText, vtFont);
            if (vtMeasured.Width > w)
            {
                while (vtText.Length > 1 && gfx.MeasureString(vtText + "...", vtFont).Width > w)
                    vtText = vtText.Substring(0, vtText.Length - 1);
                vtText = vtText + "...";
            }
            gfx.DrawString(vtText, vtFont, XBrushes.Black, new XRect(x, y, w, vtMeasured.Height), XStringFormats.TopLeft);
        }

        private void DrawRightColumn(XGraphics gfx, Product product, double x, double y, double w, double h)
        {
            var topH = _settings.RightTopHeightMm;
            var midH = _settings.RightMiddleHeightMm;
            var botH = _settings.RightBottomHeightMm;
            var pen = new XPen(XColors.Black, _settings.LineThicknessMm);

            gfx.DrawLine(pen, x, y + topH, x + w, y + topH);
            gfx.DrawLine(pen, x, y + topH + midH, x + w, y + topH + midH);

            var pad = _settings.PaddingMm;

            // Top
            DrawSection(gfx, product.SmallPackLabel ?? "", Formatting.FormatPrice(product.SmallPackPrice),
                x + pad, y, w - pad * 2, topH);

            // Middle
            DrawSection(gfx, product.LargePackLabel ?? "", Formatting.FormatPrice(product.LargePackPrice),
                x + pad, y + topH, w - pad * 2, midH);

            // Bottom: UnitPricePerKg (override if set, else computed; 2 decimals, comma, €)
            var unitText = Formatting.FormatUnitPrice(product.UnitPricePerKg);
            var uFont = new XFont(_settings.UnitPriceSmallFontFamily, _settings.UnitPriceSmallFontSizePt,
                _settings.UnitPriceSmallBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var format = XStringFormats.Center;
            gfx.DrawString(unitText, uFont, XBrushes.Black,
                new XRect(x + pad, y + topH + midH, w - pad * 2, botH), format);
        }

        private void DrawSection(XGraphics gfx, string label, string price, double x, double y, double w, double h)
        {
            var labelFont = new XFont(_settings.PackLabelSmallFontFamily, _settings.PackLabelSmallFontSizePt,
                _settings.PackLabelSmallBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var priceFont = new XFont(_settings.PriceBigFontFamily, _settings.PriceBigFontSizePt,
                _settings.PriceBigBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

            var labelFormat = _settings.PackLabelSmallAlign == 2 ? XStringFormats.TopRight
                : _settings.PackLabelSmallAlign == 1 ? XStringFormats.TopCenter : XStringFormats.TopLeft;
            var priceFormat = _settings.PriceBigAlign == 2 ? XStringFormats.TopRight
                : _settings.PriceBigAlign == 1 ? XStringFormats.TopCenter : XStringFormats.TopLeft;

            gfx.DrawString(label, labelFont, XBrushes.Black, new XRect(x, y, w, h / 2), labelFormat);
            gfx.DrawString(price, priceFont, XBrushes.Black, new XRect(x, y + h / 2, w, h / 2), priceFormat);
        }

        public void DrawCropMarks(XGraphics gfx, double offsetXMm, double offsetYMm, double cropLenMm = 3)
        {
            var ox = offsetXMm + _settings.OffsetXMm;
            var oy = offsetYMm + _settings.OffsetYMm;
            var w = LabelRenderer.LabelWidthMm;
            var h = LabelRenderer.LabelHeightMm;
            var pen = new XPen(XColors.Black, 0.2);

            // Top-left
            gfx.DrawLine(pen, ox, oy, ox + cropLenMm, oy);
            gfx.DrawLine(pen, ox, oy, ox, oy + cropLenMm);
            // Top-right
            gfx.DrawLine(pen, ox + w - cropLenMm, oy, ox + w, oy);
            gfx.DrawLine(pen, ox + w, oy, ox + w, oy + cropLenMm);
            // Bottom-left
            gfx.DrawLine(pen, ox, oy + h - cropLenMm, ox, oy + h);
            gfx.DrawLine(pen, ox, oy + h, ox + cropLenMm, oy + h);
            // Bottom-right
            gfx.DrawLine(pen, ox + w - cropLenMm, oy + h, ox + w, oy + h);
            gfx.DrawLine(pen, ox + w, oy + h - cropLenMm, ox + w, oy + h);
        }
    }
}
