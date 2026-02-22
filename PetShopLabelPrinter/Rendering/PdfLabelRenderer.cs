using System;
using System.Collections.Generic;
using PdfSharp.Drawing;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Renders labels to PDF using PdfSharp. ProductName: max 2 lines, auto-reduce font.
    /// VariantText: multiline wrap within label boundary.
    /// </summary>
    public class PdfLabelRenderer
    {
        private static double MmToPt(double mm) => XUnit.FromMillimeter(mm).Point;

        private static List<string> WrapTextToLines(XGraphics gfx, string text, XFont font, double maxWidthPt)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            var paragraphs = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var para in paragraphs)
            {
                var words = para.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) { lines.Add(""); continue; }
                var line = "";
                foreach (var word in words)
                {
                    var test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (gfx.MeasureString(test, font, XStringFormats.TopLeft).Width <= maxWidthPt)
                        line = test;
                    else
                    {
                        if (!string.IsNullOrEmpty(line)) { lines.Add(line); line = ""; }
                        if (gfx.MeasureString(word, font, XStringFormats.TopLeft).Width <= maxWidthPt)
                            line = word;
                        else
                        {
                            foreach (var c in word)
                            {
                                var tc = line + c;
                                if (gfx.MeasureString(tc, font, XStringFormats.TopLeft).Width > maxWidthPt && line.Length > 0)
                                { lines.Add(line); line = c.ToString(); }
                                else line = tc;
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
            return lines;
        }

        private readonly TemplateSettings _settings;

        public PdfLabelRenderer(TemplateSettings settings)
        {
            _settings = settings ?? new TemplateSettings();
        }

        public void Draw(XGraphics gfx, Product product, double offsetXMm, double offsetYMm)
        {
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var isNarrow = labelWidthMm < 90;
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : _settings.RightColWidthMm;
            if (isNarrow)
                rightWidthMm = Math.Max(20, Math.Min(26, labelWidthMm * 0.32));
            rightWidthMm = Math.Max(10, Math.Min(labelWidthMm - 10, rightWidthMm));
            var leftWidthMm = labelWidthMm - rightWidthMm;
            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : _settings.LineThicknessMm;

            var ox = offsetXMm;
            var oy = offsetYMm;
            var w = labelWidthMm;
            var h = labelHeightMm;
            var scaleX = Clamp(_settings.CalibrationScaleX, 0.90, 1.10);
            var scaleY = Clamp(_settings.CalibrationScaleY, 0.90, 1.10);
            var offsetX = Clamp(_settings.OffsetXMm, -5, 5);
            var offsetY = Clamp(_settings.OffsetYMm, -5, 5);
            var cx = MmToPt(ox + w / 2.0);
            var cy = MmToPt(oy + h / 2.0);

            var state = gfx.Save();
            // Final output calibration transform: scale around label center, then translate by mm offsets.
            gfx.TranslateTransform(MmToPt(offsetX), MmToPt(offsetY));
            gfx.TranslateTransform(cx, cy);
            gfx.ScaleTransform(scaleX, scaleY);
            gfx.TranslateTransform(-cx, -cy);

            // Outer border (points)
            var borderPt = MmToPt(borderMm);
            var pen = new XPen(XColors.Black, borderPt);
            // Keep border fully inside allocated label rect.
            var half = borderPt / 2.0;
            gfx.DrawRectangle(pen, MmToPt(ox) + half, MmToPt(oy) + half, Math.Max(0, MmToPt(w) - borderPt), Math.Max(0, MmToPt(h) - borderPt));

            var pad = _settings.PaddingMm;
            var leftW = leftWidthMm;
            var rightW = rightWidthMm;

            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;
            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));

            var layout = LabelBottomLayout.ComputeLayout(labelWidthMm, labelHeightMm, pad, hasBarcode, hasMeta);
            var bottomReserveMm = layout.BarcodeAreaHeightMm + layout.MetaAreaHeightMm;
            var mainH = layout.MainContentHeightMm;

            DrawLeftColumn(gfx, product, ox + pad, oy + pad, leftW - pad * 2, mainH, isNarrow);

            var rightX = ox + leftW;
            DrawRightColumn(gfx, product, rightX, oy, rightW - pad, h - bottomReserveMm);

            var bottomY = oy + h - pad - bottomReserveMm;
            if (layout.ShowBarcode)
            {
                var qz = layout.QuietZoneMm;
                var contentW = w - pad * 2 - qz * 2;
                var bcWmm = isNarrow
                    ? Clamp((labelWidthMm - _settings.PaddingMm * 2 - qz * 2) * 0.65, 38, 55)
                    : (labelWidthMm - _settings.PaddingMm * 2 - qz * 2);
                var bcW = Math.Min(contentW, bcWmm);
                var bcX = ox + pad + qz + Math.Max(0, (contentW - bcW) / 2.0);
                var bcH = layout.BarcodeHeightMm;
                BarcodeRenderer.DrawToPdf(gfx, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13",
                    bcX, bottomY, bcW, bcH, product.BarcodeShowText);
                bottomY += layout.BarcodeAreaHeightMm;
            }
            if (layout.ShowMeta)
            {
                DrawMetaRow(gfx, product, ox + pad, bottomY, w - pad * 2);
            }
            gfx.Restore(state);
        }

        private void DrawMetaRow(XGraphics gfx, Product product, double x, double y, double w)
        {
            var line1Parts = new List<string>();
            if (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean)) line1Parts.Add("EAN: " + product.Ean);
            if (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku)) line1Parts.Add("SKU: " + product.Sku);
            var line1 = line1Parts.Count > 0 ? string.Join("  |  ", line1Parts) : null;
            var line2 = product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate)
                ? "SP: " + FormatExpiry(product.ExpiryDate) : null;

            var font = new XFont("Arial", LabelBottomLayout.MetaFontSizePt);
            var lineH = LabelBottomLayout.MetaLineHeightMm;
            var xPt = MmToPt(x); var yPt = MmToPt(y); var wPt = MmToPt(w);

            if (!string.IsNullOrEmpty(line1))
            {
                gfx.DrawString(line1, font, XBrushes.Black, new XRect(xPt, yPt, wPt, MmToPt(lineH)), XStringFormats.TopLeft);
                yPt += MmToPt(lineH);
            }
            if (!string.IsNullOrEmpty(line2))
            {
                gfx.DrawString(line2, font, XBrushes.Black, new XRect(xPt, yPt, wPt, MmToPt(lineH)), XStringFormats.TopLeft);
            }
        }

        private static string FormatExpiry(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "";
            if (DateTime.TryParse(iso, out var d)) return d.ToString("dd.MM.yyyy");
            return iso;
        }

        private void DrawLeftColumn(XGraphics gfx, Product product, double x, double y, double w, double h, bool isNarrow)
        {
            var maxTwoLinesHeightPt = MmToPt(h * (isNarrow ? 0.62 : 0.5));
            var pnSize = _settings.ProductNameFontSizePt;
            var minSize = Math.Max(isNarrow ? 9 : 6, _settings.ProductNameMinFontSizePt);
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

            // VariantText: multiline wrap within label boundary
            var vtFont = new XFont(_settings.VariantTextFontFamily, _settings.VariantTextFontSizePt,
                _settings.VariantTextBold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            var vtText = product.VariantText ?? "";
            var lineHeightPt = gfx.MeasureString("X", vtFont, XStringFormats.TopLeft).Height;
            var lines = WrapTextToLines(gfx, vtText, vtFont, wPt);
            foreach (var line in lines)
            {
                if (yPt + lineHeightPt > MmToPt(y + h)) break;
                gfx.DrawString(line, vtFont, XBrushes.Black, new XRect(xPt, yPt, wPt, lineHeightPt), XStringFormats.TopLeft);
                yPt += lineHeightPt;
            }
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
            var labelWidthMm = _settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = _settings.LabelHeightMm > 0 ? _settings.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var scaleX = Clamp(_settings.CalibrationScaleX, 0.90, 1.10);
            var scaleY = Clamp(_settings.CalibrationScaleY, 0.90, 1.10);
            var offsetX = Clamp(_settings.OffsetXMm, -5, 5);
            var offsetY = Clamp(_settings.OffsetYMm, -5, 5);
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

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
