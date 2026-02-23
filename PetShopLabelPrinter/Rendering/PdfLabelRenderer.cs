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
            var isWide = labelWidthMm >= RetailLayoutConfig.WideThresholdMm;
            var isNarrow = labelWidthMm < RetailLayoutConfig.NarrowThresholdMm;
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

            if (isWide)
                RenderRetailWide(gfx, product, ox, oy, w, h, labelWidthMm, labelHeightMm);
            else
                RenderRetailNarrow(gfx, product, ox, oy, w, h, labelWidthMm, labelHeightMm, isNarrow);

            gfx.Restore(state);
        }

        private void RenderRetailWide(XGraphics gfx, Product product, double ox, double oy, double w, double h, double labelWidthMm, double labelHeightMm)
        {
            var pad = _settings.PaddingMm;
            var gapMm = RetailLayoutConfig.Clamp(labelWidthMm * RetailLayoutConfig.GapRatio, RetailLayoutConfig.GapMinMm, RetailLayoutConfig.GapMaxMm);
            var contentX = ox + pad;
            var contentY = oy + pad;
            var contentW = Math.Max(0, w - pad * 2);
            var contentH = Math.Max(0, h - pad * 2);

            var leftRatio = RetailLayoutConfig.Clamp(RetailLayoutConfig.WideLeftRatio, RetailLayoutConfig.WideLeftMinRatio, RetailLayoutConfig.WideLeftMaxRatio);
            var leftW = contentW * leftRatio;
            var rightW = Math.Max(0, contentW - leftW - gapMm);
            var leftX = contentX;
            var rightX = leftX + leftW + gapMm;

            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));
            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var metaHmm = hasMeta ? 3.2 : 0;
            DrawTwoLineTitle(gfx, product.ProductName ?? "", leftX, contentY, leftW, Math.Max(0, contentH - metaHmm - 4.2), _settings.ProductNameFontFamily, 15, 10, true);
            DrawSingleLinePdf(gfx, product.VariantText ?? "", leftX, contentY + (contentH * 0.58), leftW, 4.2, _settings.VariantTextFontFamily, 8.5, false);
            if (!string.IsNullOrWhiteSpace(product.Notes))
                DrawSingleLinePdf(gfx, product.Notes ?? "", leftX, contentY + (contentH * 0.70), leftW, 3.6, "Arial", 6.4, false);
            if (hasMeta)
                DrawMetaRow(gfx, product, leftX, contentY + contentH - metaHmm, leftW);

            var line1H = contentH * 0.22;
            var line2H = contentH * 0.24;
            var l1Y = contentY;
            var l2Y = l1Y + line1H;
            var sepPen = new XPen(XColors.Black, MmToPt(0.12));
            gfx.DrawLine(sepPen, MmToPt(rightX), MmToPt(l1Y + line1H), MmToPt(rightX + rightW), MmToPt(l1Y + line1H));

            DrawSingleLinePdf(gfx, product.SmallPackLabel ?? "", rightX, l1Y, rightW * 0.58, line1H, "Arial", 7.2, false);
            DrawSingleLinePdf(gfx, Formatting.FormatPrice(product.SmallPackPrice), rightX + rightW * 0.58, l1Y, rightW * 0.42, line1H, _settings.PriceBigFontFamily, 9.2, true);
            DrawSingleLinePdf(gfx, product.LargePackLabel ?? "", rightX, l2Y, rightW * 0.56, line2H, "Arial", 8.0, false);
            DrawSingleLinePdf(gfx, Formatting.FormatPrice(product.LargePackPrice), rightX + rightW * 0.56, l2Y, rightW * 0.44, line2H, _settings.PriceBigFontFamily, 11.2, true);

            var bcHmm = hasBarcode
                ? RetailLayoutConfig.Clamp(labelHeightMm * RetailLayoutConfig.WideBarcodeHeightRatio, RetailLayoutConfig.WideBarcodeHeightMinMm, RetailLayoutConfig.WideBarcodeHeightMaxMm)
                : 0;
            var bcWmm = hasBarcode
                ? RetailLayoutConfig.Clamp(rightW * RetailLayoutConfig.WideBarcodeWidthRatio, RetailLayoutConfig.WideBarcodeWidthMinMm, RetailLayoutConfig.WideBarcodeWidthMaxMm)
                : 0;
            var bcTextHmm = hasBarcode && product.BarcodeShowText ? 2.8 : 0;
            var bcY = contentY + contentH - bcHmm - bcTextHmm - 0.8;
            var bcX = Math.Max(rightX + RetailLayoutConfig.QuietZoneMm, rightX + rightW - bcWmm - 1.0);
            var bcW = Math.Min(bcWmm, rightW - RetailLayoutConfig.QuietZoneMm * 2);
            var unitText = string.IsNullOrWhiteSpace(product.UnitPriceText)
                ? Formatting.FormatUnitPrice(product.UnitPricePerKg)
                : product.UnitPriceText;
            DrawSingleLinePdf(gfx, unitText, rightX, bcY - 4.2, rightW, 4.2, "Arial", 7.0, true);
            if (hasBarcode)
                DrawRetailBarcodePdf(gfx, product, bcX, bcY, bcW, bcHmm, product.BarcodeShowText);
        }

        private void RenderRetailNarrow(XGraphics gfx, Product product, double ox, double oy, double w, double h, double labelWidthMm, double labelHeightMm, bool isNarrow)
        {
            var pad = _settings.PaddingMm;
            var gapMm = RetailLayoutConfig.Clamp(labelWidthMm * RetailLayoutConfig.GapRatio, RetailLayoutConfig.GapMinMm, RetailLayoutConfig.GapMaxMm);
            var contentX = ox + pad;
            var contentY = oy + pad;
            var contentW = Math.Max(0, w - pad * 2);
            var contentH = Math.Max(0, h - pad * 2);

            var leftW = contentW * RetailLayoutConfig.NarrowLeftRatio;
            var rightW = Math.Max(0, contentW - leftW - gapMm);
            var leftX = contentX;
            var rightX = leftX + leftW + gapMm;

            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));
            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var metaHmm = hasMeta ? 2.8 : 0;
            DrawTwoLineTitle(gfx, product.ProductName ?? "", leftX, contentY, leftW, Math.Max(0, contentH - metaHmm - 3.8), _settings.ProductNameFontFamily, 12, 9, true);
            DrawSingleLinePdf(gfx, product.VariantText ?? "", leftX, contentY + (contentH * 0.60), leftW, 3.8, _settings.VariantTextFontFamily, 7.2, false);
            if (hasMeta)
                DrawMetaRow(gfx, product, leftX, contentY + contentH - metaHmm, leftW);

            DrawSingleLinePdf(gfx, Formatting.FormatPrice(product.SmallPackPrice), rightX, contentY, rightW, 5.0, _settings.PriceBigFontFamily, 9.2, true);
            DrawSingleLinePdf(gfx, Formatting.FormatPrice(product.LargePackPrice), rightX, contentY + 4.8, rightW, 5.0, _settings.PriceBigFontFamily, 10.0, true);

            if (hasBarcode)
            {
                var bcWmm = RetailLayoutConfig.Clamp(contentW * RetailLayoutConfig.NarrowBarcodeWidthRatio, RetailLayoutConfig.NarrowBarcodeWidthMinMm, RetailLayoutConfig.NarrowBarcodeWidthMaxMm);
                var bcHmm = RetailLayoutConfig.Clamp(labelHeightMm * RetailLayoutConfig.NarrowBarcodeHeightRatio, RetailLayoutConfig.NarrowBarcodeHeightMinMm, RetailLayoutConfig.NarrowBarcodeHeightMaxMm);
                var bcTextHmm = product.BarcodeShowText ? 2.6 : 0;
                var bcX = rightW >= bcWmm + 2 ? (rightX + rightW - bcWmm - 1) : (contentX + (contentW - bcWmm) / 2.0);
                var bcY = contentY + contentH - bcHmm - bcTextHmm - 0.6;
                DrawRetailBarcodePdf(gfx, product, bcX, bcY, bcWmm, bcHmm, product.BarcodeShowText);
            }
        }

        private void DrawRetailBarcodePdf(XGraphics gfx, Product product, double xMm, double yMm, double wMm, double hMm, bool showText)
        {
            BarcodeRenderer.DrawToPdf(gfx, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13", xMm, yMm, wMm, hMm, false);
            if (!showText) return;
            var text = BarcodeRenderer.NormalizeBarcodeValue(product.BarcodeValue, product.BarcodeFormat);
            var font = new XFont("Arial", RetailLayoutConfig.BarcodeTextPt, XFontStyleEx.Regular);
            gfx.DrawString(text ?? "", font, XBrushes.Black, new XRect(MmToPt(xMm), MmToPt(yMm + hMm + 0.2), MmToPt(wMm), MmToPt(2.8)), XStringFormats.TopCenter);
        }

        private void DrawSingleLinePdf(XGraphics gfx, string text, double xMm, double yMm, double wMm, double hMm, string family, double pt, bool rightAlign)
        {
            var font = new XFont(family, pt, XFontStyleEx.Regular);
            var format = rightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft;
            gfx.DrawString(text ?? "", font, XBrushes.Black, new XRect(MmToPt(xMm), MmToPt(yMm), MmToPt(wMm), MmToPt(hMm)), format);
        }

        private void DrawTwoLineTitle(XGraphics gfx, string text, double xMm, double yMm, double wMm, double hMm, string family, double maxPt, double minPt, bool bold)
        {
            var style = bold ? XFontStyleEx.Bold : XFontStyleEx.Regular;
            var maxWidthPt = MmToPt(wMm);
            var maxHeightPt = MmToPt(hMm);
            var linesToDraw = new List<string>();
            XFont best = new XFont(family, minPt, style);
            for (var pt = maxPt; pt >= minPt; pt -= 1)
            {
                var font = new XFont(family, pt, style);
                var lines = WrapTextToLines(gfx, text ?? "", font, maxWidthPt);
                if (lines.Count > 2)
                    lines = new List<string> { lines[0], lines[1] + "..." };
                var lineHeight = gfx.MeasureString("X", font, XStringFormats.TopLeft).Height;
                if (lines.Count * lineHeight <= maxHeightPt + 0.5)
                {
                    best = font;
                    linesToDraw = lines;
                    break;
                }
                linesToDraw = lines;
                best = font;
            }

            if (linesToDraw.Count == 0) linesToDraw.Add(text ?? "");
            var yPt = MmToPt(yMm);
            var xPt = MmToPt(xMm);
            var linePt = gfx.MeasureString("X", best, XStringFormats.TopLeft).Height;
            for (var i = 0; i < Math.Min(2, linesToDraw.Count); i++)
            {
                gfx.DrawString(linesToDraw[i], best, XBrushes.Black, new XRect(xPt, yPt + i * linePt, maxWidthPt, linePt), XStringFormats.TopLeft);
            }
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
