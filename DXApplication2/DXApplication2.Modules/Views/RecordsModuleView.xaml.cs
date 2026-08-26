using DXApplication2.Common.Models;
using DXApplication2.Modules.ViewModels;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace DXApplication2.Modules.Views
{
    public partial class RecordsModuleView : UserControl
    {
        public RecordsModuleView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (RecordsGrid.SelectedItems is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += OnSelectedItemsChanged;
            }
        }

        private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not RecordsModuleViewModel vm) return;

            var rows = new List<RecordDisplayRow>();
            foreach (var item in RecordsGrid.SelectedItems)
            {
                if (item is RecordDisplayRow row)
                    rows.Add(row);
            }
            vm.UpdateSelectedRows(rows);
        }
        private void ViewFullParamsClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        {
            var focused = RecordsGrid.CurrentItem as RecordDisplayRow;
            if (focused?.OriginalRecord == null) return;

            var dialog = new FullParamsDialog(focused.OriginalRecord);
            dialog.Owner = System.Windows.Window.GetWindow(this);
            dialog.ShowDialog();
        }
    }
}

