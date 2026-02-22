using System.Collections.Generic;
using System.Linq;
using System.Windows;
using PetShopLabelPrinter.Models;

namespace PetShopLabelPrinter
{
    public partial class TemplateSelectDialog : Window
    {
        public int? SelectedTemplateId { get; private set; }

        public TemplateSelectDialog(IReadOnlyList<LabelTemplate> templates, int? currentId = null)
        {
            InitializeComponent();
            CmbTemplate.ItemsSource = templates;
            if (currentId.HasValue)
                CmbTemplate.SelectedItem = templates.FirstOrDefault(t => t.Id == currentId.Value);
            if (CmbTemplate.SelectedItem == null)
                CmbTemplate.SelectedIndex = 0;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedTemplateId = (CmbTemplate.SelectedItem as LabelTemplate)?.Id;
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
