using System.Globalization;
using System.Windows;

namespace PetShopLabelPrinter
{
    public partial class CopiesDialog : Window
    {
        public int Copies { get; private set; } = 1;

        public CopiesDialog(int initial = 1)
        {
            InitializeComponent();
            TxtCopies.Text = (initial < 1 ? 1 : initial).ToString(CultureInfo.InvariantCulture);
            TxtCopies.SelectAll();
            TxtCopies.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtCopies.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies) || copies < 1 || copies > 500)
            {
                MessageBox.Show("Zadajte počet 1–500.", "Počet kópií", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Copies = copies;
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
