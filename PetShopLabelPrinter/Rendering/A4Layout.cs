using System;
using System.Collections.Generic;
using System.Linq;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// A4 packing for mixed-size templates (left-to-right, top-to-bottom, no rotation).
    /// </summary>
    public static class A4Layout
    {
        public const double A4WidthMm = 210;
        public const double A4HeightMm = 297;
        public const double EpsilonMm = 0.5;

        public static A4PackResult Pack(IReadOnlyList<LabelPrintJob> jobs, A4SheetSettings? sheetSettings = null)
        {
            var s = NormalizeSheetSettings(sheetSettings ?? new A4SheetSettings());
            var instances = ExpandInstances(jobs);
            var pages = new List<A4PackedPage>();
            if (instances.Count == 0) return new A4PackResult { Pages = pages };

            var page = new A4PackedPage { PageIndex = 0 };
            pages.Add(page);

            var x = s.SheetMarginMm;
            var y = s.SheetMarginMm;
            var rowHeight = 0.0;
            var printableRight = s.SheetWidthMm - s.SheetMarginMm;
            var printableBottom = s.SheetHeightMm - s.SheetMarginMm;

            foreach (var item in instances)
            {
                var w = item.Template.WidthMm;
                var h = item.Template.HeightMm;
                if (w <= 0 || h <= 0) continue;

                // Row break.
                if (x + w > printableRight - EpsilonMm && x > s.SheetMarginMm)
                {
                    x = s.SheetMarginMm;
                    y += rowHeight + s.GapMm;
                    rowHeight = 0;
                }

                // Page break.
                if (y + h > printableBottom - EpsilonMm && y > s.SheetMarginMm)
                {
                    page = new A4PackedPage { PageIndex = pages.Count };
                    pages.Add(page);
                    x = s.SheetMarginMm;
                    y = s.SheetMarginMm;
                    rowHeight = 0;
                }

                // If still not fitting on fresh row/page, skip deterministically.
                if (x + w > printableRight - EpsilonMm || y + h > printableBottom - EpsilonMm)
                    continue;

                page.Items.Add(new A4PlacedItem
                {
                    Xmm = x,
                    Ymm = y,
                    Product = item.Product,
                    Template = item.Template
                });

                x += w + s.GapMm;
                rowHeight = Math.Max(rowHeight, h);
            }

            return new A4PackResult { Pages = pages };
        }

        public static List<string> ValidateTemplateFit(IReadOnlyList<LabelPrintJob> jobs, A4SheetSettings? sheetSettings = null)
        {
            var s = NormalizeSheetSettings(sheetSettings ?? new A4SheetSettings());
            var printableWidth = s.SheetWidthMm - 2 * s.SheetMarginMm;
            var printableHeight = s.SheetHeightMm - 2 * s.SheetMarginMm;
            var errors = new List<string>();
            var seen = new HashSet<int>();

            foreach (var j in jobs ?? Array.Empty<LabelPrintJob>())
            {
                if (j?.Template == null) continue;
                if (!seen.Add(j.Template.Id)) continue;
                var t = j.Template;
                if (t.WidthMm > printableWidth - EpsilonMm || t.HeightMm > printableHeight - EpsilonMm)
                {
                    errors.Add($"{t.Name} ({t.WidthMm:0.##} x {t.HeightMm:0.##} mm) > printable area ({printableWidth:0.##} x {printableHeight:0.##} mm)");
                }
            }

            return errors;
        }

        private static List<LabelPrintInstance> ExpandInstances(IReadOnlyList<LabelPrintJob> jobs)
        {
            var result = new List<LabelPrintInstance>();
            foreach (var job in jobs ?? Array.Empty<LabelPrintJob>())
            {
                if (job?.Product == null || job.Template == null) continue;
                var copies = job.Copies < 1 ? 1 : job.Copies;
                for (var i = 0; i < copies; i++)
                {
                    result.Add(new LabelPrintInstance
                    {
                        Product = job.Product,
                        Template = job.Template
                    });
                }
            }
            return result;
        }

        private static A4SheetSettings NormalizeSheetSettings(A4SheetSettings s)
        {
            var normalized = s ?? new A4SheetSettings();
            if (normalized.SheetWidthMm <= 0) normalized.SheetWidthMm = A4WidthMm;
            if (normalized.SheetHeightMm <= 0) normalized.SheetHeightMm = A4HeightMm;
            normalized.SheetMarginMm = Clamp(normalized.SheetMarginMm, 0, 40);
            normalized.GapMm = Clamp(normalized.GapMm, 0, 20);
            normalized.CalibrationScaleX = Clamp(normalized.CalibrationScaleX, 0.95, 1.05);
            normalized.CalibrationScaleY = Clamp(normalized.CalibrationScaleY, 0.95, 1.05);
            normalized.CalibrationOffsetXmm = Clamp(normalized.CalibrationOffsetXmm, -5, 5);
            normalized.CalibrationOffsetYmm = Clamp(normalized.CalibrationOffsetYmm, -5, 5);
            normalized.Orientation = "Portrait";
            return normalized;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public class LabelPrintJob
    {
        public long ProductId { get; set; }
        public int TemplateId { get; set; }
        public Product Product { get; set; } = new Product();
        public LabelTemplate Template { get; set; } = new LabelTemplate();
        public int Copies { get; set; } = 1;
    }

    internal class LabelPrintInstance
    {
        public Product Product { get; set; } = new Product();
        public LabelTemplate Template { get; set; } = new LabelTemplate();
    }

    public class A4PlacedItem
    {
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public Product Product { get; set; } = new Product();
        public LabelTemplate Template { get; set; } = new LabelTemplate();
    }

    public class A4PackedPage
    {
        public int PageIndex { get; set; }
        public List<A4PlacedItem> Items { get; set; } = new List<A4PlacedItem>();
    }

    public class A4PackResult
    {
        public List<A4PackedPage> Pages { get; set; } = new List<A4PackedPage>();
        public int PageCount => Pages.Count;
    }
}
