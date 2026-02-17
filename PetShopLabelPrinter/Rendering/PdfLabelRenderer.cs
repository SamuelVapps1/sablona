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
        private static double MmToPt(double mm) => XUnit.FromMillimeter(mm).Point;

        private readonly TemplateSettings _settings;

        public PdfLabelRenderer(TemplateSettings settings)
        {
            _settings = settings ?? new TemplateSettings();
        }

        public void Draw(XGraphics gfx, Product product, double offsetXMm, double offsetYMm)
        {
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : _settings.RightColWidthMm;
            rightWidthMm = Math.Max(10, Math.Min(labelWidthMm - 10, rightWidthMm));
            var leftWidthMm = labelWidthMm - rightWidthMm;
            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;

            var ox = offsetXMm + _settings.OffsetXMm;
            var oy = offsetYMm + _settings.OffsetYMm;
            var w = labelWidthMm;
            var h = labelHeightMm;

            // Outer border (points)
            var pen = new XPen(XColors.Black, MmToPt(borderMm));
            gfx.DrawRectangle(pen, MmToPt(ox), MmToPt(oy), MmToPt(w), MmToPt(h));

            var pad = _settings.PaddingMm;
            var leftW = leftWidthMm;
            var rightW = rightWidthMm;

            // Left column
            DrawLeftColumn(gfx, product, ox + pad, oy + pad, leftW - pad * 2, h - pad * 2);

            // Right column
            var rightX = ox + leftW;
            DrawRightColumn(gfx, product, rightX, oy, rightW - pad, h);
        }

        private void DrawLeftColumn(XGraphics gfx, Product product, double x, double y, double w, double h)
        {
            var maxTwoLinesHeightPt = MmToPt(h * 0.5);
            var pnSize = _settings.ProductNameFontSizePt;
            var minSize = Math.Max(6, _settings.ProductNameMinFontSizePt);
            var xPt = MmToPt(x); var yPt = MmToPt(y); var wPt = MmToPt(w);

            // ProductName: max 2 lines, auto-reduce font
            var trySize = Math.Max(pnSize, minSize);
            XFont pnFont = new XFont(_settings.ProductNameFontFamily, trySize,
                _settings.ProductNameBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var pnRect = new XRect(xPt, yPt, wPt, maxTwoLinesHeightPt);
            var pnSizeMeasured = gfx.MeasureString(product.ProductName ?? "", pnFont, XStringFormats.TopLeft);
            for (trySize = pnSize; trySize >= minSize; trySize -= 1)
            {
                pnFont = new XFont(_settings.ProductNameFontFamily, trySize,
                    _settings.ProductNameBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                pnSizeMeasured = gfx.MeasureString(product.ProductName ?? "", pnFont, XStringFormats.TopLeft);
                if (pnSizeMeasured.Height <= maxTwoLinesHeightPt + 0.5) break;
            }
            gfx.DrawString(product.ProductName ?? "", pnFont, XBrushes.Black, pnRect, XStringFormats.TopLeft);
            yPt += pnSizeMeasured.Height + 2;

            // VariantText: max 1 line, truncate with ellipsis
            var vtFont = new XFont(_settings.VariantTextFontFamily, _settings.VariantTextFontSizePt,
                _settings.VariantTextBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var vtText = product.VariantText ?? "";
            var vtMeasured = gfx.MeasureString(vtText, vtFont, XStringFormats.TopLeft);
            if (vtMeasured.Width > wPt)
            {
                while (vtText.Length > 1 && gfx.MeasureString(vtText + "...", vtFont, XStringFormats.TopLeft).Width > wPt)
                    vtText = vtText.Substring(0, vtText.Length - 1);
                vtText = vtText + "...";
            }
            gfx.DrawString(vtText, vtFont, XBrushes.Black, new XRect(xPt, yPt, wPt, vtMeasured.Height), XStringFormats.TopLeft);
        }

        private void DrawRightColumn(XGraphics gfx, Product product, double x, double y, double w, double h)
        {
            var topH = _settings.RightTopHeightMm;
            var midH = _settings.RightMiddleHeightMm;
            var botH = _settings.RightBottomHeightMm;
            var total = topH + midH + botH;
            if (total <= 0)
            {
                topH = h / 3;
                midH = h / 3;
                botH = h / 3;
            }
            else
            {
                var scale = h / total;
                topH *= scale;
                midH *= scale;
                botH *= scale;
            }
            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;
            var pen = new XPen(XColors.Black, MmToPt(borderMm));
            var xPt = MmToPt(x); var yPt = MmToPt(y); var wPt = MmToPt(w);

            if (_settings.ShowSeparatorBetweenPacks)
                gfx.DrawLine(pen, xPt, yPt + MmToPt(topH), xPt + wPt, yPt + MmToPt(topH));
            if (_settings.ShowBottomSeparator)
                gfx.DrawLine(pen, xPt, yPt + MmToPt(topH + midH), xPt + wPt, yPt + MmToPt(topH + midH));

            var pad = _settings.PaddingMm;

            DrawSection(gfx, product.SmallPackLabel ?? "", Formatting.FormatPrice(product.SmallPackPrice),
                x + pad, y, w - pad * 2, topH);
            DrawSection(gfx, product.LargePackLabel ?? "", Formatting.FormatPrice(product.LargePackPrice),
                x + pad, y + topH, w - pad * 2, midH);

            var unitText = string.IsNullOrWhiteSpace(product.UnitPriceText)
                ? Formatting.FormatUnitPrice(product.UnitPricePerKg)
                : product.UnitPriceText;
            var uFont = new XFont(_settings.UnitPriceSmallFontFamily, _settings.UnitPriceSmallFontSizePt,
                _settings.UnitPriceSmallBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            gfx.DrawString(unitText, uFont, XBrushes.Black,
                new XRect(MmToPt(x + pad), MmToPt(y + topH + midH), MmToPt(w - pad * 2), MmToPt(botH)), XStringFormats.Center);
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

            var xPt = MmToPt(x); var yPt = MmToPt(y); var wPt = MmToPt(w); var hPt = MmToPt(h);
            gfx.DrawString(label, labelFont, XBrushes.Black, new XRect(xPt, yPt, wPt, hPt / 2), labelFormat);
            gfx.DrawString(price, priceFont, XBrushes.Black, new XRect(xPt, yPt + hPt / 2, wPt, hPt / 2), priceFormat);
        }

        public void DrawCropMarks(XGraphics gfx, double offsetXMm, double offsetYMm, double cropLenMm = 3)
        {
            var ox = MmToPt(offsetXMm + _settings.OffsetXMm);
            var oy = MmToPt(offsetYMm + _settings.OffsetYMm);
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var w = MmToPt(labelWidthMm);
            var h = MmToPt(labelHeightMm);
            var cropLen = MmToPt(cropLenMm);
            var pen = new XPen(XColors.Black, 0.2);

            gfx.DrawLine(pen, ox, oy, ox + cropLen, oy);
            gfx.DrawLine(pen, ox, oy, ox, oy + cropLen);
            gfx.DrawLine(pen, ox + w - cropLen, oy, ox + w, oy);
            gfx.DrawLine(pen, ox + w, oy, ox + w, oy + cropLen);
            gfx.DrawLine(pen, ox, oy + h - cropLen, ox, oy + h);
            gfx.DrawLine(pen, ox, oy + h, ox + cropLen, oy + h);
            gfx.DrawLine(pen, ox + w - cropLen, oy + h, ox + w, oy + h);
            gfx.DrawLine(pen, ox + w, oy + h - cropLen, ox + w, oy + h);
        }
    }
}
