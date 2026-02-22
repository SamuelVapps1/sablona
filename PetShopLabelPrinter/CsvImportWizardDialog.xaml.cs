using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PetShopLabelPrinter.Data;
using PetShopLabelPrinter.Services;

namespace PetShopLabelPrinter
{
    public partial class CsvImportWizardDialog : Window
    {
        private readonly Database _db;
        private readonly CsvImportService _importService;
        private int _step;
        private string? _filePath;
        private List<string> _rawLines = new List<string>();
        private List<string[]> _rows = new List<string[]>();
        private char _delimiter = ';';
        private Encoding _encoding = Encoding.UTF8;
        private bool _skipHeader = true;
        private Dictionary<string, int> _mapping = new Dictionary<string, int>();

        private void ReparseRows()
        {
            _rows = _rawLines.Select(l => CsvImportService.ParseLine(l, _delimiter)).ToList();
        }

        public CsvImportWizardDialog(Database db)
        {
            InitializeComponent();
            _db = db;
            _importService = new CsvImportService(db);
            ShowStep(0);
        }

        private void ShowStep(int step)
        {
            _step = step;
            BtnBack.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility = step < 2 ? Visibility.Visible : Visibility.Collapsed;
            BtnImport.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;

            if (step == 0)
            {
                TxtStepTitle.Text = "Krok 1: Vyberte CSV súbor";
                StepContent.Content = BuildStepFile();
            }
            else if (step == 1)
            {
                TxtStepTitle.Text = "Krok 2: Oddeľovač a náhľad";
                StepContent.Content = BuildStepPreview();
            }
            else
            {
                TxtStepTitle.Text = "Krok 3: Mapovanie stĺpcov";
                StepContent.Content = BuildStepMapping();
            }
        }

        private UIElement BuildStepFile()
        {
            var sp = new StackPanel();
            var btn = new Button { Content = "Vybrať súbor...", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Left };
            var txt = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
            btn.Click += (s, e) =>
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "CSV súbory|*.csv|Všetky súbory|*.*",
                    Title = "Vybrať CSV súbor"
                };
                if (dlg.ShowDialog() == true)
                {
                    _filePath = dlg.FileName;
                    txt.Text = _filePath;
                }
            };
            if (!string.IsNullOrEmpty(_filePath)) txt.Text = _filePath;
            sp.Children.Add(btn);
            sp.Children.Add(txt);
            return sp;
        }

        private UIElement BuildStepPreview()
        {
            var sp = new StackPanel();

            var delimPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            delimPanel.Children.Add(new TextBlock { Text = "Oddeľovač:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            var rbComma = new RadioButton { Content = "Čiarka (,)", IsChecked = _delimiter == ',', Margin = new Thickness(0, 0, 16, 0) };
            var rbSemicolon = new RadioButton { Content = "Bodkočiarka (;)", IsChecked = _delimiter == ';' };

            var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 11 };
            sp.Tag = preview;

            void ReloadPreview(StackPanel container)
            {
                if (container.Tag is TextBlock p)
                {
                    if (_rows.Count == 0) p.Text = "Žiadne dáta.";
                    else
                    {
                        var sb = new StringBuilder();
                        var show = Math.Min(5, _rows.Count);
                        for (var i = 0; i < show; i++)
                            sb.AppendLine(string.Join(_delimiter == ',' ? ", " : "; ", _rows[i].Select(c => c.Length > 20 ? c.Substring(0, 17) + "..." : c)));
                        p.Text = sb.ToString();
                    }
                }
            }
            rbComma.Checked += (s, e) => { _delimiter = ','; ReparseRows(); ReloadPreview(sp); };
            rbSemicolon.Checked += (s, e) => { _delimiter = ';'; ReparseRows(); ReloadPreview(sp); };
            delimPanel.Children.Add(rbComma);
            delimPanel.Children.Add(rbSemicolon);
            sp.Children.Add(delimPanel);

            var chkHeader = new CheckBox { Content = "Prvý riadok je hlavička", IsChecked = _skipHeader, Margin = new Thickness(0, 0, 0, 8) };
            chkHeader.Checked += (s, e) => { _skipHeader = true; ReloadPreview(sp); };
            chkHeader.Unchecked += (s, e) => { _skipHeader = false; ReloadPreview(sp); };
            sp.Children.Add(chkHeader);
            sp.Children.Add(preview);

            ReloadPreview(sp);
            return sp;
        }

        private UIElement BuildStepMapping()
        {
            var sp = new StackPanel();
            if (_rows.Count == 0) { sp.Children.Add(new TextBlock { Text = "Žiadne dáta na mapovanie." }); return sp; }

            var headerRow = _skipHeader && _rows.Count > 1 ? _rows[0] : _rows[0];
            var colCount = headerRow.Length;

            sp.Children.Add(new TextBlock { Text = "Priraďte stĺpce CSV k poliam aplikácie:", Margin = new Thickness(0, 0, 0, 8) });

            var targets = new[] { ("Name", "Názov (ProductName)"), ("Price", "Cena"), ("SKU", "SKU"), ("EAN", "EAN"), ("ExpiryDate", "Dátum spotreby") };
            foreach (var (field, label) in targets)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                row.Children.Add(new TextBlock { Text = label + ":", Width = 160, VerticalAlignment = VerticalAlignment.Center });
                var combo = new ComboBox { Width = 220, VerticalAlignment = VerticalAlignment.Center };
                combo.Items.Add(new ComboBoxItem { Content = "(nevybrané)", Tag = -1 });
                for (var c = 0; c < colCount; c++)
                {
                    var header = c < headerRow.Length ? headerRow[c] : $"Stĺpec {c + 1}";
                    if (string.IsNullOrWhiteSpace(header)) header = $"Stĺpec {c + 1}";
                    combo.Items.Add(new ComboBoxItem { Content = $"{c + 1}: {header}", Tag = c });
                }
                var currentCol = _mapping.TryGetValue(field, out var col) && col >= 0 && col < colCount ? col : -1;
                for (var i = 0; i < combo.Items.Count; i++)
                {
                    if (combo.Items[i] is ComboBoxItem item && (int)(item.Tag ?? -1) == currentCol)
                    { combo.SelectedIndex = i; break; }
                }
                if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                combo.SelectionChanged += (s, e) =>
                {
                    if (combo.SelectedItem is ComboBoxItem si && si.Tag is int idx)
                        _mapping[field] = idx;
                };
                row.Children.Add(combo);
                sp.Children.Add(row);
            }
            return sp;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            ShowStep(_step - 1);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                {
                    MessageBox.Show("Vyberte platný CSV súbor.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var (rawLines, rows, delim, enc) = CsvImportService.ReadCsv(_filePath);
                _rawLines = rawLines;
                _delimiter = delim;
                _encoding = enc;
                ReparseRows();
                if (_rows.Count == 0)
                {
                    MessageBox.Show("Súbor je prázdny alebo sa nepodarilo prečítať.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var (presetMapping, presetDelim, presetSkip) = _importService.LoadMappingPreset();
                if (presetMapping != null) { _mapping = new Dictionary<string, int>(presetMapping); _delimiter = presetDelim; _skipHeader = presetSkip; ReparseRows(); }
            }
            else if (_step == 1)
            {
                if (!_mapping.ContainsKey("Name") || _mapping["Name"] < 0)
                {
                    MessageBox.Show("Mapujte aspoň stĺpec Názov.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            ShowStep(_step + 1);
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (!_mapping.ContainsKey("Name") || _mapping["Name"] < 0)
            {
                MessageBox.Show("Mapujte aspoň stĺpec Názov.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _importService.SaveMappingPreset(_mapping, _delimiter, _skipHeader);
            var (created, updated) = _importService.Import(_rows, _mapping, _skipHeader);
            MessageBox.Show($"Import dokončený.\nVytvorené: {created}\nAktualizované: {updated}", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
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
