using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
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
            var rightWidthMm = _settings.RightColumnWidthMm > 0 ? _settings.RightColumnWidthMm : 58;
            rightWidthMm = Math.Max(22, Math.Min(labelWidthMm * 0.55, rightWidthMm));

            var ox = Units.MmToWpfUnits(offsetXMm);
            var oy = Units.MmToWpfUnits(offsetYMm);
            var w = Units.MmToWpfUnits(labelWidthMm);
            var h = Units.MmToWpfUnits(labelHeightMm);
            var rect = new Rect(ox, oy, w, h);
            var scaleX = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleX));
            var scaleY = Math.Max(0.90, Math.Min(1.10, _settings.CalibrationScaleY));
            var offX = Units.MmToWpfUnits(Math.Max(-5, Math.Min(5, _settings.OffsetXMm)));
            var offY = Units.MmToWpfUnits(Math.Max(-5, Math.Min(5, _settings.OffsetYMm)));

            dc.PushTransform(new TranslateTransform(offX, offY));
            dc.PushTransform(new ScaleTransform(scaleX, scaleY, rect.Left + rect.Width / 2.0, rect.Top + rect.Height / 2.0));

            var borderMm = _settings.BorderThicknessMm > 0 ? _settings.BorderThicknessMm : 0.2;
            var borderW = Units.MmToWpfUnits(borderMm);
            var pen = new Pen(Brushes.Black, borderW);
            var half = borderW / 2.0;
            dc.DrawRectangle(null, pen, new Rect(rect.Left + half, rect.Top + half, Math.Max(0, rect.Width - borderW), Math.Max(0, rect.Height - borderW)));

            var pad = Units.MmToWpfUnits(Math.Max(1.2, _settings.PaddingMm));
            var gap = Units.MmToWpfUnits(1.6);
            var content = new Rect(rect.Left + pad, rect.Top + pad, Math.Max(0, rect.Width - pad * 2), Math.Max(0, rect.Height - pad * 2));

            var rightW = Units.MmToWpfUnits(rightWidthMm);
            rightW = Math.Min(content.Width * 0.6, rightW);
            var leftW = Math.Max(0, content.Width - rightW - gap);

            var leftRect = new Rect(content.Left, content.Top, leftW, content.Height);
            var rightRect = new Rect(leftRect.Right + gap, content.Top, rightW, content.Height);

            DrawTitleBlock(dc, product, leftRect);
            DrawPackBlock(dc, product, rightRect, labelWidthMm, labelHeightMm);
            dc.Pop();
            dc.Pop();
        }

        private void DrawTitleBlock(DrawingContext dc, Product product, Rect rect)
        {
            var titleArea = new Rect(rect.Left, rect.Top, rect.Width, rect.Height * 0.72);
            DrawWrappedFit(dc, product.ProductName ?? "", titleArea, _settings.ProductNameFontFamily, 16, 9.5, 3, true);

            var variantArea = new Rect(rect.Left, titleArea.Bottom + Units.MmToWpfUnits(0.8), rect.Width, Math.Max(0, rect.Bottom - titleArea.Bottom));
            DrawWrappedFit(dc, product.VariantText ?? "", variantArea, _settings.VariantTextFontFamily, 8.8, 7.2, 2, false);
        }

        private void DrawPackBlock(DrawingContext dc, Product product, Rect rect, double labelWidthMm, double labelHeightMm)
        {
            var hasBarcode = product.BarcodeEnabled
                && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var quiet = Units.MmToWpfUnits(RetailLayoutConfig.QuietZoneMm);
            var barcodeTextH = hasBarcode ? Units.MmToWpfUnits(2.6) : 0;
            var barcodeHmm = RetailLayoutConfig.Clamp(
                labelHeightMm * RetailLayoutConfig.WideBarcodeHeightRatio,
                RetailLayoutConfig.WideBarcodeHeightMinMm,
                RetailLayoutConfig.WideBarcodeHeightMaxMm);
            var barcodeWmm = RetailLayoutConfig.Clamp(
                (rect.Width / Units.MmToWpfUnits(1)) * RetailLayoutConfig.WideBarcodeWidthRatio,
                RetailLayoutConfig.WideBarcodeWidthMinMm,
                RetailLayoutConfig.WideBarcodeWidthMaxMm);

            var barcodeH = hasBarcode ? Units.MmToWpfUnits(barcodeHmm) : 0;
            var barcodeW = hasBarcode ? Units.MmToWpfUnits(barcodeWmm) : 0;
            var barcodeAreaH = hasBarcode ? barcodeH + barcodeTextH + Units.MmToWpfUnits(0.8) : 0;
            var sectionsRect = new Rect(rect.Left, rect.Top, rect.Width, Math.Max(0, rect.Height - barcodeAreaH));

            var sectionGap = Units.MmToWpfUnits(1.0);
            var sectionH = Math.Max(0, (sectionsRect.Height - sectionGap) / 2.0);
            var smallRect = new Rect(sectionsRect.Left, sectionsRect.Top, sectionsRect.Width, sectionH);
            var largeRect = new Rect(sectionsRect.Left, smallRect.Bottom + sectionGap, sectionsRect.Width, sectionH);

            var linePen = new Pen(Brushes.Black, Units.MmToWpfUnits(0.12));
            if (_settings.ShowSeparatorBetweenPacks)
                dc.DrawLine(linePen, new Point(sectionsRect.Left, smallRect.Bottom), new Point(sectionsRect.Right, smallRect.Bottom));
            if (_settings.ShowBottomSeparator && hasBarcode)
                dc.DrawLine(linePen, new Point(sectionsRect.Left, sectionsRect.Bottom), new Point(sectionsRect.Right, sectionsRect.Bottom));

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

            DrawSection(dc, smallRect, "MALÉ BALENIE", smallLines, false);
            DrawSection(dc, largeRect, "VEĽKÉ BALENIE", largeLines, true);

            if (!hasBarcode) return;

            var bcX = Math.Max(rect.Left + quiet, rect.Right - barcodeW - quiet);
            var bcY = rect.Bottom - barcodeAreaH + Units.MmToWpfUnits(0.2);
            var bcW = Math.Min(barcodeW, rect.Width - quiet * 2);
            var barRect = new Rect(bcX, bcY, bcW, barcodeH);
            BarcodeRenderer.DrawToWpf(dc, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13", barRect, false);
            DrawBarcodeText(dc, BarcodeRenderer.NormalizeBarcodeValue(product.BarcodeValue, product.BarcodeFormat), barRect);
        }

        private void DrawSection(DrawingContext dc, Rect rect, string header, IReadOnlyList<string> lines, bool emphasizePrice)
        {
            var y = rect.Top + Units.MmToWpfUnits(0.3);
            DrawSingle(dc, header, new Rect(rect.Left, y, rect.Width, Units.MmToWpfUnits(3.2)), "Arial", 6.8, true, false);
            y += Units.MmToWpfUnits(3.2);

            for (var i = 0; i < lines.Count; i++)
            {
                if (y > rect.Bottom - Units.MmToWpfUnits(2.8)) break;
                var isPriceLine = emphasizePrice && i == 0;
                DrawSingle(dc, lines[i], new Rect(rect.Left, y, rect.Width, Units.MmToWpfUnits(isPriceLine ? 4.5 : 3.2)),
                    isPriceLine ? _settings.PriceBigFontFamily : "Arial",
                    isPriceLine ? 10.6 : 6.8,
                    isPriceLine,
                    isPriceLine);
                y += Units.MmToWpfUnits(isPriceLine ? 4.2 : 2.9);
            }
        }

        private void DrawBarcodeText(DrawingContext dc, string text, Rect barRect)
        {
            var ft = CreateFormatted(text ?? "", "Arial", RetailLayoutConfig.BarcodeTextPt, false);
            ft.MaxTextWidth = barRect.Width;
            ft.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(ft, new Point(barRect.Left + (barRect.Width - ft.Width) / 2.0, barRect.Bottom + 1));
        }

        private void DrawWrappedFit(DrawingContext dc, string text, Rect rect, string family, double maxPt, double minPt, int maxLines, bool bold)
        {
            if (string.IsNullOrWhiteSpace(text) || rect.Width <= 0 || rect.Height <= 0) return;
            for (var pt = maxPt; pt >= minPt; pt -= 0.5)
            {
                var lineHeight = 0.0;
                var lines = WrapLines(text, family, pt, bold, rect.Width, out lineHeight);
                if (lines.Count > maxLines)
                {
                    lines = lines.Take(maxLines).ToList();
                    lines[maxLines - 1] = Ellipsize(lines[maxLines - 1], family, pt, bold, rect.Width);
                }
                if (lineHeight * lines.Count <= rect.Height + 0.5)
                {
                    var y = rect.Top;
                    foreach (var line in lines)
                    {
                        DrawSingle(dc, line, new Rect(rect.Left, y, rect.Width, lineHeight), family, pt, bold, false);
                        y += lineHeight;
                    }
                    return;
                }
            }
            DrawSingle(dc, Ellipsize(text, family, minPt, bold, rect.Width), rect, family, minPt, bold, false);
        }

        private List<string> WrapLines(string text, string family, double pt, bool bold, double maxWidth, out double lineHeight)
        {
            var parts = new List<string>();
            var words = (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = "";
            lineHeight = CreateFormatted("X", family, pt, bold).Height;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (CreateFormatted(candidate, family, pt, bold).Width <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    if (!string.IsNullOrEmpty(current)) parts.Add(current);
                    current = word;
                }
            }
            if (!string.IsNullOrEmpty(current)) parts.Add(current);
            if (parts.Count == 0) parts.Add("");
            return parts;
        }

        private string Ellipsize(string text, string family, double pt, bool bold, double maxWidth)
        {
            var t = (text ?? "").Trim();
            if (CreateFormatted(t, family, pt, bold).Width <= maxWidth) return t;
            while (t.Length > 1 && CreateFormatted(t + "...", family, pt, bold).Width > maxWidth)
                t = t.Substring(0, t.Length - 1);
            return t + "...";
        }

        private void DrawSingle(DrawingContext dc, string text, Rect rect, string family, double pt, bool bold, bool rightAlign)
        {
            var ft = CreateFormatted(text ?? "", family, pt, bold);
            ft.MaxTextWidth = rect.Width;
            ft.Trimming = TextTrimming.CharacterEllipsis;
            var x = rightAlign ? rect.Right - ft.Width : rect.Left;
            dc.DrawText(ft, new Point(x, rect.Top));
        }

        private static string FormatWeight(decimal? value, string? unit)
        {
            if (!value.HasValue) return "-";
            var u = string.Equals(unit, "g", StringComparison.OrdinalIgnoreCase) ? "g" : "kg";
            return value.Value.ToString("0.###") + " " + u;
        }

        private FormattedText CreateFormatted(string text, string family, double pt, bool bold)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily(family), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(text ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, pt * 96.0 / 72.0, Brushes.Black, dpi);
        }
    }
}
