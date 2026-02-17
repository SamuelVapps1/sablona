using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly ObservableCollection<PrintHistoryItem> _history = new ObservableCollection<PrintHistoryItem>();
        private readonly ICollectionView _productView;

        private bool _isAdminMode;
        private const string AdminPin = "1234";

        public MainWindow()
        {
            InitializeComponent();
            _db = new Database();
            _db.Initialize();
            _pdfService = new PdfExportService(_db);
            _printService = new PrintService(_db);
            _calibService = new CalibrationTestService(_db);

            ProductGrid.ItemsSource = _products;
            HistoryList.ItemsSource = _history;
            _productView = CollectionViewSource.GetDefaultView(_products);

            LoadProducts();
            LoadHistory();
            LoadPrinterList();
            LoadFontLists();
            LoadAdminSettings();
            SwitchToUserMode();
            UpdateHistoryActions();
        }

        private void LoadProducts()
        {
            _products.Clear();
            foreach (var p in _db.SearchProducts(string.Empty))
            {
                if (p.Quantity < 1) p.Quantity = 1;
                p.IsActiveForPrint = false;
                _products.Add(p);
            }
            ApplySearchFilter();
            RefreshPreview();
        }

        private void ApplySearchFilter()
        {
            var term = (TxtSearch.Text ?? "").Trim();
            _productView.Filter = o =>
            {
                var p = o as Product;
                if (p == null) return false;
                if (term.Length == 0) return true;
                return (p.ProductName ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                       || (p.VariantText ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            _productView.Refresh();
        }

        private void LoadHistory()
        {
            _history.Clear();
            foreach (var h in _db.GetPrintHistory())
                _history.Add(h);
            UpdateHistoryActions();
        }

        private void LoadPrinterList()
        {
            CmbPrinter.Items.Clear();
            foreach (var name in _printService.GetInstalledPrinters())
                CmbPrinter.Items.Add(name);
            var def = _printService.GetDefaultPrinter();
            if (!string.IsNullOrWhiteSpace(def))
                CmbPrinter.SelectedItem = def;
        }

        private void LoadFontLists()
        {
            var fonts = Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
            CmbProductNameFont.ItemsSource = fonts;
            CmbVariantFont.ItemsSource = fonts;
            CmbPriceBigFont.ItemsSource = fonts;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ProductGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var p = e.Row.Item as Product;
            if (p == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NormalizeProduct(p);
                if (string.IsNullOrWhiteSpace(p.ProductName))
                    return;
                if (p.Id == 0) _db.InsertProduct(p);
                else _db.UpdateProduct(p);
                RefreshPreview();
            }));
        }

        private void ProductGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPreview();
        }

        private void NormalizeProduct(Product p)
        {
            if (p.Quantity < 1) p.Quantity = 1;
            p.ProductName = (p.ProductName ?? "").Trim();
            p.VariantText = (p.VariantText ?? "").Trim();
            p.SmallPackLabel = (p.SmallPackLabel ?? "").Trim();
            p.LargePackLabel = (p.LargePackLabel ?? "").Trim();
            p.UnitPriceText = (p.UnitPriceText ?? "").Trim();
        }

        private void SaveAllProducts()
        {
            ProductGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ProductGrid.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var p in _products)
            {
                NormalizeProduct(p);
                if (string.IsNullOrWhiteSpace(p.ProductName))
                    continue;
                if (p.Id == 0) _db.InsertProduct(p);
                else _db.UpdateProduct(p);
            }
        }

        private List<QueuedLabel> BuildQueueFromCurrentSelection()
        {
            var fromChecks = _products.Where(p => p.IsActiveForPrint).ToList();
            var sourceRows = fromChecks.Count > 0
                ? fromChecks
                : ProductGrid.SelectedItems.Cast<object>().OfType<Product>().ToList();

            var queue = new List<QueuedLabel>();
            foreach (var p in sourceRows)
            {
                var qty = p.Quantity < 1 ? 1 : p.Quantity;
                queue.Add(new QueuedLabel { Product = p, Quantity = qty });
            }
            return queue;
        }

        private string BuildHistoryNames(IReadOnlyCollection<QueuedLabel> queue)
        {
            var names = queue.Select(q => q.Product.ProductName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            if (names.Count <= 4) return string.Join(", ", names);
            return string.Join(", ", names.Take(4)) + $" (+{names.Count - 4})";
        }

        private void BtnPrintSelected_Click(object sender, RoutedEventArgs e)
        {
            SaveAllProducts();
            var queue = BuildQueueFromCurrentSelection();
            if (queue.Count == 0)
            {
                MessageBox.Show("Vyberte riadky (alebo označte Tlačiť?) a nastavte Počet.", "Bez výberu", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_printService.PrintSilent(queue))
            {
                _db.AddPrintHistory(new PrintHistoryItem
                {
                    PrintedAt = DateTime.Now,
                    JobType = "PRINT",
                    ProductNames = BuildHistoryNames(queue),
                    TotalLabels = queue.Sum(q => q.Quantity),
                    PdfPath = ""
                });
                LoadHistory();
            }
        }

        private void BtnExportSelected_Click(object sender, RoutedEventArgs e)
        {
            SaveAllProducts();
            var queue = BuildQueueFromCurrentSelection();
            if (queue.Count == 0)
            {
                MessageBox.Show("Vyberte riadky (alebo označte Tlačiť?) a nastavte Počet.", "Bez výberu", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                FileName = $"Labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };
            if (dlg.ShowDialog() != true) return;

            var path = _pdfService.ExportToPdf(queue, dlg.FileName);
            _db.AddPrintHistory(new PrintHistoryItem
            {
                PrintedAt = DateTime.Now,
                JobType = "EXPORT",
                ProductNames = BuildHistoryNames(queue),
                TotalLabels = queue.Sum(q => q.Quantity),
                PdfPath = path
            });
            LoadHistory();
            MessageBox.Show($"PDF uložené: {path}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _products)
            {
                p.IsActiveForPrint = false;
                if (p.Quantity < 1) p.Quantity = 1;
            }
            ProductGrid.Items.Refresh();
            ProductGrid.UnselectAll();
        }

        private void BtnDeleteSelectedProducts_Click(object sender, RoutedEventArgs e)
        {
            var selected = ProductGrid.SelectedItems.Cast<object>().OfType<Product>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Najprv vyberte riadky na zmazanie.", "Zmazať riadky", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"Zmazať {selected.Count} vybraných riadkov?", "Potvrdenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            foreach (var p in selected)
            {
                if (p.Id > 0) _db.DeleteProduct(p.Id);
                _products.Remove(p);
            }
        }

        private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHistoryActions();
        }

        private void UpdateHistoryActions()
        {
            if (HistoryList.SelectedItem is not PrintHistoryItem h)
            {
                BtnOpenPdf.IsEnabled = false;
                return;
            }
            BtnOpenPdf.IsEnabled = !string.IsNullOrWhiteSpace(h.PdfPath);
        }

        private void BtnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryList.SelectedItem is not PrintHistoryItem h || string.IsNullOrWhiteSpace(h.PdfPath))
                return;
            if (!System.IO.File.Exists(h.PdfPath))
            {
                MessageBox.Show("Súbor PDF neexistuje.", "História", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(h.PdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nepodarilo sa otvoriť PDF: " + ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            RefreshPreview();
        }

        private TemplateSettings GetSettingsFromForm()
        {
            var s = _db.GetTemplateSettings();
            if (CmbProductNameFont.SelectedItem is string pnFont && pnFont.Length > 0) s.ProductNameFontFamily = pnFont;
            if (double.TryParse(TxtProductNameSize.Text, out var parsed)) s.ProductNameFontSizePt = parsed;
            if (double.TryParse(TxtProductNameMinSize.Text, out parsed)) s.ProductNameMinFontSizePt = parsed;
            s.ProductNameBold = ChkProductNameBold.IsChecked == true;

            if (CmbVariantFont.SelectedItem is string vFont && vFont.Length > 0) s.VariantTextFontFamily = vFont;
            if (double.TryParse(TxtVariantSize.Text, out parsed)) s.VariantTextFontSizePt = parsed;
            s.VariantTextBold = ChkVariantBold.IsChecked == true;

            if (CmbPriceBigFont.SelectedItem is string pbFont && pbFont.Length > 0) s.PriceBigFontFamily = pbFont;
            if (double.TryParse(TxtPriceBigSize.Text, out parsed)) s.PriceBigFontSizePt = parsed;
            s.PriceBigBold = ChkPriceBigBold.IsChecked == true;

            if (double.TryParse(TxtLabelWidthMm.Text, out parsed)) s.LabelWidthMm = parsed;
            if (double.TryParse(TxtLabelHeightMm.Text, out parsed)) s.LabelHeightMm = parsed;
            if (double.TryParse(TxtRightColumnWidthMm.Text, out parsed)) s.RightColumnWidthMm = parsed;
            if (double.TryParse(TxtBorderThicknessMm.Text, out parsed)) s.BorderThicknessMm = parsed;
            if (double.TryParse(TxtGapMm.Text, out parsed)) s.GapMm = parsed;
            if (double.TryParse(TxtPageMarginMm.Text, out parsed)) s.PageMarginMm = parsed;
            if (double.TryParse(TxtTopMm.Text, out parsed)) s.RightTopHeightMm = parsed;
            if (double.TryParse(TxtMidMm.Text, out parsed)) s.RightMiddleHeightMm = parsed;
            if (double.TryParse(TxtBotMm.Text, out parsed)) s.RightBottomHeightMm = parsed;
            s.ShowSeparatorBetweenPacks = ChkShowSeparatorBetweenPacks.IsChecked == true;
            s.ShowBottomSeparator = ChkShowBottomSeparator.IsChecked == true;
            s.CropMarksEnabled = ChkCropMarks.IsChecked == true;
            if (double.TryParse(TxtOffsetX.Text, out parsed)) s.OffsetXMm = parsed;
            if (double.TryParse(TxtOffsetY.Text, out parsed)) s.OffsetYMm = parsed;
            return s;
        }

        private void LoadAdminSettings()
        {
            var s = _db.GetTemplateSettings();
            CmbProductNameFont.SelectedItem = s.ProductNameFontFamily;
            TxtProductNameSize.Text = s.ProductNameFontSizePt.ToString("0.##");
            TxtProductNameMinSize.Text = s.ProductNameMinFontSizePt.ToString("0.##");
            ChkProductNameBold.IsChecked = s.ProductNameBold;

            CmbVariantFont.SelectedItem = s.VariantTextFontFamily;
            TxtVariantSize.Text = s.VariantTextFontSizePt.ToString("0.##");
            ChkVariantBold.IsChecked = s.VariantTextBold;

            CmbPriceBigFont.SelectedItem = s.PriceBigFontFamily;
            TxtPriceBigSize.Text = s.PriceBigFontSizePt.ToString("0.##");
            ChkPriceBigBold.IsChecked = s.PriceBigBold;

            TxtLabelWidthMm.Text = s.LabelWidthMm.ToString("0.##");
            TxtLabelHeightMm.Text = s.LabelHeightMm.ToString("0.##");
            TxtRightColumnWidthMm.Text = s.RightColumnWidthMm.ToString("0.##");
            TxtBorderThicknessMm.Text = s.BorderThicknessMm.ToString("0.##");
            TxtGapMm.Text = s.GapMm.ToString("0.##");
            TxtPageMarginMm.Text = s.PageMarginMm.ToString("0.##");
            TxtTopMm.Text = s.RightTopHeightMm.ToString("0.##");
            TxtMidMm.Text = s.RightMiddleHeightMm.ToString("0.##");
            TxtBotMm.Text = s.RightBottomHeightMm.ToString("0.##");
            ChkShowSeparatorBetweenPacks.IsChecked = s.ShowSeparatorBetweenPacks;
            ChkShowBottomSeparator.IsChecked = s.ShowBottomSeparator;
            ChkCropMarks.IsChecked = s.CropMarksEnabled;
            TxtOffsetX.Text = s.OffsetXMm.ToString("0.##");
            TxtOffsetY.Text = s.OffsetYMm.ToString("0.##");
        }

        private void AdminSetting_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isAdminMode) RefreshPreview();
        }

        private void AdminSetting_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isAdminMode) RefreshPreview();
        }

        private void AdminSetting_Checked(object sender, RoutedEventArgs e)
        {
            if (_isAdminMode) RefreshPreview();
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var s = GetSettingsFromForm();
            _db.SaveTemplateSettings(s);
            RefreshPreview();
            MessageBox.Show("Nastavenia uložené.", "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Product GetPreviewProduct()
        {
            if (ProductGrid.SelectedItem is Product selected && !string.IsNullOrWhiteSpace(selected.ProductName))
                return selected;
            var first = _products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductName));
            return first ?? new Product
            {
                ProductName = "Produkt",
                VariantText = "Varianta",
                SmallPackLabel = "Balenie 1 kg",
                SmallPackPrice = 0m,
                LargePackLabel = "Balenie 17 kg",
                LargePackPrice = 0m,
                UnitPriceText = "1 kg = 0,00 €"
            };
        }

        private void RefreshPreview()
        {
            var s = GetSettingsFromForm();
            var previewProduct = GetPreviewProduct();

            var labelWidthMm = s.LabelWidthMm > 0 ? s.LabelWidthMm : LabelRenderer.LabelWidthMm;
            var labelHeightMm = s.LabelHeightMm > 0 ? s.LabelHeightMm : LabelRenderer.LabelHeightMm;
            var w = Units.MmToWpfUnits(labelWidthMm);
            var h = Units.MmToWpfUnits(labelHeightMm);
            PreviewCanvas.Children.Clear();
            PreviewCanvas.Width = w;
            PreviewCanvas.Height = h;

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var renderer = new LabelRenderer(s);
                renderer.Draw(dc, previewProduct, 0, 0);
            }

            var bmp = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var img = new Image { Source = bmp };
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            PreviewCanvas.Children.Add(img);
        }

        private void BtnSavePrinter_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPrinter.SelectedItem is not string name || string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Vyberte tlačiareň.", "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _printService.SetDefaultPrinter(name);
            MessageBox.Show($"Predvolená tlačiareň: {name}", "Admin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnGenerateTestPdf_Click(object sender, RoutedEventArgs e)
        {
            var path = _calibService.GenerateTestPdf();
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch { }
        }

        private void BtnPrintTest_Click(object sender, RoutedEventArgs e)
        {
            var printer = _printService.GetDefaultPrinter();
            if (string.IsNullOrWhiteSpace(printer))
            {
                MessageBox.Show("Najprv uložte predvolenú tlačiareň v Admin režime.", "Tlač testu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!_calibService.PrintTestPdf(printer!))
                MessageBox.Show("Nepodarilo sa odoslať test na tlačiareň.", "Tlač testu", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Naozaj vymazať celú históriu úloh?", "História", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _db.ClearPrintHistory();
            LoadHistory();
        }
    }
}
