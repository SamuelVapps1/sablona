using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Services;

namespace PetShopLabelPrinter
{
    public partial class CsvExportDialog : Window
    {
        private readonly List<Product> _products;
        private readonly Database _db;

        public CsvExportDialog(List<Product> products, Database db)
        {
            InitializeComponent();
            _products = products;
            _db = db;
            var saved = db.GetSetting("CsvExportDelimiter");
            if (saved == ",")
            {
                CmbDelimiter.SelectedIndex = 1;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var delimItem = CmbDelimiter.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var delimStr = (delimItem?.Tag as string) ?? ";";
            var delim = delimStr.Length > 0 ? delimStr[0] : ';';
            _db.SetSetting("CsvExportDelimiter", delimStr);

            var dlg = new SaveFileDialog
            {
                Filter = "CSV súbory|*.csv|Všetky súbory|*.*",
                FileName = $"Products_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Uložiť CSV súbor"
            };
            if (dlg.ShowDialog() != true) return;

            var count = CsvExportService.ExportToFile(_products, dlg.FileName, delim);
            MessageBox.Show($"Export dokončený: {count} produktov.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
