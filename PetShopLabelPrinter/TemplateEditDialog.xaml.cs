using System.Globalization;
using System.Windows;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter
{
    public partial class TemplateEditDialog : Window
    {
        public LabelTemplate Template { get; private set; } = new LabelTemplate();

        public TemplateEditDialog(LabelTemplate? template = null)
        {
            InitializeComponent();
            if (template != null)
            {
                Template = template;
                TxtName.Text = template.Name;
                TxtWidthMm.Text = template.WidthMm.ToString("0.##", CultureInfo.InvariantCulture);
                TxtHeightMm.Text = template.HeightMm.ToString("0.##", CultureInfo.InvariantCulture);
                TxtPaddingMm.Text = template.PaddingMm.ToString("0.##", CultureInfo.InvariantCulture);
                TxtOffsetXmm.Text = template.OffsetXmm.ToString("0.##", CultureInfo.InvariantCulture);
                TxtOffsetYmm.Text = template.OffsetYmm.ToString("0.##", CultureInfo.InvariantCulture);
                TxtScaleX.Text = (template.ScaleX * 100.0).ToString("0.##", CultureInfo.InvariantCulture);
                TxtScaleY.Text = (template.ScaleY * 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Zadajte názov šablóny.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtWidthMm.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) || w < 20 || w > 300)
            {
                MessageBox.Show("Šírka musí byť 20–300 mm.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtHeightMm.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var h) || h < 15 || h > 150)
            {
                MessageBox.Show("Výška musí byť 15–150 mm.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtPaddingMm.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) || p < 0 || p > 20)
            {
                MessageBox.Show("Padding musí byť 0–20 mm.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtOffsetXmm.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var ox) || ox < -5 || ox > 5)
            {
                MessageBox.Show("Offset X musí byť -5 až +5 mm.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtOffsetYmm.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oy) || oy < -5 || oy > 5)
            {
                MessageBox.Show("Offset Y musí byť -5 až +5 mm.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtScaleX.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var sxPct) || sxPct < 90 || sxPct > 110)
            {
                MessageBox.Show("Scale X musí byť 90–110 %.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(TxtScaleY.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var syPct) || syPct < 90 || syPct > 110)
            {
                MessageBox.Show("Scale Y musí byť 90–110 %.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Template.Name = TxtName.Text.Trim();
            Template.WidthMm = w;
            Template.HeightMm = h;
            Template.PaddingMm = p;
            Template.OffsetXmm = ox;
            Template.OffsetYmm = oy;
            Template.ScaleX = sxPct / 100.0;
            Template.ScaleY = syPct / 100.0;
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
