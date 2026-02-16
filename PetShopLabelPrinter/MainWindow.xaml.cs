using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;
using PetShopLabelPrinter.Services;

namespace PetShopLabelPrinter
{
    public partial class MainWindow : Window
    {
        private readonly Database _db;
        private readonly PdfExportService _pdfService;
        private readonly PrintService _printService;
        private readonly CalibrationTestService _calibService;

        private readonly ObservableCollection<Product> _products = new ObservableCollection<Product>();
        private readonly ObservableCollection<QueuedLabel> _queue = new ObservableCollection<QueuedLabel>();
        private readonly ObservableCollection<PrintHistoryItem> _history = new ObservableCollection<PrintHistoryItem>();

        private bool _isAdminMode;
        private const string AdminPin = "1234"; // MVP: simple PIN

        public MainWindow()
        {
            InitializeComponent();
            _db = new Database();
            _db.Initialize();

            _pdfService = new PdfExportService(_db);
            _printService = new PrintService(_db);
            _calibService = new CalibrationTestService(_db);

            ProductGrid.ItemsSource = _products;
            QueueGrid.ItemsSource = _queue;
            HistoryList.ItemsSource = _history;

            LoadProducts("");
            LoadHistory();
            LoadPrinterList();
            SwitchToUserMode();
        }

        private void LoadProducts(string search)
        {
            _products.Clear();
            foreach (var p in _db.SearchProducts(search))
                _products.Add(p);
        }

        private void LoadHistory()
        {
            _history.Clear();
            foreach (var h in _db.GetPrintHistory())
                _history.Add(h);
        }

        private void LoadPrinterList()
        {
            CmbPrinter.Items.Clear();
            foreach (var name in _printService.GetInstalledPrinters())
                CmbPrinter.Items.Add(name);
            var def = _printService.GetDefaultPrinter();
            if (!string.IsNullOrEmpty(def))
                CmbPrinter.SelectedItem = def;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadProducts(TxtSearch.Text);
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ProductGrid.Items.Count > 0)
            {
                ProductGrid.SelectedIndex = 0;
                ProductGrid.Focus();
            }
        }

        private void ProductGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnAddToQueue.IsEnabled = ProductGrid.SelectedItem is Product;
        }

        private void QueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnRemoveFromQueue.IsEnabled = QueueGrid.SelectedItem is QueuedLabel;
        }

        private void BtnRemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            if (QueueGrid.SelectedItem is QueuedLabel q)
                _queue.Remove(q);
        }

        private void BtnAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (ProductGrid.SelectedItem is not Product p) return;
            var qty = 1;
            int.TryParse(TxtQuantity.Text, out qty);
            if (qty < 1) qty = 1;

            var existing = _queue.FirstOrDefault(x => x.Product.Id == p.Id);
            if (existing != null)
                existing.Quantity += qty;
            else
                _queue.Add(new QueuedLabel { Product = p, Quantity = qty });
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show("Pridajte produkty do fronty.", "Fronta prázdna", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ok = _printService.PrintSilent(_queue.ToList());
            if (ok)
            {
                var path = _pdfService.ExportToPdf(_queue.ToList());
                _db.AddPrintHistory(new PrintHistoryItem
                {
                    PrintedAt = DateTime.Now,
                    ProductNames = string.Join(", ", _queue.Select(x => x.Product.ProductName).Distinct()),
                    TotalLabels = _queue.Sum(x => x.Quantity),
                    PdfPath = path ?? ""
                });
                LoadHistory();
            }
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_queue.Count == 0)
            {
                MessageBox.Show("Pridajte produkty do fronty.", "Fronta prázdna", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                FileName = $"Labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                var path = _pdfService.ExportToPdf(_queue.ToList(), dlg.FileName);
                _db.AddPrintHistory(new PrintHistoryItem
                {
                    PrintedAt = DateTime.Now,
                    ProductNames = string.Join(", ", _queue.Select(x => x.Product.ProductName).Distinct()),
                    TotalLabels = _queue.Sum(x => x.Quantity),
                    PdfPath = path
                });
                LoadHistory();
                MessageBox.Show($"PDF uložené: {path}", "Hotovo", MessageBoxButton.OK);
            }
        }

        private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var enabled = HistoryList.SelectedItem is PrintHistoryItem;
            BtnReprint.IsEnabled = enabled;
            BtnOpenPdf.IsEnabled = enabled;
        }

        private void BtnReprint_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryList.SelectedItem is not PrintHistoryItem h) return;
            // Reprint: we don't store the actual queue, so we'd need to look up products by name
            // For MVP: just print/export again with the same product names - we'd need to search products
            var names = h.ProductNames.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _queue.Clear();
            foreach (var name in names)
            {
                var products = _db.SearchProducts(name);
                if (products.Count > 0)
                    _queue.Add(new QueuedLabel { Product = products[0], Quantity = h.TotalLabels / names.Length });
            }
            if (_queue.Count > 0)
            {
                _printService.PrintSilent(_queue.ToList());
                var path = _pdfService.ExportToPdf(_queue.ToList());
                _db.AddPrintHistory(new PrintHistoryItem
                {
                    PrintedAt = DateTime.Now,
                    ProductNames = h.ProductNames,
                    TotalLabels = h.TotalLabels,
                    PdfPath = path ?? ""
                });
                LoadHistory();
            }
        }

        private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryList.SelectedItem is not PrintHistoryItem h) return;
            if (string.IsNullOrEmpty(h.PdfPath) || !System.IO.File.Exists(h.PdfPath))
            {
                MessageBox.Show("PDF súbor nie je k dispozícii.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { Process.Start(h.PdfPath); } catch { }
        }

        private void BtnUserMode_Click(object sender, RoutedEventArgs e)
        {
            SwitchToUserMode();
        }

        private void BtnAdminMode_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AdminPinDialog { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Pin != AdminPin)
            {
                MessageBox.Show("Nesprávny PIN.", "Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SwitchToAdminMode();
        }

        private void SwitchToUserMode()
        {
            _isAdminMode = false;
            UserPanel.Visibility = Visibility.Visible;
            AdminPanel.Visibility = Visibility.Collapsed;
            TxtMode.Text = "Režim: Používateľ";
        }

        private void SwitchToAdminMode()
        {
            _isAdminMode = true;
            UserPanel.Visibility = Visibility.Collapsed;
            AdminPanel.Visibility = Visibility.Visible;
            TxtMode.Text = "Režim: Admin";
            LoadAdminSettings();
            LoadProducts("");
            AdminProductGrid.ItemsSource = _products;
            RefreshPreview();
        }

        private TemplateSettings GetSettingsFromForm()
        {
            var s = _db.GetTemplateSettings();
            if (_isAdminMode && TxtProductNameFont != null)
            {
                s.ProductNameFontFamily = TxtProductNameFont?.Text ?? s.ProductNameFontFamily;
                double.TryParse(TxtProductNameSize?.Text, out s.ProductNameFontSizePt);
                double.TryParse(TxtProductNameMinSize?.Text, out s.ProductNameMinFontSizePt);
                s.ProductNameBold = ChkProductNameBold?.IsChecked == true;
                s.VariantTextFontFamily = TxtVariantFont?.Text ?? s.VariantTextFontFamily;
                double.TryParse(TxtVariantSize?.Text, out s.VariantTextFontSizePt);
                s.VariantTextBold = ChkVariantBold?.IsChecked == true;
                s.PriceBigFontFamily = TxtPriceBigFont?.Text ?? s.PriceBigFontFamily;
                double.TryParse(TxtPriceBigSize?.Text, out s.PriceBigFontSizePt);
                s.PriceBigBold = ChkPriceBigBold?.IsChecked == true;
                double.TryParse(TxtLeftColMm?.Text, out s.LeftColWidthMm);
                double.TryParse(TxtRightColMm?.Text, out s.RightColWidthMm);
                double.TryParse(TxtTopMm?.Text, out s.RightTopHeightMm);
                double.TryParse(TxtMidMm?.Text, out s.RightMiddleHeightMm);
                double.TryParse(TxtBotMm?.Text, out s.RightBottomHeightMm);
            }
            return s;
        }

        private void LoadAdminSettings()
        {
            var s = _db.GetTemplateSettings();
            TxtProductNameFont.Text = s.ProductNameFontFamily;
            TxtProductNameSize.Text = s.ProductNameFontSizePt.ToString();
            TxtProductNameMinSize.Text = s.ProductNameMinFontSizePt.ToString();
            ChkProductNameBold.IsChecked = s.ProductNameBold;
            TxtVariantFont.Text = s.VariantTextFontFamily;
            TxtVariantSize.Text = s.VariantTextFontSizePt.ToString();
            ChkVariantBold.IsChecked = s.VariantTextBold;
            TxtPriceBigFont.Text = s.PriceBigFontFamily;
            TxtPriceBigSize.Text = s.PriceBigFontSizePt.ToString();
            ChkPriceBigBold.IsChecked = s.PriceBigBold;
            TxtLeftColMm.Text = s.LeftColWidthMm.ToString();
            TxtRightColMm.Text = s.RightColWidthMm.ToString();
            TxtTopMm.Text = s.RightTopHeightMm.ToString();
            TxtMidMm.Text = s.RightMiddleHeightMm.ToString();
            TxtBotMm.Text = s.RightBottomHeightMm.ToString();
            ChkCropMarks.IsChecked = s.CropMarksEnabled;
            TxtOffsetX.Text = s.OffsetXMm.ToString();
            TxtOffsetY.Text = s.OffsetYMm.ToString();
        }

        private void AdminSetting_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isAdminMode) RefreshPreview();
        }

        private void AdminSetting_Checked(object sender, RoutedEventArgs e)
        {
            if (_isAdminMode) RefreshPreview();
        }

        private void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            RefreshPreview();
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var s = _db.GetTemplateSettings();
            s.ProductNameFontFamily = TxtProductNameFont.Text;
            double.TryParse(TxtProductNameSize.Text, out s.ProductNameFontSizePt);
            double.TryParse(TxtProductNameMinSize.Text, out s.ProductNameMinFontSizePt);
            s.ProductNameBold = ChkProductNameBold.IsChecked == true;
            s.VariantTextFontFamily = TxtVariantFont.Text;
            double.TryParse(TxtVariantSize.Text, out s.VariantTextFontSizePt);
            s.VariantTextBold = ChkVariantBold.IsChecked == true;
            s.PriceBigFontFamily = TxtPriceBigFont.Text;
            double.TryParse(TxtPriceBigSize.Text, out s.PriceBigFontSizePt);
            s.PriceBigBold = ChkPriceBigBold.IsChecked == true;
            double.TryParse(TxtLeftColMm.Text, out s.LeftColWidthMm);
            double.TryParse(TxtRightColMm.Text, out s.RightColWidthMm);
            double.TryParse(TxtTopMm.Text, out s.RightTopHeightMm);
            double.TryParse(TxtMidMm.Text, out s.RightMiddleHeightMm);
            double.TryParse(TxtBotMm.Text, out s.RightBottomHeightMm);
            s.CropMarksEnabled = ChkCropMarks.IsChecked == true;
            double.TryParse(TxtOffsetX.Text, out s.OffsetXMm);
            double.TryParse(TxtOffsetY.Text, out s.OffsetYMm);

            _db.SaveTemplateSettings(s);
            RefreshPreview();
            MessageBox.Show("Nastavenia uložené.", "Admin", MessageBoxButton.OK);
        }

        private void RefreshPreview()
        {
            var s = GetSettingsFromForm();
            var sample = new Product
            {
                ProductName = "Vzorový produkt",
                VariantText = "Varianta",
                SmallPackLabel = "Balenie 1 kg",
                SmallPackPrice = 5.99m,
                LargePackLabel = "Balenie 17 kg",
                LargePackWeightKg = 17,
                LargePackPrice = 42.90m
            };

            var w = Units.MmToWpfUnits(LabelRenderer.LabelWidthMm);
            var h = Units.MmToWpfUnits(LabelRenderer.LabelHeightMm);
            PreviewCanvas.Children.Clear();
            PreviewCanvas.Width = w;
            PreviewCanvas.Height = h;

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var renderer = new LabelRenderer(s);
                renderer.Draw(dc, sample, 0, 0);
            }

            var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var img = new System.Windows.Controls.Image { Source = bmp };
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            PreviewCanvas.Children.Add(img);
        }

        private void BtnSavePrinter_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPrinter.SelectedItem is string name)
            {
                _printService.SetDefaultPrinter(name);
                MessageBox.Show($"Tlačiareň '{name}' uložená.", "Admin", MessageBoxButton.OK);
            }
        }

        private void BtnTestPage_Click(object sender, RoutedEventArgs e)
        {
            var path = _calibService.GenerateTestPdf();
            try { Process.Start(path); } catch { }
            MessageBox.Show($"Test PDF: {path}", "Admin", MessageBoxButton.OK);
        }

        private void AdminProductGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnEditProduct.IsEnabled = AdminProductGrid.SelectedItem is Product;
            BtnDeleteProduct.IsEnabled = AdminProductGrid.SelectedItem is Product;
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ProductEditDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _db.InsertProduct(dlg.Product);
                LoadProducts("");
            }
        }

        private void BtnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (AdminProductGrid.SelectedItem is not Product p) return;
            var dlg = new ProductEditDialog(p) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _db.UpdateProduct(dlg.Product);
                LoadProducts("");
            }
        }

        private void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (AdminProductGrid.SelectedItem is not Product p) return;
            if (MessageBox.Show($"Zmazať produkt '{p.ProductName}'?", "Potvrdiť", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _db.DeleteProduct(p.Id);
                LoadProducts("");
            }
        }
    }
}
