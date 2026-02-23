using System;
using System.Collections.Generic;
using System.Printing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Services
{
    public class PrintService
    {
        private readonly Database _db;

        public PrintService(Database db)
        {
            _db = db;
        }

        public string? GetDefaultPrinter()
        {
            var preferred = _db.GetSetting("PreferredPrinterName");
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;
            return _db.GetSetting("PrinterName");
        }

        public void SetDefaultPrinter(string? name)
        {
            var value = name ?? "";
            _db.SetSetting("PreferredPrinterName", value);
            _db.SetSetting("PrinterName", value); // backward compatibility for existing reads
        }

        public bool IsVirtualPrinter(string? printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) return true;
            return IsVirtualPrinterName(printerName);
        }

        public List<string> GetInstalledPrinters()
        {
            var list = new List<string>();
            try
            {
                var server = new LocalPrintServer();
                var queues = server.GetPrintQueues();
                foreach (var q in queues)
                {
                    if (!q.IsOffline && !q.IsNotAvailable)
                        list.Add(q.Name);
                }
            }
            catch { }
            return list;
        }

        public bool PrintSilent(IReadOnlyList<QueuedLabel> queue, TemplateSettings? effectiveSettings = null)
        {
            if (queue == null || queue.Count == 0) return false;

            try
            {
                var printDialog = new PrintDialog();
                var pq = ResolveRealPrintQueue(printDialog);
                if (pq == null) return false;

                printDialog.PrintQueue = pq;
                printDialog.PrintTicket = pq.DefaultPrintTicket;
                // Best effort: force no implicit driver scaling from app side.
                if (printDialog.PrintTicket != null)
                    printDialog.PrintTicket.PageScalingFactor = 100;

                var settings = effectiveSettings ?? _db.GetTemplateSettings();
                var a4 = _db.GetA4SheetSettings();
                var jobs = BuildJobs(queue, settings);
                var fitErrors = A4Layout.ValidateTemplateFit(jobs, a4);
                if (fitErrors.Count > 0)
                    throw new InvalidOperationException("Niektoré šablóny sa nezmestia do printable area:\n- " + string.Join("\n- ", fitErrors));
                var packed = A4Layout.Pack(jobs, a4);
                var paginator = new LabelDocumentPaginator(settings, a4, packed);
                printDialog.PrintDocument(paginator, "Pet Shop Labels");
                return true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Chyba tlače: " + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private List<LabelPrintJob> BuildJobs(IReadOnlyList<QueuedLabel> queue, TemplateSettings settings)
        {
            var templates = _db.GetLabelTemplates().ToDictionary(t => t.Id, t => t);
            var fallbackTemplate = new LabelTemplate
            {
                Id = 0,
                Name = "Default",
                WidthMm = settings.LabelWidthMm > 0 ? settings.LabelWidthMm : 150,
                HeightMm = settings.LabelHeightMm > 0 ? settings.LabelHeightMm : 38,
                PaddingMm = settings.PaddingMm > 0 ? settings.PaddingMm : 2
            };

            var jobs = new List<LabelPrintJob>();
            foreach (var q in queue)
            {
                if (q?.Product == null) continue;
                var copies = q.Quantity < 1 ? 1 : q.Quantity;
                var resolvedTemplateId = q.Product.TemplateId ?? q.TemplateId;
                var tpl = (resolvedTemplateId.HasValue && templates.TryGetValue(resolvedTemplateId.Value, out var found))
                    ? found
                    : fallbackTemplate;
                jobs.Add(new LabelPrintJob
                {
                    ProductId = q.Product.Id,
                    TemplateId = tpl.Id,
                    Product = q.Product,
                    Template = tpl,
                    Copies = copies
                });
            }
            return jobs;
        }

        public bool PrintCalibrationTestPage(TemplateSettings settings, LabelTemplate template)
        {
            try
            {
                var printDialog = new PrintDialog();
                var pq = ResolveRealPrintQueue(printDialog);
                if (pq == null) return false;

                printDialog.PrintQueue = pq;
                printDialog.PrintTicket = pq.DefaultPrintTicket;
                if (printDialog.PrintTicket != null)
                    printDialog.PrintTicket.PageScalingFactor = 100;
                var paginator = new CalibrationTestDocumentPaginator(_db.GetA4SheetSettings(), template);
                printDialog.PrintDocument(paginator, "Pet Shop Calibration Test");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool PrintA4TestPage(A4SheetSettings settings)
        {
            try
            {
                var printDialog = new PrintDialog();
                var pq = ResolveRealPrintQueue(printDialog);
                if (pq == null) return false;

                printDialog.PrintQueue = pq;
                printDialog.PrintTicket = pq.DefaultPrintTicket;
                if (printDialog.PrintTicket != null)
                    printDialog.PrintTicket.PageScalingFactor = 100;
                var paginator = new A4SettingsTestDocumentPaginator(settings);
                printDialog.PrintDocument(paginator, "A4 Settings Test");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private PrintQueue? ResolveRealPrintQueue(PrintDialog printDialog)
        {
            var server = new LocalPrintServer();
            var queues = server.GetPrintQueues().ToList();
            var realQueues = queues.Where(q => !q.IsOffline && !q.IsNotAvailable && !IsVirtualPrinterName(q.Name)).ToList();
            if (realQueues.Count == 0)
            {
                MessageBox.Show("Nenájdená reálna tlačiareň. Pripojte tlačiareň alebo zvoľte inú.", "Tlač", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var preferred = GetDefaultPrinter();
            var preferredQueue = TryGetQueue(queues, preferred);
            if (preferredQueue != null && !IsVirtualPrinterName(preferredQueue.Name) && !preferredQueue.IsOffline && !preferredQueue.IsNotAvailable)
                return preferredQueue;

            var defaultQueue = TryGetQueue(queues, server.DefaultPrintQueue?.Name);
            if (defaultQueue != null && !IsVirtualPrinterName(defaultQueue.Name) && !defaultQueue.IsOffline && !defaultQueue.IsNotAvailable)
                return defaultQueue;

            while (true)
            {
                if (printDialog.ShowDialog() != true || printDialog.PrintQueue == null)
                    return null;
                var selected = TryGetQueue(queues, printDialog.PrintQueue.Name) ?? printDialog.PrintQueue;
                if (selected != null && !IsVirtualPrinterName(selected.Name))
                {
                    SetDefaultPrinter(selected.Name);
                    return selected;
                }
                MessageBox.Show("Vyberte reálnu tlačiareň (nie PDF/XPS/OneNote/Fax).", "Tlač", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static PrintQueue? TryGetQueue(IEnumerable<PrintQueue> queues, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var q in queues)
            {
                if (string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase))
                    return q;
            }
            return null;
        }

        private static bool IsVirtualPrinterName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("XPS", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("OneNote", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Fax", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal class LabelDocumentPaginator : DocumentPaginator
    {
        private readonly TemplateSettings _settings;
        private readonly A4SheetSettings _sheetSettings;
        private readonly A4PackResult _packed;
        private readonly A4SheetRenderer _sheetRenderer = new A4SheetRenderer();
        private readonly Size _pageSize;

        public LabelDocumentPaginator(TemplateSettings settings, A4SheetSettings sheetSettings, A4PackResult packed)
        {
            _settings = settings;
            _sheetSettings = sheetSettings;
            _packed = packed;
            _pageSize = new Size(
                Units.MmToWpfUnits(A4Layout.A4WidthMm),
                Units.MmToWpfUnits(A4Layout.A4HeightMm));
        }

        public override DocumentPage GetPage(int pageNumber)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, _pageSize.Width, _pageSize.Height));
                if (pageNumber >= 0 && pageNumber < _packed.Pages.Count)
                    _sheetRenderer.DrawWpfPage(dc, _packed.Pages[pageNumber], _settings, _sheetSettings);
            }

            dv.Transform = new TranslateTransform(0, 0);
            return new DocumentPage(dv, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
        }

        public override bool IsPageCountValid => true;
        public override int PageCount => _packed.PageCount;
        public override Size PageSize { get => _pageSize; set { } }
        public override IDocumentPaginatorSource Source => null!;
    }

    internal class CalibrationTestDocumentPaginator : DocumentPaginator
    {
        private readonly A4SheetSettings _sheet;
        private readonly LabelTemplate _template;
        private readonly Size _pageSize;

        public CalibrationTestDocumentPaginator(A4SheetSettings sheet, LabelTemplate template)
        {
            _sheet = sheet ?? new A4SheetSettings();
            _template = template;
            _pageSize = new Size(
                Units.MmToWpfUnits(A4Layout.A4WidthMm),
                Units.MmToWpfUnits(A4Layout.A4HeightMm));
        }

        public override DocumentPage GetPage(int pageNumber)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, _pageSize.Width, _pageSize.Height));

                var marginMm = 20.0;
                var labelXmm = marginMm;
                var labelYmm = marginMm;
                var labelWmm = _template.WidthMm;
                var labelHmm = _template.HeightMm;
                var paddingMm = _template.PaddingMm;

                var labelRect = new Rect(
                    Units.MmToWpfUnits(labelXmm),
                    Units.MmToWpfUnits(labelYmm),
                    Units.MmToWpfUnits(labelWmm),
                    Units.MmToWpfUnits(labelHmm));

                var sx = Clamp(_sheet.CalibrationScaleX, 0.95, 1.05);
                var sy = Clamp(_sheet.CalibrationScaleY, 0.95, 1.05);
                var tx = Units.MmToWpfUnits(Clamp(_sheet.CalibrationOffsetXmm, -5, 5));
                var ty = Units.MmToWpfUnits(Clamp(_sheet.CalibrationOffsetYmm, -5, 5));

                dc.PushTransform(new TranslateTransform(tx, ty));
                dc.PushTransform(new ScaleTransform(sx, sy, labelRect.Left + labelRect.Width / 2.0, labelRect.Top + labelRect.Height / 2.0));

                var outerPen = new Pen(Brushes.Black, 1.0);
                dc.DrawRectangle(null, outerPen, labelRect);

                var innerRect = new Rect(
                    labelRect.Left + Units.MmToWpfUnits(paddingMm),
                    labelRect.Top + Units.MmToWpfUnits(paddingMm),
                    Math.Max(0, labelRect.Width - Units.MmToWpfUnits(paddingMm * 2)),
                    Math.Max(0, labelRect.Height - Units.MmToWpfUnits(paddingMm * 2)));
                var innerPen = new Pen(Brushes.Gray, 0.8);
                dc.DrawRectangle(null, innerPen, innerRect);

                DrawRulerX(dc, labelRect.Left, labelRect.Top - Units.MmToWpfUnits(6), 50);
                DrawRulerY(dc, labelRect.Left - Units.MmToWpfUnits(6), labelRect.Top, 50);

                var cx = labelRect.Left + labelRect.Width / 2.0;
                var cy = labelRect.Top + labelRect.Height / 2.0;
                var chLen = Units.MmToWpfUnits(4);
                var crossPen = new Pen(Brushes.Black, 0.8);
                dc.DrawLine(crossPen, new Point(cx - chLen, cy), new Point(cx + chLen, cy));
                dc.DrawLine(crossPen, new Point(cx, cy - chLen), new Point(cx, cy + chLen));

                dc.Pop();
                dc.Pop();

                var dpi = 1.0;
                try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
                var tf = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var text = $"Šablóna: {_template.Name} | {_template.WidthMm:0.##} x {_template.HeightMm:0.##} mm | " +
                           $"A4 Margin={_sheet.SheetMarginMm:0.##} Gap={_sheet.GapMm:0.##} mm | " +
                           $"A4 OffX={_sheet.CalibrationOffsetXmm:0.##} OffY={_sheet.CalibrationOffsetYmm:0.##} mm | " +
                           $"A4 ScaleX={_sheet.CalibrationScaleX * 100.0:0.##}% ScaleY={_sheet.CalibrationScaleY * 100.0:0.##}%";
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, tf, 12, Brushes.Black, dpi);
                dc.DrawText(ft, new Point(Units.MmToWpfUnits(10), Units.MmToWpfUnits(8)));
            }

            return new DocumentPage(dv, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
        }

        private static void DrawRulerX(DrawingContext dc, double x, double y, int maxMm)
        {
            var pen = new Pen(Brushes.Black, 0.8);
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            for (var mm = 0; mm <= maxMm; mm++)
            {
                var xx = x + Units.MmToWpfUnits(mm);
                var tick = mm % 10 == 0 ? Units.MmToWpfUnits(3) : Units.MmToWpfUnits(1.5);
                dc.DrawLine(pen, new Point(xx, y), new Point(xx, y + tick));
                if (mm % 10 == 0)
                {
                    var ft = new FormattedText(mm.ToString(), System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight, tf, 8, Brushes.Black, dpi);
                    dc.DrawText(ft, new Point(xx - 2, y - Units.MmToWpfUnits(3.5)));
                }
            }
        }

        private static void DrawRulerY(DrawingContext dc, double x, double y, int maxMm)
        {
            var pen = new Pen(Brushes.Black, 0.8);
            var dpi = 1.0;
            try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
            var tf = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            for (var mm = 0; mm <= maxMm; mm++)
            {
                var yy = y + Units.MmToWpfUnits(mm);
                var tick = mm % 10 == 0 ? Units.MmToWpfUnits(3) : Units.MmToWpfUnits(1.5);
                dc.DrawLine(pen, new Point(x, yy), new Point(x + tick, yy));
                if (mm % 10 == 0)
                {
                    var ft = new FormattedText(mm.ToString(), System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight, tf, 8, Brushes.Black, dpi);
                    dc.DrawText(ft, new Point(x - Units.MmToWpfUnits(4), yy - 4));
                }
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return System.Math.Max(min, System.Math.Min(max, value));
        }

        public override bool IsPageCountValid => true;
        public override int PageCount => 1;
        public override Size PageSize { get => _pageSize; set { } }
        public override IDocumentPaginatorSource Source => null!;
    }

    internal class A4SettingsTestDocumentPaginator : DocumentPaginator
    {
        private readonly A4SheetSettings _settings;
        private readonly Size _pageSize;

        public A4SettingsTestDocumentPaginator(A4SheetSettings settings)
        {
            _settings = settings ?? new A4SheetSettings();
            _pageSize = new Size(Units.MmToWpfUnits(A4Layout.A4WidthMm), Units.MmToWpfUnits(A4Layout.A4HeightMm));
        }

        public override DocumentPage GetPage(int pageNumber)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var a4Rect = new Rect(0, 0, Units.MmToWpfUnits(A4Layout.A4WidthMm), Units.MmToWpfUnits(A4Layout.A4HeightMm));
                dc.DrawRectangle(Brushes.White, null, a4Rect);
                dc.DrawRectangle(null, new Pen(Brushes.Black, 1), a4Rect);

                var margin = Math.Max(0, _settings.SheetMarginMm);
                var printableRect = new Rect(
                    Units.MmToWpfUnits(margin),
                    Units.MmToWpfUnits(margin),
                    Units.MmToWpfUnits(A4Layout.A4WidthMm - margin * 2),
                    Units.MmToWpfUnits(A4Layout.A4HeightMm - margin * 2));
                dc.DrawRectangle(null, new Pen(Brushes.DarkGray, 1), printableRect);

                var sx = Clamp(_settings.CalibrationScaleX, 0.95, 1.05);
                var sy = Clamp(_settings.CalibrationScaleY, 0.95, 1.05);
                var tx = Units.MmToWpfUnits(Clamp(_settings.CalibrationOffsetXmm, -5, 5));
                var ty = Units.MmToWpfUnits(Clamp(_settings.CalibrationOffsetYmm, -5, 5));
                dc.PushTransform(new ScaleTransform(sx, sy, a4Rect.Width / 2.0, a4Rect.Height / 2.0));
                dc.PushTransform(new TranslateTransform(tx, ty));

                var gridPen = new Pen(Brushes.LightGray, 0.6);
                for (var x = 0.0; x <= 200; x += 10)
                {
                    var xx = Units.MmToWpfUnits(margin + x);
                    dc.DrawLine(gridPen, new Point(xx, printableRect.Top), new Point(xx, printableRect.Bottom));
                }
                for (var y = 0.0; y <= 287; y += 10)
                {
                    var yy = Units.MmToWpfUnits(margin + y);
                    dc.DrawLine(gridPen, new Point(printableRect.Left, yy), new Point(printableRect.Right, yy));
                }

                DrawRulerX(dc, printableRect.Left, printableRect.Top - Units.MmToWpfUnits(6), 200);
                DrawRulerY(dc, printableRect.Left - Units.MmToWpfUnits(6), printableRect.Top, 287);

                dc.Pop();
                dc.Pop();

                var dpi = 1.0;
                try { if (Application.Current?.MainWindow != null) dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip; } catch { }
                var tf = new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var text = $"A4 Margin={_settings.SheetMarginMm:0.##} mm Gap={_settings.GapMm:0.##} mm | " +
                           $"ScaleX={_settings.CalibrationScaleX * 100.0:0.##}% ScaleY={_settings.CalibrationScaleY * 100.0:0.##}% | " +
                           $"OffsetX={_settings.CalibrationOffsetXmm:0.##} mm OffsetY={_settings.CalibrationOffsetYmm:0.##} mm";
                var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, 12, Brushes.Black, dpi);
                dc.DrawText(ft, new Point(Units.MmToWpfUnits(10), Units.MmToWpfUnits(6)));
            }
            return new DocumentPage(dv, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
        }

        private static void DrawRulerX(DrawingContext dc, double x, double y, int maxMm)
        {
            var pen = new Pen(Brushes.Black, 0.8);
            for (var mm = 0; mm <= maxMm; mm++)
            {
                var xx = x + Units.MmToWpfUnits(mm);
                var tick = mm % 10 == 0 ? Units.MmToWpfUnits(3) : Units.MmToWpfUnits(1.5);
                dc.DrawLine(pen, new Point(xx, y), new Point(xx, y + tick));
            }
        }

        private static void DrawRulerY(DrawingContext dc, double x, double y, int maxMm)
        {
            var pen = new Pen(Brushes.Black, 0.8);
            for (var mm = 0; mm <= maxMm; mm++)
            {
                var yy = y + Units.MmToWpfUnits(mm);
                var tick = mm % 10 == 0 ? Units.MmToWpfUnits(3) : Units.MmToWpfUnits(1.5);
                dc.DrawLine(pen, new Point(x, yy), new Point(x + tick, yy));
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public override bool IsPageCountValid => true;
        public override int PageCount => 1;
        public override Size PageSize { get => _pageSize; set { } }
        public override IDocumentPaginatorSource Source => null!;
    }
}
