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
            var isWide = labelWidthMm >= RetailLayoutConfig.WideThresholdMm;
            var isNarrow = labelWidthMm < RetailLayoutConfig.NarrowThresholdMm
                || ((_settings.LabelWidthMm > 0 ? _settings.LabelWidthMm : LabelWidthMm) < RetailLayoutConfig.NarrowThresholdMm);

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

            if (isWide)
                RenderRetailWide(dc, product, rect, labelWidthMm, labelHeightMm);
            else
                RenderRetailNarrow(dc, product, rect, labelWidthMm, labelHeightMm, isNarrow);

            dc.Pop();
            dc.Pop();
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void RenderRetailWide(DrawingContext dc, Product product, Rect rect, double labelWidthMm, double labelHeightMm)
        {
            var pad = Units.MmToWpfUnits(_settings.PaddingMm);
            var gapMm = RetailLayoutConfig.Clamp(labelWidthMm * RetailLayoutConfig.GapRatio, RetailLayoutConfig.GapMinMm, RetailLayoutConfig.GapMaxMm);
            var gap = Units.MmToWpfUnits(gapMm);
            var content = new Rect(rect.Left + pad, rect.Top + pad, Math.Max(0, rect.Width - pad * 2), Math.Max(0, rect.Height - pad * 2));

            var leftRatio = RetailLayoutConfig.Clamp(RetailLayoutConfig.WideLeftRatio, RetailLayoutConfig.WideLeftMinRatio, RetailLayoutConfig.WideLeftMaxRatio);
            var leftW = content.Width * leftRatio;
            var rightW = Math.Max(0, content.Width - leftW - gap);
            var leftRect = new Rect(content.Left, content.Top, leftW, content.Height);
            var rightRect = new Rect(leftRect.Right + gap, content.Top, rightW, content.Height);

            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));
            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var metaHeightMm = hasMeta ? 3.2 : 0;
            var metaH = Units.MmToWpfUnits(metaHeightMm);

            // LEFT: title + variant + notes, meta at bottom-left.
            var topLeft = new Rect(leftRect.Left, leftRect.Top, leftRect.Width, Math.Max(0, leftRect.Height - metaH));
            DrawLineFitText(dc, product.ProductName ?? "", topLeft, _settings.ProductNameFontFamily, 15, 10, true, 2, Brushes.Black);
            var variantY = topLeft.Top + topLeft.Height * 0.58;
            DrawSingleLine(dc, product.VariantText ?? "", new Rect(topLeft.Left, variantY, topLeft.Width, Units.MmToWpfUnits(4.2)), _settings.VariantTextFontFamily, 8.5, false);
            if (!string.IsNullOrWhiteSpace(product.Notes))
                DrawSingleLine(dc, product.Notes ?? "", new Rect(topLeft.Left, variantY + Units.MmToWpfUnits(4.2), topLeft.Width, Units.MmToWpfUnits(3.6)), "Arial", 6.4, false);
            if (hasMeta)
                DrawMetaRow(dc, product, leftRect.Left, leftRect.Bottom - metaH, leftRect.Width);

            // RIGHT: pack lines + prices + unit price + barcode bottom-right.
            var line1H = rightRect.Height * 0.22;
            var line2H = rightRect.Height * 0.24;
            var unitH = Units.MmToWpfUnits(4.2);
            var sepPen = new Pen(Brushes.Black, Units.MmToWpfUnits(0.12));
            var l1Rect = new Rect(rightRect.Left, rightRect.Top, rightRect.Width, line1H);
            var l2Rect = new Rect(rightRect.Left, l1Rect.Bottom, rightRect.Width, line2H);
            dc.DrawLine(sepPen, new Point(l1Rect.Left, l1Rect.Bottom), new Point(l1Rect.Right, l1Rect.Bottom));

            DrawSingleLine(dc, product.SmallPackLabel ?? "", new Rect(l1Rect.Left, l1Rect.Top + 1, l1Rect.Width * 0.58, l1Rect.Height), "Arial", 7.2, false);
            DrawSingleLine(dc, Formatting.FormatPrice(product.SmallPackPrice), new Rect(l1Rect.Left + l1Rect.Width * 0.58, l1Rect.Top + 1, l1Rect.Width * 0.42, l1Rect.Height), _settings.PriceBigFontFamily, 9.2, true);
            DrawSingleLine(dc, product.LargePackLabel ?? "", new Rect(l2Rect.Left, l2Rect.Top + 1, l2Rect.Width * 0.56, l2Rect.Height), "Arial", 8.0, false);
            DrawSingleLine(dc, Formatting.FormatPrice(product.LargePackPrice), new Rect(l2Rect.Left + l2Rect.Width * 0.56, l2Rect.Top + 1, l2Rect.Width * 0.44, l2Rect.Height), _settings.PriceBigFontFamily, 11.2, true);

            var barcodeH = hasBarcode
                ? Units.MmToWpfUnits(RetailLayoutConfig.Clamp(labelHeightMm * RetailLayoutConfig.WideBarcodeHeightRatio, RetailLayoutConfig.WideBarcodeHeightMinMm, RetailLayoutConfig.WideBarcodeHeightMaxMm))
                : 0;
            var barcodeW = hasBarcode
                ? Units.MmToWpfUnits(RetailLayoutConfig.Clamp((rightRect.Width / Units.MmToWpfUnits(1)) * RetailLayoutConfig.WideBarcodeWidthRatio, RetailLayoutConfig.WideBarcodeWidthMinMm, RetailLayoutConfig.WideBarcodeWidthMaxMm))
                : 0;

            var barcodeTextH = hasBarcode && product.BarcodeShowText ? Units.MmToWpfUnits(2.8) : 0;
            var bcTotal = barcodeH + barcodeTextH;
            var bcX = rightRect.Right - barcodeW - Units.MmToWpfUnits(1.0);
            var bcY = rightRect.Bottom - bcTotal - Units.MmToWpfUnits(0.8);
            var bcRect = new Rect(
                Math.Max(rightRect.Left + Units.MmToWpfUnits(RetailLayoutConfig.QuietZoneMm), bcX),
                bcY,
                Math.Min(barcodeW, rightRect.Width - Units.MmToWpfUnits(RetailLayoutConfig.QuietZoneMm * 2)),
                barcodeH);

            var unitText = string.IsNullOrWhiteSpace(product.UnitPriceText)
                ? Formatting.FormatUnitPrice(product.UnitPricePerKg)
                : product.UnitPriceText;
            DrawSingleLine(dc, unitText, new Rect(rightRect.Left, bcRect.Top - unitH - 1, rightRect.Width, unitH), "Arial", 7.0, true);

            if (hasBarcode)
                DrawRetailBarcodeWpf(dc, product, bcRect, product.BarcodeShowText);
        }

        private void RenderRetailNarrow(DrawingContext dc, Product product, Rect rect, double labelWidthMm, double labelHeightMm, bool isNarrow)
        {
            var pad = Units.MmToWpfUnits(_settings.PaddingMm);
            var gapMm = RetailLayoutConfig.Clamp(labelWidthMm * RetailLayoutConfig.GapRatio, RetailLayoutConfig.GapMinMm, RetailLayoutConfig.GapMaxMm);
            var gap = Units.MmToWpfUnits(gapMm);
            var content = new Rect(rect.Left + pad, rect.Top + pad, Math.Max(0, rect.Width - pad * 2), Math.Max(0, rect.Height - pad * 2));

            var leftW = content.Width * RetailLayoutConfig.NarrowLeftRatio;
            var rightW = Math.Max(0, content.Width - leftW - gap);
            var leftRect = new Rect(content.Left, content.Top, leftW, content.Height);
            var rightRect = new Rect(leftRect.Right + gap, content.Top, rightW, content.Height);

            var hasMeta = (product.ShowEan && !string.IsNullOrWhiteSpace(product.Ean))
                || (product.ShowSku && !string.IsNullOrWhiteSpace(product.Sku))
                || (product.ShowExpiry && !string.IsNullOrWhiteSpace(product.ExpiryDate));
            var hasBarcode = product.BarcodeEnabled && !string.IsNullOrWhiteSpace(product.BarcodeValue)
                && BarcodeRenderer.ValidateBarcodeValue(product.BarcodeValue, product.BarcodeFormat).IsValid;

            var metaH = hasMeta ? Units.MmToWpfUnits(2.8) : 0;
            var titleRect = new Rect(leftRect.Left, leftRect.Top, leftRect.Width, Math.Max(0, leftRect.Height - metaH - Units.MmToWpfUnits(4.2)));
            DrawLineFitText(dc, product.ProductName ?? "", titleRect, _settings.ProductNameFontFamily, 12, 9, true, 2, Brushes.Black);
            DrawSingleLine(dc, product.VariantText ?? "", new Rect(leftRect.Left, titleRect.Bottom, leftRect.Width, Units.MmToWpfUnits(3.8)), _settings.VariantTextFontFamily, 7.2, false);
            if (hasMeta)
                DrawMetaRow(dc, product, leftRect.Left, leftRect.Bottom - metaH, leftRect.Width);

            DrawSingleLine(dc, Formatting.FormatPrice(product.SmallPackPrice), new Rect(rightRect.Left, rightRect.Top, rightRect.Width, Units.MmToWpfUnits(5.0)), _settings.PriceBigFontFamily, 9.2, true);
            DrawSingleLine(dc, Formatting.FormatPrice(product.LargePackPrice), new Rect(rightRect.Left, rightRect.Top + Units.MmToWpfUnits(4.8), rightRect.Width, Units.MmToWpfUnits(5.0)), _settings.PriceBigFontFamily, 10.0, true);

            if (hasBarcode)
            {
                var bcWmm = RetailLayoutConfig.Clamp((content.Width / Units.MmToWpfUnits(1)) * RetailLayoutConfig.NarrowBarcodeWidthRatio, RetailLayoutConfig.NarrowBarcodeWidthMinMm, RetailLayoutConfig.NarrowBarcodeWidthMaxMm);
                var bcHmm = RetailLayoutConfig.Clamp(labelHeightMm * RetailLayoutConfig.NarrowBarcodeHeightRatio, RetailLayoutConfig.NarrowBarcodeHeightMinMm, RetailLayoutConfig.NarrowBarcodeHeightMaxMm);
                var bcTextH = product.BarcodeShowText ? Units.MmToWpfUnits(2.6) : 0;
                var bcW = Units.MmToWpfUnits(bcWmm);
                var bcH = Units.MmToWpfUnits(bcHmm);
                var bcX = rightRect.Width >= bcW + Units.MmToWpfUnits(2)
                    ? rightRect.Right - bcW - Units.MmToWpfUnits(1)
                    : content.Left + (content.Width - bcW) / 2.0;
                var bcY = content.Bottom - bcH - bcTextH - Units.MmToWpfUnits(0.6);
                var bcRect = new Rect(bcX, bcY, bcW, bcH);
                DrawRetailBarcodeWpf(dc, product, bcRect, product.BarcodeShowText);
            }
        }

        private void DrawRetailBarcodeWpf(DrawingContext dc, Product product, Rect barRect, bool showText)
        {
            BarcodeRenderer.DrawToWpf(dc, product.BarcodeValue!, product.BarcodeFormat ?? "EAN13", barRect, false);
            if (!showText) return;

            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var text = BarcodeRenderer.NormalizeBarcodeValue(product.BarcodeValue, product.BarcodeFormat);
            var typeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var size = RetailLayoutConfig.BarcodeTextPt * 96.0 / 72.0;
            var ft = new FormattedText(text ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, dpi);
            ft.MaxTextWidth = barRect.Width;
            ft.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(ft, new Point(barRect.Left + (barRect.Width - ft.Width) / 2.0, barRect.Bottom + 1));
        }

        private void DrawLineFitText(DrawingContext dc, string text, Rect rect, string family, double maxPt, double minPt, bool bold, int maxLines, Brush brush)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily(family), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            FormattedText? best = null;
            for (var pt = maxPt; pt >= minPt; pt -= 1)
            {
                var ft = new FormattedText(text ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, pt * 96.0 / 72.0, brush, dpi);
                ft.MaxTextWidth = rect.Width;
                ft.MaxTextHeight = rect.Height;
                ft.Trimming = TextTrimming.CharacterEllipsis;
                var lineHeight = Math.Max(1, ft.Height / Math.Max(1, ft.Text.Length > 0 ? 1 : 1));
                if (ft.Height <= rect.Height + 1 && ft.Height <= lineHeight * maxLines + 2) { best = ft; break; }
                best = ft;
            }
            if (best != null) dc.DrawText(best, new Point(rect.Left, rect.Top));
        }

        private void DrawSingleLine(DrawingContext dc, string text, Rect rect, string family, double pt, bool rightAlign)
        {
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily(family), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var ft = new FormattedText(text ?? "", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, pt * 96.0 / 72.0, Brushes.Black, dpi);
            ft.MaxTextWidth = rect.Width;
            ft.MaxTextHeight = rect.Height;
            ft.Trimming = TextTrimming.CharacterEllipsis;
            var x = rightAlign ? rect.Right - ft.Width : rect.Left;
            dc.DrawText(ft, new Point(x, rect.Top));
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
