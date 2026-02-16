using System.Globalization;
using System.Windows;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter
{
    public partial class ProductEditDialog : Window
    {
        public Product Product { get; private set; } = new Product();

        public ProductEditDialog(Product? product = null)
        {
            InitializeComponent();
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
            Product.SmallPackWeightKg = decimal.TryParse(TxtSmallPackWeight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sw) ? sw : null;
            Product.SmallPackPrice = decimal.TryParse(TxtSmallPackPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sp) ? sp : null;
            Product.LargePackLabel = TxtLargePackLabel.Text?.Trim() ?? "";
            Product.LargePackWeightKg = decimal.TryParse(TxtLargePackWeight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lw) ? lw : null;
            Product.LargePackPrice = decimal.TryParse(TxtLargePackPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var lp) ? lp : null;
            decimal upo;
            Product.UnitPriceOverride = string.IsNullOrWhiteSpace(TxtUnitPriceOverride.Text) ? null
                : (decimal.TryParse(TxtUnitPriceOverride.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("sk-SK"), out upo)
                    || decimal.TryParse(TxtUnitPriceOverride.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out upo) ? (decimal?)upo : null);
            Product.Notes = TxtNotes.Text?.Trim() ?? "";
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
