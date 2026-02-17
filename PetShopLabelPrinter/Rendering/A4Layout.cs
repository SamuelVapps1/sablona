using System.Collections.Generic;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// A4 portrait packing computed from template settings.
    /// </summary>
    public static class A4Layout
    {
        public const double A4WidthMm = 210;
        public const double A4HeightMm = 297;

        /// <summary>
        /// Returns list of (pageIndex, row, col, offsetXMm, offsetYMm) for each label.
        /// </summary>
        public static List<LabelPosition> ComputePositions(IReadOnlyList<QueuedLabel> queue, TemplateSettings settings)
        {
            var result = new List<LabelPosition>();
            var layout = BuildLayout(settings);

            var idx = 0;
            foreach (var q in queue)
            {
                for (var i = 0; i < q.Quantity; i++)
                {
                    var page = idx / layout.ItemsPerPage;
                    var indexInPage = idx % layout.ItemsPerPage;
                    var row = indexInPage / layout.Cols;
                    var col = indexInPage % layout.Cols;

                    var x = layout.MarginMm + col * (layout.LabelWidthMm + layout.GapMm);
                    var y = layout.MarginMm + row * (layout.LabelHeightMm + layout.GapMm);

                    result.Add(new LabelPosition
                    {
                        PageIndex = page,
                        Row = row,
                        Col = col,
                        OffsetXMm = x,
                        OffsetYMm = y,
                        Product = q.Product
                    });
                    idx++;
                }
            }
            return result;
        }

        public static int PagesNeeded(int labelCount, TemplateSettings settings)
        {
            if (labelCount <= 0) return 0;
            var layout = BuildLayout(settings);
            return (labelCount + layout.ItemsPerPage - 1) / layout.ItemsPerPage;
        }

        private static LayoutInfo BuildLayout(TemplateSettings settings)
        {
            var s = settings ?? new TemplateSettings();
            var labelW = s.LabelWidthMm > 0 ? s.LabelWidthMm : 150;
            var labelH = s.LabelHeightMm > 0 ? s.LabelHeightMm : 38;
            var margin = s.PageMarginMm >= 0 ? s.PageMarginMm : 10;
            var gap = s.GapMm >= 0 ? s.GapMm : 2;

            var usableW = A4WidthMm - margin * 2;
            var usableH = A4HeightMm - margin * 2;
            if (usableW <= 0 || usableH <= 0)
                return new LayoutInfo(labelW, labelH, gap, margin, 1, 1);

            var cols = (int)System.Math.Floor((usableW + gap) / (labelW + gap));
            var rows = (int)System.Math.Floor((usableH + gap) / (labelH + gap));
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;
            return new LayoutInfo(labelW, labelH, gap, margin, rows, cols);
        }

        private sealed class LayoutInfo
        {
            public LayoutInfo(double labelWidthMm, double labelHeightMm, double gapMm, double marginMm, int rows, int cols)
            {
                LabelWidthMm = labelWidthMm;
                LabelHeightMm = labelHeightMm;
                GapMm = gapMm;
                MarginMm = marginMm;
                Rows = rows;
                Cols = cols;
                ItemsPerPage = Rows * Cols;
            }

            public double LabelWidthMm { get; }
            public double LabelHeightMm { get; }
            public double GapMm { get; }
            public double MarginMm { get; }
            public int Rows { get; }
            public int Cols { get; }
            public int ItemsPerPage { get; }
        }
    }

    public class LabelPosition
    {
        public int PageIndex { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public double OffsetXMm { get; set; }
        public double OffsetYMm { get; set; }
        public Product Product { get; set; } = new Product();
    }
}
