using System.Collections.Generic;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// A4 portrait: 1 column x 7 rows. Label 150mm x 38mm, gap 2mm, margins 10mm.
    /// </summary>
    public static class A4Layout
    {
        public const double A4WidthMm = 210;
        public const double A4HeightMm = 297;
        public const double MarginLeftMm = 10;
        public const double MarginTopMm = 10;
        public const double VerticalGapMm = 2;
        public const int RowsPerPage = 7;
        public const int ColsPerPage = 1;

        public static double LabelWidthMm => LabelRenderer.LabelWidthMm;
        public static double LabelHeightMm => LabelRenderer.LabelHeightMm;

        /// <summary>
        /// Returns list of (pageIndex, row, col, offsetXMm, offsetYMm) for each label.
        /// </summary>
        public static List<LabelPosition> ComputePositions(IReadOnlyList<QueuedLabel> queue)
        {
            var result = new List<LabelPosition>();
            var total = 0;
            foreach (var q in queue)
                total += q.Quantity;

            var idx = 0;
            foreach (var q in queue)
            {
                for (var i = 0; i < q.Quantity; i++)
                {
                    var page = idx / RowsPerPage;
                    var row = idx % RowsPerPage;
                    var col = 0;

                    var x = MarginLeftMm + col * (LabelWidthMm + 0);
                    var y = MarginTopMm + row * (LabelHeightMm + VerticalGapMm);

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

        public static int PagesNeeded(int labelCount)
        {
            if (labelCount <= 0) return 0;
            return (labelCount + RowsPerPage - 1) / RowsPerPage;
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
