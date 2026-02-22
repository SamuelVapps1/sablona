using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Rendering;

namespace PetShopLabelPrinter
{
    public partial class ProductEditDialog : Window
    {
        private readonly Database _db;
        public Product Product { get; private set; } = new Product();

        public ProductEditDialog(Database db, Product? product = null)
        {
            InitializeComponent();
            _db = db;
            var templates = _db.GetLabelTemplates();
            CmbTemplate.ItemsSource = templates;
            if (product != null)
            {
                Product = product;
                TxtProductName.Text = product.ProductName;
                TxtVariantText.Text = product.VariantText;
                TxtSmallPackLabel.Text = product.SmallPackLabel;
                TxtSmallPackWeight.Text = product.SmallPackWeightKg?.ToString(CultureInfo.InvariantCulture) ?? "";
                TxtSmallPackPrice.Text = product.SmallPackPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
                TxtLargePackLabel.Text = product.LargePackLabel;
                TxtLargePackWeight.Text = product.LargePackWeightKg?.ToString(CultureInfo.InvariantCulture) ?? "";
                TxtLargePackPrice.Text = product.LargePackPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
                TxtUnitPriceOverride.Text = product.UnitPriceOverride?.ToString("N2", CultureInfo.GetCultureInfo("sk-SK")) ?? "";
                TxtNotes.Text = product.Notes ?? "";
                if (product.TemplateId.HasValue)
                    CmbTemplate.SelectedItem = templates.FirstOrDefault(t => t.Id == product.TemplateId.Value);
                ChkShowEan.IsChecked = product.ShowEan;
                TxtEan.Text = product.Ean ?? "";
                ChkShowSku.IsChecked = product.ShowSku;
                TxtSku.Text = product.Sku ?? "";
                ChkShowExpiry.IsChecked = product.ShowExpiry;
                TxtExpiryDate.Text = product.ExpiryDate ?? "";
                ChkBarcodeEnabled.IsChecked = product.BarcodeEnabled;
                TxtBarcodeValue.Text = product.BarcodeValue ?? "";
                var fmt = product.BarcodeFormat ?? "EAN13";
                CmbBarcodeFormat.SelectedIndex = fmt == "CODE128" ? 1 : 0;
                ChkBarcodeShowText.IsChecked = product.BarcodeShowText;
                if (product.PackWeightValue.HasValue)
                    TxtSmallPackWeight.Text = product.PackWeightValue.Value.ToString(CultureInfo.InvariantCulture);
                var unit = string.Equals(product.PackWeightUnit, "g", System.StringComparison.OrdinalIgnoreCase) ? "g" : "kg";
                CmbPackUnit.SelectedIndex = unit == "g" ? 1 : 0;
            }
            else
            {
                CmbPackUnit.SelectedIndex = 0;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Product.ProductName = TxtProductName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(Product.ProductName))
            {
                MessageBox.Show("Zadajte názov produktu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Product.VariantText = TxtVariantText.Text?.Trim() ?? "";
            Product.SmallPackLabel = TxtSmallPackLabel.Text?.Trim() ?? "";
            var unit = (CmbPackUnit.SelectedItem as ComboBoxItem)?.Content as string ?? "kg";
            Product.PackWeightUnit = unit == "g" ? "g" : "kg";
            Product.PackWeightValue = decimal.TryParse(TxtSmallPackWeight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sw) ? sw : null;
            Product.SmallPackWeightKg = Product.PackWeightValue.HasValue
                ? (Product.PackWeightUnit == "g" ? Product.PackWeightValue.Value / 1000m : Product.PackWeightValue.Value)
                : null;
            Product.SmallPackPrice = decimal.TryParse(TxtSmallPackPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sp) ? sp : null;
            if (string.IsNullOrWhiteSpace(Product.SmallPackLabel) && Product.PackWeightValue.HasValue)
                Product.SmallPackLabel = "Balenie " + FormatPackWeight(Product.PackWeightValue.Value, Product.PackWeightUnit);
            Product.LargePackLabel = TxtLargePackLabel.Text?.Trim() ?? "";
            Product.LargePackWeightKg = decimal.TryParse(TxtLargePackWeight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lw) ? lw : null;
            Product.LargePackPrice = decimal.TryParse(TxtLargePackPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lp) ? lp : null;
            decimal upo;
            Product.UnitPriceOverride = string.IsNullOrWhiteSpace(TxtUnitPriceOverride.Text) ? null
                : (decimal.TryParse(TxtUnitPriceOverride.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("sk-SK"), out upo)
                    || decimal.TryParse(TxtUnitPriceOverride.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out upo) ? (decimal?)upo : null);
            Product.Notes = TxtNotes.Text?.Trim() ?? "";
            Product.ShowEan = ChkShowEan.IsChecked == true;
            Product.Ean = Product.ShowEan ? NormalizeEan(TxtEan.Text) : null;
            Product.ShowSku = ChkShowSku.IsChecked == true;
            Product.Sku = Product.ShowSku ? (TxtSku.Text?.Trim() ?? "") : null;
            Product.ShowExpiry = ChkShowExpiry.IsChecked == true;
            Product.ExpiryDate = Product.ShowExpiry ? (TxtExpiryDate.Text?.Trim() ?? "") : null;
            Product.BarcodeEnabled = ChkBarcodeEnabled.IsChecked == true;
            var fmt = (CmbBarcodeFormat.SelectedItem as ComboBoxItem)?.Content as string ?? "EAN13";
            Product.BarcodeFormat = fmt;
            Product.BarcodeValue = Product.BarcodeEnabled ? BarcodeRenderer.NormalizeBarcodeValue(TxtBarcodeValue.Text, fmt) : null;
            Product.BarcodeShowText = ChkBarcodeShowText.IsChecked != false;
            Product.TemplateId = (CmbTemplate.SelectedItem as LabelTemplate)?.Id;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBarcodeWarning();
        }

        private void TxtBarcodeValue_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateBarcodeWarning();
        }

        private void CmbBarcodeFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBarcodeWarning();
        }

        private void UpdateBarcodeWarning()
        {
            if (ChkBarcodeEnabled?.IsChecked != true || TxtBarcodeValue == null || TxtBarcodeWarning == null || CmbBarcodeFormat == null) return;
            var text = TxtBarcodeValue.Text ?? "";
            var fmt = (CmbBarcodeFormat.SelectedItem as ComboBoxItem)?.Content as string ?? "EAN13";
            var (isValid, errorMsg) = BarcodeRenderer.ValidateBarcodeValue(text, fmt);
            TxtBarcodeWarning.Text = errorMsg ?? "";
            TxtBarcodeWarning.Visibility = !isValid && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string? NormalizeEan(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? value.Trim() : digits;
        }

        private static string FormatPackWeight(decimal value, string unit)
        {
            if (unit == "g")
                return $"{decimal.Round(value, 0):0} g";
            return $"{decimal.Round(value, 3):0.###} kg";
        }
    }
}
