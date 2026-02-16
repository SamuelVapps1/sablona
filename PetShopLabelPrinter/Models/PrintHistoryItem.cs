using System;

namespace PetShopLabelPrinter.Models
{
    public class PrintHistoryItem
    {
        public long Id { get; set; }
        public DateTime PrintedAt { get; set; }
        public string ProductNames { get; set; } = "";
        public int TotalLabels { get; set; }
        public string PdfPath { get; set; } = "";
    }
}
