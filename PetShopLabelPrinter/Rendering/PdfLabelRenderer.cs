using System;
using System.Collections.Generic;
using System.Linq;
using PdfSharp.Drawing;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
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
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : 58;
            rightWidthMm = Math.Max(22, Math.Min(labelWidthMm * 0.55, rightWidthMm));
            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : 0.2;

            var ox = offsetXMm;
            var oy = offsetYMm;
            var w = labelWidthMm;
            var h = labelHeightMm;

            var scaleX = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleX));
            var scaleY = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleY));
            var offsetX = Math.Max(-5, Math.Min(5, _settings.OffsetXMm));
            var offsetY = Math.Max(-5, Math.Min(5, _settings.OffsetYMm));
            var cx = MmToPt(ox + w / 2.0);
            var cy = MmToPt(oy + h / 2.0);
            var state = gfx.Save();
            gfx.TranslateTransform(MmToPt(offsetX), MmToPt(offsetY));
            gfx.TranslateTransform(cx, cy);
            gfx.ScaleTransform(scaleX, scaleY);
            gfx.TranslateTransform(-cx, -cy);

            var borderPt = MmToPt(borderMm);
            var pen = new XPen(XColors.Black, borderPt);
            var half = borderPt / 2.0;
            gfx.DrawRectangle(pen, MmToPt(ox) + half, MmToPt(oy) + half, Math.Max(0, MmToPt(w) - borderPt), Math.Max(0, MmToPt(h) - borderPt));

            var pad = Math.Max(1.2, _settings.PaddingMm);
            var gap = 1.6;
            var contentX = ox + pad;
            var contentY = oy + pad;
            var contentW = Math.Max(0, w - pad * 2);
            var contentH = Math.Max(0, h - pad * 2);
            var rightW = Math.Min(contentW * 0.6, rightWidthMm);
            var leftW = Math.Max(0, contentW - rightW - gap);
            var leftX = contentX;
            var rightX = leftX + leftW + gap;

            DrawTitleBlock(gfx, product, leftX, contentY, leftW, contentH);
            DrawPackBlock(gfx, product, rightX, contentY, rightW, contentH, labelWidthMm, labelHeightMm);

            gfx.Restore(state);
        }

        private void DrawTitleBlock(XGraphics gfx, Product product, double x, double y, double w, double h)
        {
            DrawWrappedFit(gfx, product.ProductName ?? "", x, y, w, h * 0.72, _settings.ProductNameFontFamily, 16, 9.5, 3, true);
            DrawWrappedFit(gfx, product.VariantText ?? "", x, y + h * 0.72 + 0.8, w, Math.Max(0, h * 0.28 - 0.8), _settings.VariantTextFontFamily, 8.8, 7.2, 2, false);
        }

        private void DrawPackBlock(XGraphics gfx, Product product, double x, double y, double w, double h, double labelWidthMm, double labelHeightMm)
        {
            var hasBarcode = product.BarcodeEnabled
                && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var quiet = RetailLayoutConfig.QuietZoneMm;
            var barcodeTextH = hasBarcode ? 2.6 : 0;
            var barcodeHmm = RetailLayoutConfig.Clamp(labelHeightMm * RetailLayoutConfig.WideBarcodeHeightRatio, RetailLayoutConfig.WideBarcodeHeightMinMm, RetailLayoutConfig.WideBarcodeHeightMaxMm);
            var barcodeWmm = RetailLayoutConfig.Clamp(w * RetailLayoutConfig.WideBarcodeWidthRatio, RetailLayoutConfig.WideBarcodeWidthMinMm, RetailLayoutConfig.WideBarcodeWidthMaxMm);
            var barcodeAreaH = hasBarcode ? barcodeHmm + barcodeTextH + 0.8 : 0;

            var sectionsH = Math.Max(0, h - barcodeAreaH);
            var sectionGap = 1.0;
            var sectionH = Math.Max(0, (sectionsH - sectionGap) / 2.0);
            var smallY = y;
            var largeY = smallY + sectionH + sectionGap;

            var linePen = new XPen(XColors.Black, MmToPt(0.12));
            if (_settings.ShowSeparatorBetweenPacks)
                gfx.DrawLine(linePen, MmToPt(x), MmToPt(smallY + sectionH), MmToPt(x + w), MmToPt(smallY + sectionH));
            if (_settings.ShowBottomSeparator && hasBarcode)
                gfx.DrawLine(linePen, MmToPt(x), MmToPt(y + sectionsH), MmToPt(x + w), MmToPt(y + sectionsH));

            var smallLines = new List<string>
            {
                "Cena: " + Formatting.FormatPrice(product.SmallPackPrice),
                "Hmotnosť: " + FormatWeight(product.PackWeightValue, product.PackWeightUnit)
            };
            var unitText = string.IsNullOrWhiteSpace(product.UnitPriceText) ? Formatting.FormatUnitPrice(product.UnitPricePerKg) : product.UnitPriceText;
            var largeLines = new List<string>
            {
                "Cena: " + Formatting.FormatPrice(product.LargePackPrice),
                "Hmotnosť: " + FormatWeight(product.LargePackWeightValue, product.LargePackWeightUnit),
                unitText
            };

            DrawSection(gfx, x, smallY, w, sectionH, "MALÉ BALENIE", smallLines, false);
            DrawSection(gfx, x, largeY, w, sectionH, "VEĽKÉ BALENIE", largeLines, true);

            if (!hasBarcode) return;
            var bcW = Math.Min(barcodeWmm, w - quiet * 2);
            var bcX = Math.Max(x + quiet, x + w - bcW - quiet);
            var bcY = y + h - barcodeAreaH + 0.2;
            BarcodeRenderer.DrawToPdf(gfx, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13", bcX, bcY, bcW, barcodeHmm, false);
            var text = BarcodeRenderer.NormalizeBarcodeValue(product.BarcodeValue, product.BarcodeFormat) ?? "";
            var font = new XFont("Arial", RetailLayoutConfig.BarcodeTextPt, XFontStyleEx.Regular);
            gfx.DrawString(text, font, XBrushes.Black, new XRect(MmToPt(bcX), MmToPt(bcY + barcodeHmm + 0.2), MmToPt(bcW), MmToPt(2.6)), XStringFormats.TopCenter);
        }

        private void DrawSection(XGraphics gfx, double x, double y, double w, double h, string header, IReadOnlyList<string> lines, bool emphasizePrice)
        {
            var headerFont = new XFont("Arial", 6.8, XFontStyleEx.Bold);
            gfx.DrawString(header, headerFont, XBrushes.Black, new XRect(MmToPt(x), MmToPt(y + 0.3), MmToPt(w), MmToPt(3.2)), XStringFormats.TopLeft);
            var cy = y + 3.2;
            for (var i = 0; i < lines.Count; i++)
            {
                if (cy > y + h - 2.8) break;
                var isPrice = emphasizePrice && i == 0;
                var font = new XFont(isPrice ? _settings.PriceBigFontFamily : "Arial", isPrice ? 10.6 : 6.8, isPrice ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                var lineH = isPrice ? 4.2 : 2.9;
                var align = isPrice ? XStringFormats.TopRight : XStringFormats.TopLeft;
                gfx.DrawString(lines[i], font, XBrushes.Black, new XRect(MmToPt(x), MmToPt(cy), MmToPt(w), MmToPt(lineH)), align);
                cy += lineH;
            }
        }

        private void DrawWrappedFit(XGraphics gfx, string text, double x, double y, double w, double h, string family, double maxPt, double minPt, int maxLines, bool bold)
        {
            if (string.IsNullOrWhiteSpace(text) || w <= 0 || h <= 0) return;
            var style = bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
            for (var pt = maxPt; pt >= minPt; pt -= 0.5)
            {
                var font = new XFont(family, pt, style);
                var lineHeight = gfx.MeasureString("X", font, XStringFormats.TopLeft).Height;
                var lines = WrapLines(gfx, text, font, MmToPt(w));
                if (lines.Count > maxLines)
                {
                    lines = lines.Take(maxLines).ToList();
                    lines[maxLines - 1] = Ellipsize(gfx, lines[maxLines - 1], font, MmToPt(w));
                }
                if (lineHeight * lines.Count <= MmToPt(h) + 0.5)
                {
                    var yy = MmToPt(y);
                    foreach (var line in lines)
                    {
                        gfx.DrawString(line, font, XBrushes.Black, new XRect(MmToPt(x), yy, MmToPt(w), lineHeight), XStringFormats.TopLeft);
                        yy += lineHeight;
                    }
                    return;
                }
            }
            var fallback = new XFont(family, minPt, style);
            gfx.DrawString(Ellipsize(gfx, text, fallback, MmToPt(w)), fallback, XBrushes.Black, new XRect(MmToPt(x), MmToPt(y), MmToPt(w), MmToPt(h)), XStringFormats.TopLeft);
        }

        private static List<string> WrapLines(XGraphics gfx, string text, XFont font, double maxWidthPt)
        {
            var lines = new List<string>();
            var words = (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            foreach (var word in words)
            {
                var test = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (gfx.MeasureString(test, font, XStringFormats.TopLeft).Width <= maxWidthPt)
                    current = test;
                else
                {
                    if (!string.IsNullOrEmpty(current)) lines.Add(current);
                    current = word;
                }
            }
            if (!string.IsNullOrEmpty(current)) lines.Add(current);
            if (lines.Count == 0) lines.Add("");
            return lines;
        }

        private static string Ellipsize(XGraphics gfx, string text, XFont font, double maxWidthPt)
        {
            var t = (text ?? "").Trim();
            if (gfx.MeasureString(t, font, XStringFormats.TopLeft).Width <= maxWidthPt) return t;
            while (t.Length > 1 && gfx.MeasureString(t + "...", font, XStringFormats.TopLeft).Width > maxWidthPt)
                t = t.Substring(0, t.Length - 1);
            return t + "...";
        }

        private static string FormatWeight(decimal? value, string? unit)
        {
            if (!value.HasValue) return "-";
            var u = string.Equals(unit, "g", StringComparison.OrdinalIgnoreCase) ? "g" : "kg";
            return value.Value.ToString("0.###") + " " + u;
        }

        public void DrawCropMarks(XGraphics gfx, double offsetXMm, double offsetYMm, double cropLenMm = 3)
        {
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var scaleX = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleX));
            var scaleY = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleY));
            var offsetX = Math.Max(-5, Math.Min(5, _settings.OffsetXMm));
            var offsetY = Math.Max(-5, Math.Min(5, _settings.OffsetYMm));
            var cx = MmToPt(offsetXMm + labelWidthMm / 2.0);
            var cy = MmToPt(offsetYMm + labelHeightMm / 2.0);
            var state = gfx.Save();
            gfx.TranslateTransform(MmToPt(offsetX), MmToPt(offsetY));
            gfx.TranslateTransform(cx, cy);
            gfx.ScaleTransform(scaleX, scaleY);
            gfx.TranslateTransform(-cx, -cy);

            var ox = MmToPt(offsetXMm);
            var oy = MmToPt(offsetYMm);
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
            gfx.Restore(state);
        }
    }
}
