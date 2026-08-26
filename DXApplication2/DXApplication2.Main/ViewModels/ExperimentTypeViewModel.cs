
using DevExpress.Mvvm;
using DXApplication2.Common.Models;
using DXApplication2.Common.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;

using MessageBox = System.Windows.MessageBox;

namespace DXApplication2.Main.ViewModels
{
    public class ExperimentTypesViewModel : ViewModelBase
    {
        public ObservableCollection<ExperimentType> ExperimentTypes { get; }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ReloadCommand { get; }
        public DelegateCommand<ExperimentType> DeleteSelectedCommand { get; }

        public ExperimentTypesViewModel()
        {
            ExperimentTypes = new ObservableCollection<ExperimentType>();

            SaveCommand = new DelegateCommand(Save);
            ReloadCommand = new DelegateCommand(LoadFromDatabase);
            DeleteSelectedCommand = new DelegateCommand<ExperimentType>(DeleteSelected);

            LoadFromDatabase();
        }

        private void LoadFromDatabase()
        {
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();

                var types = db.ExperimentTypes.OrderBy(e => e.Id).ToList();
                ExperimentTypes.Clear();
                foreach (var t in types)
                    ExperimentTypes.Add(t);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load: {ex.Message}", "Error");
            }
        }

        private void Save()
        {
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();

                // 获取数据库现有记录
                var existingInDb = db.ExperimentTypes.ToDictionary(e => e.Id);

                foreach (var item in ExperimentTypes)
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                        continue;  // 跳过空行(用户可能在新建行没输 Name 就跳过了)

                    if (item.Id == 0)
                    {
                        // 新增
                        db.ExperimentTypes.Add(item);
                    }
                    else if (existingInDb.TryGetValue(item.Id, out var dbItem))
                    {
                        // 更新
                        dbItem.Name = item.Name;
                        dbItem.DisplayName = item.DisplayName;
                        dbItem.Description = item.Description;
                        dbItem.Category = item.Category;
                        dbItem.DefaultPlotTemplate = item.DefaultPlotTemplate;
                    }
                }

                db.SaveChanges();

                // 保存后重新加载(新记录的 Id 会被回填)
                LoadFromDatabase();

                //MessageBox.Show("Saved successfully!", "Done");
            }
            catch (DbUpdateException dbEx)
            {
                // 常见:唯一约束冲突(Name 重复)
                MessageBox.Show(
                    $"Save failed (possibly duplicate Name):\n{dbEx.InnerException?.Message ?? dbEx.Message}",
                    "Error");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error");
            }
        }

        private void DeleteSelected(ExperimentType? selected)
        {
            if (selected == null)
            {
                MessageBox.Show("Please select a row first.", "Info");
                return;
            }

            var result = MessageBox.Show(
                $"Delete '{selected.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                if (selected.Id != 0)
                {
                    using var db = new AppDbContext();
                    var dbItem = db.ExperimentTypes.Find(selected.Id);
                    if (dbItem != null)
                    {
                        db.ExperimentTypes.Remove(dbItem);
                        db.SaveChanges();
                    }
                }
                ExperimentTypes.Remove(selected);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error");
            }
        }
    }
}