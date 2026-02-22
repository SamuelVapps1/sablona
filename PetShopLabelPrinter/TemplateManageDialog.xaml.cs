using System.Collections.Generic;
using System.Windows;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter
{
    public partial class TemplateManageDialog : Window
    {
        private readonly Database _db;
        private List<LabelTemplate> _templates = new List<LabelTemplate>();

        public TemplateManageDialog(Database db)
        {
            InitializeComponent();
            _db = db;
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates = _db.GetLabelTemplates();
            TemplateList.ItemsSource = null;
            TemplateList.ItemsSource = _templates;
        }

        private void TemplateList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var hasSelection = TemplateList.SelectedItem != null;
            BtnEdit.IsEnabled = hasSelection;
            BtnDelete.IsEnabled = hasSelection;
            if (TemplateList.SelectedItem is LabelTemplate t)
            {
                TxtCalOffsetX.Text = $"{t.OffsetXmm:0.##} mm";
                TxtCalOffsetY.Text = $"{t.OffsetYmm:0.##} mm";
                TxtCalScaleX.Text = $"{t.ScaleX * 100.0:0.##} %";
                TxtCalScaleY.Text = $"{t.ScaleY * 100.0:0.##} %";
            }
            else
            {
                TxtCalOffsetX.Text = "-";
                TxtCalOffsetY.Text = "-";
                TxtCalScaleX.Text = "-";
                TxtCalScaleY.Text = "-";
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var t = new LabelTemplate { Name = "Nová šablóna", WidthMm = 150, HeightMm = 38, PaddingMm = 2, OffsetXmm = 0, OffsetYmm = 0, ScaleX = 1.0, ScaleY = 1.0 };
            var dlg = new TemplateEditDialog(t) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _db.InsertLabelTemplate(dlg.Template);
                LoadTemplates();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateList.SelectedItem is not LabelTemplate t) return;
            var dlg = new TemplateEditDialog(t) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _db.UpdateLabelTemplate(dlg.Template);
                LoadTemplates();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateList.SelectedItem is not LabelTemplate t) return;
            if (MessageBox.Show($"Zmazať šablónu \"{t.Name}\"?", "Potvrdenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _db.DeleteLabelTemplate(t.Id);
            LoadTemplates();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
