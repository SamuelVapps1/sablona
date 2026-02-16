using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter.Services
{
    public class PdfExportService
    {
        private readonly Database _db;

        public PdfExportService(Database db)
        {
            _db = db;
        }

        public string ExportToPdf(IReadOnlyList<QueuedLabel> queue, string? suggestedPath = null)
        {
            if (queue == null || queue.Count == 0) return "";

            var settings = _db.GetTemplateSettings();
            var positions = A4Layout.ComputePositions(queue);
            var pagesNeeded = A4Layout.PagesNeeded(positions.Count);

            var doc = new PdfDocument();
            doc.Info.Title = "Pet Shop Labels";

            var renderer = new PdfLabelRenderer(settings);
            var a4W = XUnit.FromMillimeter(A4Layout.A4WidthMm);
            var a4H = XUnit.FromMillimeter(A4Layout.A4HeightMm);

            for (var p = 0; p < pagesNeeded; p++)
            {
                var page = doc.AddPage();
                page.Width = a4W;
                page.Height = a4H;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.PageUnit = XUnit.Millimeter;

                foreach (var pos in positions)
                {
                    if (pos.PageIndex != p) continue;

                    renderer.Draw(gfx, pos.Product, pos.OffsetXMm, pos.OffsetYMm);

                    if (settings.CropMarksEnabled)
                        renderer.DrawCropMarks(gfx, pos.OffsetXMm, pos.OffsetYMm);
                }
            }

            var path = suggestedPath ?? Path.Combine(
                Path.GetTempPath(),
                $"Labels_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            doc.Save(path, false);
            return path;
        }
    }
}
