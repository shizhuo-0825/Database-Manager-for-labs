using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using DXApplication2.Common.Data;
using DXApplication2.Common.Messages;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DXApplication2.Common.ViewModels;
using DXApplication2.Common.Services;
using MessageBox = System.Windows.MessageBox;
using DXApplication2.Common;

namespace DXApplication2.Modules.ViewModels
{
    public class GroupModuleViewModel : ViewModelBase
    {
        // Tab 显示的名字
        public string Title => "Group";
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    RaisePropertyChanged(nameof(IsActive));
                }
            }
        }
        // 静态创建方法(Bootstrapper 需要,如果你 Bootstrapper 里用的是 lambda 就不需要这个)
        public static object Create()
        {
            return ViewModelSource.Create(() => new GroupModuleViewModel());
        }

        // 表格数据源
        public ObservableCollection<DataGroupRowViewModel> GroupRows { get; }

        // 每个 Category 对应的下拉选项("" 表示"未选")
        public ObservableCollection<string> SourceOptions { get; }
        public ObservableCollection<string> MethodOptions { get; }
        public ObservableCollection<string> PumpOptions { get; }
        private List<DataGroupRowViewModel> _selectedRows = new();

        public void UpdateSelectedRows(List<DataGroupRowViewModel> rows)
        {
            _selectedRows = rows;
            BroadcastSelectedRecords();
        }
        // 选中项(供 Inspector 面板显示)
        private DataGroupRowViewModel? _selectedRow;
        public DataGroupRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (_selectedRow != value)
                {
                    _selectedRow = value;
                    RaisePropertyChanged(nameof(SelectedRow));
                    UpdateInspectorItemsAsync();
                }
            }
        }

        private async void BroadcastSelectedRecords()
        {
            var selectedGroupIds = _selectedRows
                .Where(r => r.OriginalGroup != null)
                .Select(r => r.OriginalGroup!.Id)
                .ToList();

            if (selectedGroupIds.Count == 0)
            {
                Messenger.Default.Send(new SelectedRecordsChangedMessage
                {
                    Records = new List<DataRecord>(),
                    Source = "Group"
                });
                return;
            }

            using var db = new AppDbContext();
            var query = db.DataRecords
                .Where(r => selectedGroupIds.Contains(r.DataGroupId) && !r.IsDeleted);
            if (!UiPreferences.ShowHidden)
                query = query.Where(r => !r.IsHidden);
            var records = await query.ToListAsync();
            Messenger.Default.Send(new SelectedRecordsChangedMessage
            {
                Records = records,
                Source = "Group"
            });
        }

        // Inspector 显示的键值对
        private List<ExperimentType> _allExpTypes = new();
        private List<ExperimentParams> _allExpParams = new();

        // Inspector 数据
        private ObservableCollection<InspectorItem> _inspectorItems = new();
        public ObservableCollection<InspectorItem> InspectorItems
        {
            get => _inspectorItems;
            private set { _inspectorItems = value; RaisePropertyChanged(nameof(InspectorItems)); }
        }
        private async void UpdateInspectorItemsAsync()
        {
            if (SelectedRow?.OriginalGroup == null)
            {
                InspectorItems = new ObservableCollection<InspectorItem>();
                return;
            }

            try
            {
                using var db = new AppDbContext();

                // 加载选中 Group 的所有 Records
                var groupId = SelectedRow.OriginalGroup.Id;
                var records = await db.DataRecords
                    .Where(r => r.DataGroupId == groupId)
                    .ToListAsync();

                // 调用 InspectorService 计算
                var items = InspectorService.GetInspectorItemsForGroup(
                    SelectedRow.OriginalGroup,
                    records,
                    _allExpTypes,
                    _allExpParams);

                InspectorItems = new ObservableCollection<InspectorItem>(items);
            }
            catch (Exception ex)
            {
                InspectorItems = new ObservableCollection<InspectorItem>
        {
            new InspectorItem("Error", ex.Message)
        };
            }
        }
        // 当前打开的文件夹(用于按 folder 过滤 Groups)
        private string? _currentFolder;

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ReloadCommand { get; }
        public DelegateCommand<DataGroupRowViewModel> OpenRecordsCommand { get; }

        public GroupModuleViewModel()
        {
            GroupRows = new ObservableCollection<DataGroupRowViewModel>();
            SourceOptions = new ObservableCollection<string>();
            MethodOptions = new ObservableCollection<string>();
            PumpOptions = new ObservableCollection<string>();

            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            ReloadCommand = new DelegateCommand(async () => await LoadAsync());
            OpenRecordsCommand = new DelegateCommand<DataGroupRowViewModel>(OpenRecords);
            // 订阅跨模块消息
            Messenger.Default.Register<SaveCurrentModuleMessage>(this, msg =>
            {
                SaveCommand.Execute(null);
            });

            Messenger.Default.Register<RefreshCurrentModuleMessage>(this, msg =>
            {
                _currentFolder = msg.FolderPath;
                _ = LoadAsync();
            });

            Messenger.Default.Register<FolderChangedMessage>(this, msg =>
            {
                _currentFolder = msg.FolderPath;
                _ = LoadAsync();
            });

            // 首次加载
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = new AppDbContext();
                // 缓存 ExperimentTypes(Inspector 会用)
                _allExpTypes = await db.ExperimentTypes.ToListAsync();
                var allTypes = _allExpTypes;

                // 缓存 ExperimentParams,包含 ExperimentType 导航属性(Inspector 会用)
                _allExpParams = await db.ExperimentParamss
                    .Include(p => p.ExperimentType)
                    .ToListAsync();

                // 加载 ExperimentTypes 填充下拉选项

                var sources = allTypes.Where(t => t.Category == "Source")
                                      .Select(t => t.Name).OrderBy(n => n).ToList();
                var methods = allTypes.Where(t => t.Category == "Method")
                                      .Select(t => t.Name).OrderBy(n => n).ToList();
                var pumps = allTypes.Where(t => t.Category == "Pump")
                                    .Select(t => t.Name).OrderBy(n => n).ToList();

                SourceOptions.Clear();
                SourceOptions.Add("");
                foreach (var s in sources) SourceOptions.Add(s);

                MethodOptions.Clear();
                MethodOptions.Add("");
                foreach (var m in methods) MethodOptions.Add(m);

                PumpOptions.Clear();
                PumpOptions.Add("");
                foreach (var p in pumps) PumpOptions.Add(p);

                // 按 _currentFolder 过滤(如果有)
                var query = db.DataGroups
                    .Where(g => !g.IsDeleted)
                    .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                    .AsQueryable();

                query = query.Where(g => !g.IsDeleted);
                if (!UiPreferences.ShowHidden)
                    query = query.Where(g => !g.IsHidden);

                if (!string.IsNullOrEmpty(_currentFolder))
                {
                    query = query.Where(g => g.CsvPath.StartsWith(_currentFolder));

                }

                var groups = await query.OrderByDescending(g => g.Id).ToListAsync();

                GroupRows.Clear();
                foreach (var g in groups)
                {
                    var typesByCategory = g.GroupExperimentTypes
                        .Where(get => get.ExperimentType != null)
                        .GroupBy(get => get.ExperimentType.Category ?? "None")
                        .ToDictionary(
                            grp => grp.Key,
                            grp => string.Join(", ", grp.Select(get => get.ExperimentType.Name).OrderBy(n => n)));

                    var row = new DataGroupRowViewModel
                    {
                        Id = g.Id,
                        ExperimentDate = g.ExperimentDate,
                        Material = g.Material,
                        Notes = g.Notes,        // ← 加这一行
                        SourceTypes = typesByCategory.GetValueOrDefault("Source", ""),
                        MethodTypes = typesByCategory.GetValueOrDefault("Method", ""),
                        PumpTypes = typesByCategory.GetValueOrDefault("Pump", ""),
                        OriginalGroup = g,
                    };
                    row.IsDirty = false;
                    GroupRows.Add(row);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load: {ex.Message}", "Error");
            }
        }

        private async Task SaveAsync()
        {
            var dirtyRows = GroupRows.Where(r => r.IsDirty && r.OriginalGroup != null).ToList();
            if (dirtyRows.Count == 0)
            {
                return; // 静默:没改就不弹提示,避免多次广播时反复弹窗
            }

            try
            {
                using var db = new AppDbContext();
                var allTypes = await db.ExperimentTypes.ToListAsync();
                var typesByName = allTypes.ToDictionary(t => t.Name);

                foreach (var row in dirtyRows)
                {
                    var groupId = row.OriginalGroup!.Id;

                    var existing = await db.GroupExperimentTypes
                        .Where(get => get.DataGroupId == groupId)
                        .Include(get => get.ExperimentType)
                        .ToListAsync();

                    // 目标类型(用户在表格里选择的)
                    var targetTypeNames = new HashSet<string>();
                    AddNames(targetTypeNames, row.SourceTypes);
                    AddNames(targetTypeNames, row.MethodTypes);
                    AddNames(targetTypeNames, row.PumpTypes);
                    var groupEntity = await db.DataGroups.FindAsync(row.OriginalGroup!.Id);
                    if (groupEntity != null)
                    {
                        groupEntity.Notes = row.Notes;
                        // 顺便把 Material 和 Date 也写回(如果你希望它们也可编辑)
                        // groupEntity.Material = row.Material;
                        // groupEntity.ExperimentDate = row.ExperimentDate;
                    }
                    // 保留 Category=None 的关联(不该被删)
                    foreach (var g in existing)
                    {
                        var cat = g.ExperimentType?.Category;
                        if (cat == "None" || string.IsNullOrEmpty(cat))
                        {
                            if (g.ExperimentType != null)
                                targetTypeNames.Add(g.ExperimentType.Name);
                        }
                    }

                    var currentTypeNames = existing
                        .Where(get => get.ExperimentType != null)
                        .Select(get => get.ExperimentType.Name)
                        .ToHashSet();

                    var toRemove = existing
                        .Where(get => get.ExperimentType != null &&
                                      !targetTypeNames.Contains(get.ExperimentType.Name))
                        .ToList();
                    db.GroupExperimentTypes.RemoveRange(toRemove);

                    var toAdd = targetTypeNames.Except(currentTypeNames);
                    foreach (var name in toAdd)
                    {
                        if (typesByName.TryGetValue(name, out var expType))
                        {
                            db.GroupExperimentTypes.Add(new GroupExperimentType
                            {
                                DataGroupId = groupId,
                                ExperimentTypeId = expType.Id
                            });
                        }
                    }

                    row.IsDirty = false;
                }

                await db.SaveChangesAsync();
                //MessageBox.Show($"Saved {dirtyRows.Count} row(s).", "Done");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error");
            }
        }

        private void OpenRecords(DataGroupRowViewModel? row)
        {
            if (row == null) return;

            // 广播消息:请求打开 Records tab,显示该 Group
            Messenger.Default.Send(new OpenRecordsForGroupMessage
            {
                GroupId = row.Id
            });
        }

        private static void AddNames(HashSet<string> set, string commaSeparated)
        {
            if (string.IsNullOrWhiteSpace(commaSeparated)) return;
            foreach (var part in commaSeparated.Split(','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    set.Add(trimmed);
            }
        }
    }


}