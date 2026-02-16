using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Services
{
    /// <summary>
    /// Generates A4 test page: ruler/grid + one label outline for calibration.
    /// </summary>
    public class CalibrationTestService
    {
        private readonly Database _db;

        public CalibrationTestService(Database db)
        {
            _db = db;
        }

        public string GenerateTestPdf()
        {
            var settings = _db.GetTemplateSettings();
            var doc = new PdfDocument();
            doc.Info.Title = "Calibration Test";

            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(A4Layout.A4WidthMm);
            page.Height = XUnit.FromMillimeter(A4Layout.A4HeightMm);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.PageUnit = XUnit.Millimeter;

            var pen = new XPen(XColors.Black, 0.2);

            // Ruler: horizontal lines every 10mm
            for (var y = 0.0; y <= A4Layout.A4HeightMm; y += 10)
            {
                gfx.DrawLine(pen, 0, y, 5, y);
                if (y % 50 == 0)
                    gfx.DrawString($"{y}mm", new XFont("Arial", 6), XBrushes.Black, 0, y - 2);
            }

            // Ruler: vertical lines every 10mm
            for (var x = 0.0; x <= A4Layout.A4WidthMm; x += 10)
            {
                gfx.DrawLine(pen, x, 0, x, 5);
                if (x % 50 == 0)
                    gfx.DrawString($"{x}mm", new XFont("Arial", 6), XBrushes.Black, x - 5, 0);
            }

            // Grid at 10mm
            pen = new XPen(XColors.LightGray, 0.1);
            for (var x = 0.0; x <= A4Layout.A4WidthMm; x += 10)
                gfx.DrawLine(pen, x, 0, x, A4Layout.A4HeightMm);
            for (var y = 0.0; y <= A4Layout.A4HeightMm; y += 10)
                gfx.DrawLine(pen, 0, y, A4Layout.A4WidthMm, y);

            // One label outline at default position (with calibration offset applied)
            var labelX = A4Layout.MarginLeftMm + settings.OffsetXMm;
            var labelY = A4Layout.MarginTopMm + settings.OffsetYMm;
            var labelPen = new XPen(XColors.Black, 0.5);
            gfx.DrawRectangle(labelPen, labelX, labelY,
                LabelRenderer.LabelWidthMm, LabelRenderer.LabelHeightMm);
            gfx.DrawString("150 x 38 mm label outline", new XFont("Arial", 8), XBrushes.Black,
                labelX, labelY + LabelRenderer.LabelHeightMm / 2 - 2, XStringFormats.Center);

            var path = Path.Combine(Path.GetTempPath(), $"CalibrationTest_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            doc.Save(path, false);
            return path;
        }
    }
}
