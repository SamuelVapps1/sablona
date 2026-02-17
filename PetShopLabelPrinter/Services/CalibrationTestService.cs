using System.Diagnostics;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Services
{
    /// <summary>
    /// Generates A4 test page: ruler/grid + one label outline for calibration.
    /// </summary>
    public class CalibrationTestService
    {
        private static double MmToPt(double mm) => XUnit.FromMillimeter(mm).Point;

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

            var pen = new XPen(XColors.Black, 0.2);

            // Ruler: horizontal lines every 10mm (coordinates in points)
            for (var y = 0.0; y <= A4Layout.A4HeightMm; y += 10)
            {
                var yPt = MmToPt(y);
                gfx.DrawLine(pen, 0, yPt, MmToPt(5), yPt);
                if (y % 50 == 0)
                    gfx.DrawString($"{y}mm", new XFont("Arial", 6), XBrushes.Black, 0, yPt - 2, XStringFormats.Default);
            }

            for (var x = 0.0; x <= A4Layout.A4WidthMm; x += 10)
            {
                var xPt = MmToPt(x);
                gfx.DrawLine(pen, xPt, 0, xPt, MmToPt(5));
                if (x % 50 == 0)
                    gfx.DrawString($"{x}mm", new XFont("Arial", 6), XBrushes.Black, xPt - MmToPt(5), 0, XStringFormats.Default);
            }

            pen = new XPen(XColors.LightGray, 0.1);
            var a4Wpt = MmToPt(A4Layout.A4WidthMm);
            var a4Hpt = MmToPt(A4Layout.A4HeightMm);
            for (var x = 0.0; x <= A4Layout.A4WidthMm; x += 10)
                gfx.DrawLine(pen, MmToPt(x), 0, MmToPt(x), a4Hpt);
            for (var y = 0.0; y <= A4Layout.A4HeightMm; y += 10)
                gfx.DrawLine(pen, 0, MmToPt(y), a4Wpt, MmToPt(y));

            var labelX = MmToPt(A4Layout.MarginLeftMm + settings.OffsetXMm);
            var labelY = MmToPt(A4Layout.MarginTopMm + settings.OffsetYMm);
            var labelW = MmToPt(LabelRenderer.LabelWidthMm);
            var labelH = MmToPt(LabelRenderer.LabelHeightMm);
            var labelPen = new XPen(XColors.Black, 0.5);
            gfx.DrawRectangle(labelPen, labelX, labelY, labelW, labelH);
            gfx.DrawString("150 x 38 mm label outline", new XFont("Arial", 8), XBrushes.Black,
                labelX, labelY + labelH / 2 - 2, XStringFormats.Center);

            var path = Path.Combine(Path.GetTempPath(), $"CalibrationTest_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            using (var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                doc.Save(stream, false);
            return path;
        }

        public bool PrintTestPdf(string printerName)
        {
            var path = GenerateTestPdf();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Verb = "printto",
                    Arguments = $"\"{printerName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
