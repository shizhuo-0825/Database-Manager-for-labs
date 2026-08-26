using DXApplication2.Common.Processing;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DXApplication2.Modules.Views
{
    public partial class EditLegendDialog : Window
    {
        private readonly List<Curve> _curves;
        public ObservableCollection<LabelEntry> Entries { get; } = new();

        public EditLegendDialog(List<Curve> curves)
        {
            InitializeComponent();
            _curves = curves;
            foreach (var c in curves)
            {
                Entries.Add(new LabelEntry { OriginalLabel = c.Label, NewLabel = c.Label });
            }
            LabelsList.ItemsSource = Entries;
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            // 把新 label 写回 Curve
            for (int i = 0; i < _curves.Count && i < Entries.Count; i++)
            {
                _curves[i].Label = Entries[i].NewLabel ?? "";
            }
            DialogResult = true;
            Close();
        }

        public class LabelEntry : INotifyPropertyChanged
        {
            public string OriginalLabel { get; set; } = "";
            private string _newLabel = "";
            public string NewLabel
            {
                get => _newLabel;
                set { _newLabel = value; OnPropertyChanged(); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}