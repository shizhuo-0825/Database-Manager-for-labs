using DevExpress.Xpf.Grid;
using DXApplication2.Modules.ViewModels;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace DXApplication2.Modules.Views
{
    public partial class GroupModuleView : UserControl
    {
        public GroupModuleView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (MainGrid.SelectedItems is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += OnSelectedItemsChanged;
            }
        }

        private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[GroupModule] SelectedItems changed. Count={MainGrid.SelectedItems.Count}");

            if (DataContext is not GroupModuleViewModel vm) return;

            var rows = new List<DataGroupRowViewModel>();
            foreach (var item in MainGrid.SelectedItems)
            {
                if (item is DataGroupRowViewModel row)
                    rows.Add(row);
            }

            System.Diagnostics.Debug.WriteLine($"[GroupModule] Broadcasting {rows.Count} rows");
            vm.UpdateSelectedRows(rows);
        }
    }
}