using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using PdfSharp.Drawing;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Shared A4 sheet renderer for both WPF printing and PDF export.
    /// Applies global A4 calibration transform once per page.
    /// </summary>
    public class A4SheetRenderer
    {
        private static double MmToPt(double mm) => XUnit.FromMillimeter(mm).Point;

        public void DrawWpfPage(DrawingContext dc, A4PackedPage page, TemplateSettings baseSettings, A4SheetSettings sheetSettings)
        {
            var s = NormalizeSheetSettings(sheetSettings);
            var pageW = Units.MmToWpfUnits(s.SheetWidthMm);
            var pageH = Units.MmToWpfUnits(s.SheetHeightMm);
            var centerX = pageW / 2.0;
            var centerY = pageH / 2.0;
            var scale = new ScaleTransform(s.CalibrationScaleX, s.CalibrationScaleY, centerX, centerY);
            var translate = new TranslateTransform(
                Units.MmToWpfUnits(s.CalibrationOffsetXmm),
                Units.MmToWpfUnits(s.CalibrationOffsetYmm));
            var tg = new TransformGroup();
            // Required order: scale then translate.
            tg.Children.Add(scale);
            tg.Children.Add(translate);

            dc.PushTransform(tg);
            if (s.DebugLayout)
                DrawWpfDebugOverlay(dc, page, s);
            foreach (var item in page.Items)
            {
                var labelSettings = CreateLabelSettings(baseSettings, item.Template);
                var renderer = new LabelRenderer(labelSettings);
                renderer.Draw(dc, item.Product, item.Xmm, item.Ymm);
            }
            dc.Pop();
        }

        public void DrawPdfPage(XGraphics gfx, A4PackedPage page, TemplateSettings baseSettings, A4SheetSettings sheetSettings)
        {
            var s = NormalizeSheetSettings(sheetSettings);
            var pageW = MmToPt(s.SheetWidthMm);
            var pageH = MmToPt(s.SheetHeightMm);
            var cx = pageW / 2.0;
            var cy = pageH / 2.0;

            var state = gfx.Save();
            // Required order: scale then translate.
            gfx.TranslateTransform(cx, cy);
            gfx.ScaleTransform(s.CalibrationScaleX, s.CalibrationScaleY);
            gfx.TranslateTransform(-cx, -cy);
            gfx.TranslateTransform(MmToPt(s.CalibrationOffsetXmm), MmToPt(s.CalibrationOffsetYmm));

            if (s.DebugLayout)
                DrawPdfDebugOverlay(gfx, page, s);
            foreach (var item in page.Items)
            {
                var labelSettings = CreateLabelSettings(baseSettings, item.Template);
                var renderer = new PdfLabelRenderer(labelSettings);
                renderer.Draw(gfx, item.Product, item.Xmm, item.Ymm);
                if (labelSettings.CropMarksEnabled)
                    renderer.DrawCropMarks(gfx, item.Xmm, item.Ymm);
            }

            gfx.Restore(state);
        }

        private static TemplateSettings CreateLabelSettings(TemplateSettings baseSettings, LabelTemplate template)
        {
            var s = CloneSettings(baseSettings);
            s.LabelWidthMm = template.WidthMm;
            s.LabelHeightMm = template.HeightMm;
            s.PaddingMm = template.PaddingMm;
            // A4 renderer uses global sheet calibration only.
            s.OffsetXMm = 0;
            s.OffsetYMm = 0;
            s.CalibrationScaleX = 1.0;
            s.CalibrationScaleY = 1.0;
            return s;
        }

        private static TemplateSettings CloneSettings(TemplateSettings settings)
        {
            var json = JsonSerializer.Serialize(settings ?? new TemplateSettings());
            return JsonSerializer.Deserialize<TemplateSettings>(json) ?? new TemplateSettings();
        }

        private static A4SheetSettings NormalizeSheetSettings(A4SheetSettings settings)
        {
            var s = settings ?? new A4SheetSettings();
            s.SheetWidthMm = s.SheetWidthMm > 0 ? s.SheetWidthMm : A4Layout.A4WidthMm;
            s.SheetHeightMm = s.SheetHeightMm > 0 ? s.SheetHeightMm : A4Layout.A4HeightMm;
            s.CalibrationScaleX = Clamp(s.CalibrationScaleX, 0.95, 1.05);
            s.CalibrationScaleY = Clamp(s.CalibrationScaleY, 0.95, 1.05);
            s.CalibrationOffsetXmm = Clamp(s.CalibrationOffsetXmm, -5, 5);
            s.CalibrationOffsetYmm = Clamp(s.CalibrationOffsetYmm, -5, 5);
            return s;
        }

        private static void DrawWpfDebugOverlay(DrawingContext dc, A4PackedPage page, A4SheetSettings s)
        {
            var printableRect = new Rect(
                Units.MmToWpfUnits(s.SheetMarginMm),
                Units.MmToWpfUnits(s.SheetMarginMm),
                Units.MmToWpfUnits(Math.Max(0, s.SheetWidthMm - 2 * s.SheetMarginMm)),
                Units.MmToWpfUnits(Math.Max(0, s.SheetHeightMm - 2 * s.SheetMarginMm)));
            dc.DrawRectangle(null, new Pen(Brushes.DarkGray, 0.8), printableRect);

            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            for (var i = 0; i < page.Items.Count; i++)
            {
                var item = page.Items[i];
                var r = new Rect(
                    Units.MmToWpfUnits(item.Xmm),
                    Units.MmToWpfUnits(item.Ymm),
                    Units.MmToWpfUnits(item.Template.WidthMm),
                    Units.MmToWpfUnits(item.Template.HeightMm));
                dc.DrawRectangle(null, new Pen(Brushes.LightSlateGray, 0.6), r);
                var ft = new FormattedText((i + 1).ToString(), System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, tf, 9, Brushes.DimGray, dpi);
                dc.DrawText(ft, new Point(r.Left + 2, r.Top + 2));
            }
        }

        private static void DrawPdfDebugOverlay(XGraphics gfx, A4PackedPage page, A4SheetSettings s)
        {
            var printableRect = new XRect(
                MmToPt(s.SheetMarginMm),
                MmToPt(s.SheetMarginMm),
                MmToPt(Math.Max(0, s.SheetWidthMm - 2 * s.SheetMarginMm)),
                MmToPt(Math.Max(0, s.SheetHeightMm - 2 * s.SheetMarginMm)));
            gfx.DrawRectangle(new XPen(XColors.DarkGray, 0.4), printableRect);

            var font = new XFont("Arial", 7);
            for (var i = 0; i < page.Items.Count; i++)
            {
                var item = page.Items[i];
                var r = new XRect(MmToPt(item.Xmm), MmToPt(item.Ymm), MmToPt(item.Template.WidthMm), MmToPt(item.Template.HeightMm));
                gfx.DrawRectangle(new XPen(XColors.LightSlateGray, 0.3), r);
                gfx.DrawString((i + 1).ToString(), font, XBrushes.DimGray, r.X + 2, r.Y + 8);
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
