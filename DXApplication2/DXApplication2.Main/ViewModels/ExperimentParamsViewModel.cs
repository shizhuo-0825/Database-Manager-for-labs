using DevExpress.Mvvm;
using DXApplication2.Common.Models;
using DXApplication2.Common.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

using MessageBox = System.Windows.MessageBox;

namespace DXApplication2.Main.ViewModels
{
    public class ExperimentParamsViewModel : ViewModelBase
    {
        // 所有实验类型(下拉数据源)

        public ObservableCollection<ExperimentType> AllExperimentTypes { get; }



        // 所有参数(数据源)
        public ObservableCollection<ExperimentParams> ExperimentParams { get; }

        // 筛选后视图(表格实际绑定的)
        public ICollectionView FilteredParameters { get; }

        // 筛选值:null=显示全部,否则显示对应 ExperimentTypeId 的参数
        private int? _selectedFilterTypeId;
        public int? SelectedFilterTypeId
        {
            get => _selectedFilterTypeId;
            set
            {
                if (_selectedFilterTypeId != value)
                {
                    _selectedFilterTypeId = value;
                    RaisePropertyChanged(nameof(SelectedFilterTypeId));
                    FilteredParameters.Refresh();
                }
            }
        }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ReloadCommand { get; }
        public DelegateCommand<ExperimentParams> DeleteSelectedCommand { get; }
        public ICollectionView GroupedExperimentTypes { get; }
        public ExperimentParamsViewModel()
        {
            AllExperimentTypes = new ObservableCollection<ExperimentType>();
            ExperimentParams = new ObservableCollection<ExperimentParams>();

            // 建立筛选视图
            FilteredParameters = CollectionViewSource.GetDefaultView(ExperimentParams);
            FilteredParameters.Filter = FilterPredicate;

            // 建立分组视图(新加)
            GroupedExperimentTypes = new CollectionViewSource { Source = AllExperimentTypes }.View;
            GroupedExperimentTypes.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            SaveCommand = new DelegateCommand(Save);
            ReloadCommand = new DelegateCommand(LoadFromDatabase);
            DeleteSelectedCommand = new DelegateCommand<ExperimentParams>(DeleteSelected);

            LoadFromDatabase();
        }

        private bool FilterPredicate(object obj)
        {
            if (SelectedFilterTypeId == null) return true;
            if (obj is not ExperimentParams p) return false;
            return p.ExperimentTypeId == SelectedFilterTypeId.Value;
        }

        private void LoadFromDatabase()
        {
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();

                // 加载所有实验类型(下拉源)
                var types = db.ExperimentTypes.OrderBy(e => e.Name).ToList();
                AllExperimentTypes.Clear();
                foreach (var t in types)
                    AllExperimentTypes.Add(t);

                // 加载所有参数
                var parameters = db.ExperimentParamss
                    .OrderBy(p => p.ExperimentTypeId)
                    .ThenBy(p => p.DisplayOrder == null)
                    .ThenBy(p => p.DisplayOrder)
                    .ThenBy(p => p.FieldName)
                    .ThenBy(p => p.Id)
                    .ToList();
                //MessageBox.Show(parameters.Count.ToString());
                ExperimentParams.Clear();
                foreach (var p in parameters)
                    ExperimentParams.Add(p);

                FilteredParameters.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load: {ex.Message}", "Error");
            }
            FilteredParameters.Refresh();
        }

        private void Save()
        {
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();

                var existingInDb = db.ExperimentParamss.ToDictionary(p => p.Id);

                foreach (var item in ExperimentParams)
                {
                    if (string.IsNullOrWhiteSpace(item.FieldName)) continue;
                    if (item.ExperimentTypeId == 0)
                    {
                        MessageBox.Show(
                            $"Parameter '{item.FieldName}' has no Experiment Type selected. Skipping.",
                            "Warning");
                        continue;
                    }

                    if (item.Id == 0)
                    {
                        // 新增(不加载导航属性,避免 EF Core 尝试保存 ExperimentType)
                        var newItem = new ExperimentParams
                        {
                            FieldName = item.FieldName,
                            DisplayName = item.DisplayName,
                            Unit = item.Unit,
                            DataType = item.DataType,
                            Description = item.Description,
                            DisplayOrder = item.DisplayOrder,
                            IsDeprecated = item.IsDeprecated,
                            ExperimentTypeId = item.ExperimentTypeId
                        };
                        db.ExperimentParamss.Add(newItem);
                    }
                    else if (existingInDb.TryGetValue(item.Id, out var dbItem))
                    {
                        dbItem.FieldName = item.FieldName;
                        dbItem.DisplayName = item.DisplayName;
                        dbItem.Unit = item.Unit;
                        dbItem.DataType = item.DataType;
                        dbItem.Description = item.Description;
                        dbItem.DisplayOrder = item.DisplayOrder;
                        dbItem.IsDeprecated = item.IsDeprecated;
                        dbItem.ExperimentTypeId = item.ExperimentTypeId;
                    }
                }

                db.SaveChanges();
                LoadFromDatabase();

                //MessageBox.Show("Saved successfully!", "Done");
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show(
                    $"Save failed (possibly duplicate Name within same Experiment Type):\n{dbEx.InnerException?.Message ?? dbEx.Message}",
                    "Error");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error");
            }
        }

        private void DeleteSelected(ExperimentParams? selected)
        {
            if (selected == null)
            {
                MessageBox.Show("Please select a row first.", "Info");
                return;
            }

            var result = MessageBox.Show(
                $"Delete '{selected.FieldName}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                if (selected.Id != 0)
                {
                    using var db = new AppDbContext();
                    var dbItem = db.ExperimentParamss.Find(selected.Id);
                    if (dbItem != null)
                    {
                        db.ExperimentParamss.Remove(dbItem);
                        db.SaveChanges();
                    }
                }
                ExperimentParams.Remove(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error");
            }
        }

    }
}