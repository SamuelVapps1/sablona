using System.Globalization;
using System.Windows;
using PetShopLabelPrinter.Models;
using PetShopLabelPrinter.Services;

namespace PetShopLabelPrinter
{
    public partial class A4SettingsDialog : Window
    {
        private readonly PrintService _printService;
        public A4SheetSettings Settings { get; private set; }

        public A4SettingsDialog(A4SheetSettings settings, PrintService printService)
        {
            InitializeComponent();
            _printService = printService;
            Settings = settings ?? new A4SheetSettings();

            TxtMargin.Text = Settings.SheetMarginMm.ToString("0.##", CultureInfo.InvariantCulture);
            TxtGap.Text = Settings.GapMm.ToString("0.##", CultureInfo.InvariantCulture);
            TxtScaleX.Text = (Settings.CalibrationScaleX * 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            TxtScaleY.Text = (Settings.CalibrationScaleY * 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            TxtOffsetX.Text = Settings.CalibrationOffsetXmm.ToString("0.##", CultureInfo.InvariantCulture);
            TxtOffsetY.Text = Settings.CalibrationOffsetYmm.ToString("0.##", CultureInfo.InvariantCulture);
            ChkDebugLayout.IsChecked = Settings.DebugLayout;
        }

        private bool TryReadSettings(out A4SheetSettings parsed)
        {
            parsed = new A4SheetSettings();
            if (!double.TryParse(TxtMargin.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var margin) || margin < 0 || margin > 40)
            {
                MessageBox.Show("Margin musí byť 0–40 mm.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!double.TryParse(TxtGap.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var gap) || gap < 0 || gap > 20)
            {
                MessageBox.Show("Gap musí byť 0–20 mm.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!double.TryParse(TxtScaleX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sxPct) || sxPct < 95 || sxPct > 105)
            {
                MessageBox.Show("Scale X musí byť 95–105 %.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!double.TryParse(TxtScaleY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var syPct) || syPct < 95 || syPct > 105)
            {
                MessageBox.Show("Scale Y musí byť 95–105 %.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!double.TryParse(TxtOffsetX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var ox) || ox < -5 || ox > 5)
            {
                MessageBox.Show("Offset X musí byť -5 až +5 mm.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!double.TryParse(TxtOffsetY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oy) || oy < -5 || oy > 5)
            {
                MessageBox.Show("Offset Y musí byť -5 až +5 mm.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            parsed = new A4SheetSettings
            {
                SheetWidthMm = 210,
                SheetHeightMm = 297,
                SheetMarginMm = margin,
                GapMm = gap,
                Orientation = "Portrait",
                CalibrationScaleX = sxPct / 100.0,
                CalibrationScaleY = syPct / 100.0,
                CalibrationOffsetXmm = ox,
                CalibrationOffsetYmm = oy,
                DebugLayout = ChkDebugLayout.IsChecked == true
            };
            return true;
        }

        private void BtnTestPrintA4_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSettings(out var s)) return;
            if (!_printService.PrintA4TestPage(s))
                MessageBox.Show("Nepodarilo sa vytlačiť A4 test stránku.", "A4 nastavenia", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSettings(out var s)) return;
            Settings = s;
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
