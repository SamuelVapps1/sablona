using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PetShopLabelPrinter.Data;
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

        public string ExportToPdf(IReadOnlyList<QueuedLabel> queue, string? suggestedPath = null, TemplateSettings? effectiveSettings = null)
        {
            if (queue == null || queue.Count == 0) return "";

            var settings = effectiveSettings ?? _db.GetTemplateSettings();
            var a4 = _db.GetA4SheetSettings();
            var jobs = BuildJobs(queue, settings);
            var fitErrors = A4Layout.ValidateTemplateFit(jobs, a4);
            if (fitErrors.Count > 0)
                throw new System.InvalidOperationException("Niektoré šablóny sa nezmestia do printable area:\n- " + string.Join("\n- ", fitErrors));
            var packed = A4Layout.Pack(jobs, a4);

            var doc = new PdfDocument();
            doc.Info.Title = "Pet Shop Labels";

            var renderer = new A4SheetRenderer();
            var a4W = XUnit.FromMillimeter(A4Layout.A4WidthMm);
            var a4H = XUnit.FromMillimeter(A4Layout.A4HeightMm);

            for (var p = 0; p < packed.PageCount; p++)
            {
                var page = doc.AddPage();
                page.Width = a4W;
                page.Height = a4H;

                using var gfx = XGraphics.FromPdfPage(page);
                renderer.DrawPdfPage(gfx, packed.Pages[p], settings, a4);
            }

            var path = suggestedPath ?? Path.Combine(
                Path.GetTempPath(),
                $"Labels_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            using (var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                doc.Save(stream, false);
            return path;
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
    }
}
