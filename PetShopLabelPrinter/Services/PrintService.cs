using System.Collections.Generic;
using System.Printing;
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
            return _db.GetSetting("PrinterName");
        }

        public void SetDefaultPrinter(string? name)
        {
            _db.SetSetting("PrinterName", name ?? "");
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

        public bool PrintSilent(IReadOnlyList<QueuedLabel> queue)
        {
            if (queue == null || queue.Count == 0) return false;

            var printerName = GetDefaultPrinter();
            if (string.IsNullOrEmpty(printerName))
            {
                MessageBox.Show("Vyberte tlačiareň v režime Admin.", "Chýba tlačiareň", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var printDialog = new PrintDialog();
                var server = new LocalPrintServer();
                var queues = server.GetPrintQueues();
                PrintQueue? pq = null;
                foreach (var q in queues)
                {
                    if (q.Name == printerName) { pq = q; break; }
                }
                if (pq == null)
                {
                    MessageBox.Show($"Tlačiareň '{printerName}' nebola nájdená.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                printDialog.PrintQueue = pq;
                printDialog.PrintTicket = pq.DefaultPrintTicket;

                var paginator = new LabelDocumentPaginator(_db.GetTemplateSettings(), queue);
                printDialog.PrintDocument(paginator, "Pet Shop Labels");
                return true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Chyba tlače: " + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }

    internal class LabelDocumentPaginator : DocumentPaginator
    {
        private readonly TemplateSettings _settings;
        private readonly IReadOnlyList<QueuedLabel> _queue;
        private readonly List<LabelPosition> _positions;
        private readonly int _pageCount;
        private readonly Size _pageSize;

        public LabelDocumentPaginator(TemplateSettings settings, IReadOnlyList<QueuedLabel> queue)
        {
            _settings = settings;
            _queue = queue;
            _positions = A4Layout.ComputePositions(queue);
            _pageCount = A4Layout.PagesNeeded(_positions.Count);
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

                var renderer = new LabelRenderer(_settings);
                foreach (var pos in _positions)
                {
                    if (pos.PageIndex != pageNumber) continue;
                    renderer.Draw(dc, pos.Product, pos.OffsetXMm, pos.OffsetYMm);
                }
            }

            dv.Transform = new TranslateTransform(0, 0);
            return new DocumentPage(dv, _pageSize, new Rect(_pageSize), new Rect(_pageSize));
        }

        public override bool IsPageCountValid => true;
        public override int PageCount => _pageCount;
        public override Size PageSize { get => _pageSize; set { } }
        public override IDocumentPaginatorSource Source => null!;
    }
}
