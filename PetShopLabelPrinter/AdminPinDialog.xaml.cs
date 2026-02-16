using System.Windows;

namespace PetShopLabelPrinter
{
    public partial class AdminPinDialog : Window
    {
        public string Pin { get; private set; } = "";

        public AdminPinDialog()
        {
            InitializeComponent();
        }

        private void TxtPin_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Pin = TxtPin.Password;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Pin = TxtPin.Password;
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
