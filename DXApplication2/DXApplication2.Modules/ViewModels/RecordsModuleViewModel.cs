using DevExpress.Mvvm;
using DXApplication2.Common.Data;
using DXApplication2.Common.Messages;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DXApplication2.Common.Services;
using DXApplication2.Common.ViewModels;
using MessageBox = System.Windows.MessageBox;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using DXApplication2.Common;
namespace DXApplication2.Modules.ViewModels
{
    // Records 表格显示的一行(动态字段)
    public class RecordDisplayRow : Dictionary<string, object?>
    {
        public DataRecord? OriginalRecord { get; set; }
    }
    public class RecordsModuleViewModel : ViewModelBase
    {
        public string Title => "Records";

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; RaisePropertyChanged(nameof(IsActive)); }
        }

        private bool _isClosed;
        public bool IsClosed
        {
            get => _isClosed;
            set { _isClosed = value; RaisePropertyChanged(nameof(IsClosed)); }
        }
        private List<ExperimentType> _allExpTypes = new();
        private List<ExperimentParams> _allExpParams = new();
        private List<RecordDisplayRow> _selectedRows = new();

        public void UpdateSelectedRows(List<RecordDisplayRow> rows)
        {
            _selectedRows = rows;
            BroadcastSelectedRecords();
        }

        private void BroadcastSelectedRecords()
        {
            var records = _selectedRows
                .Where(r => r.OriginalRecord != null)
                .Select(r => r.OriginalRecord!)
                .ToList();

            Messenger.Default.Send(new SelectedRecordsChangedMessage
            {
                Records = records,
                Source = "Records"
            });
        }
        // 选中的 Record
        private RecordDisplayRow? _selectedDisplayRow;
        public RecordDisplayRow? SelectedDisplayRow
        {
            get => _selectedDisplayRow;
            set
            {
                if (_selectedDisplayRow != value)
                {
                    _selectedDisplayRow = value;
                    _selectedRecord = value?.OriginalRecord;
                    RaisePropertyChanged(nameof(SelectedDisplayRow));
                    RaisePropertyChanged(nameof(SelectedRecord));
                    UpdateInspectorItems();
                }
            }
        }


        private DataRecord? _selectedRecord;
        public DataRecord? SelectedRecord
        {
            get => _selectedRecord;
        }

        // Inspector 数据
        private ObservableCollection<InspectorItem> _inspectorItems = new();
        public ObservableCollection<InspectorItem> InspectorItems
        {
            get => _inspectorItems;
            private set { _inspectorItems = value; RaisePropertyChanged(nameof(InspectorItems)); }
        }
        // 当前显示的 GroupId(null 表示没有加载任何 Group)
        private int? _currentGroupId;
        public int? CurrentGroupId
        {
            get => _currentGroupId;
            set { _currentGroupId = value; RaisePropertyChanged(nameof(CurrentGroupId)); RaisePropertyChanged(nameof(HeaderText)); }
        }

        // 当前 Group 的信息(供顶部标题显示)
        private DataGroup? _currentGroup;
        public DataGroup? CurrentGroup
        {
            get => _currentGroup;
            set { _currentGroup = value; RaisePropertyChanged(nameof(CurrentGroup)); RaisePropertyChanged(nameof(HeaderText)); }
        }

        // 顶部显示的标题(Group 信息)
        public string HeaderText
        {
            get
            {
                if (CurrentGroup == null)
                    return "No group selected. Double-click a group in the Group tab to load records.";
                return $"Records of {CurrentGroup.Material}, {CurrentGroup.ExperimentDate:yyyy-MM-dd}";
            }
        }

        // 表格数据源
        // 动态字段的显示行集合
        public ObservableCollection<RecordDisplayRow> DisplayRows { get; }

        // 原始 Records(供 Inspector 使用)
        private List<DataRecord> _originalRecords = new();

        public RecordsModuleViewModel()
        {
            DisplayRows = new ObservableCollection<RecordDisplayRow>();

            // 订阅"加载 Records"消息
            Messenger.Default.Register<LoadRecordsForGroupMessage>(this, msg =>
            {
                _ = LoadRecordsForGroupAsync(msg.GroupId);
            });

            Messenger.Default.Register<SaveCurrentModuleMessage>(this, msg =>
            {
                // Records 目前没有可编辑的字段,Save 不做事
            });

            Messenger.Default.Register<RefreshCurrentModuleMessage>(this, msg =>
            {
                if (CurrentGroupId.HasValue)
                {
                    _ = LoadRecordsForGroupAsync(CurrentGroupId.Value);
                }
            });
        }

        private async Task LoadRecordsForGroupAsync(int groupId)
        {
            try
            {
                using var db = new AppDbContext();

                // 加载 Group 信息(用于标题显示 + Inspector 判断类型)
                var group = await db.DataGroups
                    .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                    .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    MessageBox.Show($"Group #{groupId} not found.", "Error");
                    return;
                }

                CurrentGroupId = groupId;
                CurrentGroup = group;

                // ← 新增:加载缓存
                _allExpTypes = await db.ExperimentTypes.ToListAsync();
                _allExpParams = await db.ExperimentParamss
                    .Include(p => p.ExperimentType)
                    .ToListAsync();
                var query = db.DataRecords
                .Where(r => r.DataGroupId == groupId && !r.IsDeleted);
                if (!UiPreferences.ShowHidden)
                    query = query.Where(r => !r.IsHidden);
                _originalRecords = await query.OrderBy(r => r.RowIndex).ToListAsync();

                // 转换成 DisplayRows
                BuildDisplayRows();

                // 重置选中和 Inspector
                SelectedDisplayRow = null;
                InspectorItems = new ObservableCollection<InspectorItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load records: {ex.Message}", "Error");
            }
        }
        private void BuildDisplayRows()
        {
            DisplayRows.Clear();
            if (CurrentGroup == null || _originalRecords.Count == 0) return;

            // 1. 决定哪些字段要显示为列
            var fieldsToShow = DetermineFieldsForColumns();

            // 2. 为每个 Record 生成一行
            foreach (var record in _originalRecords)
            {
                var row = new RecordDisplayRow { OriginalRecord = record };

                // 固定列在前:Id
                row["Id"] = record.Id;

                // 物理量列(按 fieldsToShow 顺序)
                var parsed = ParseExptParams(record.ExptParams);
                foreach (var (columnHeader, param) in fieldsToShow)
                {
                    if (parsed.TryGetValue(param.FieldName, out var raw))
                    {
                        row[columnHeader] = FormatValue(param, raw);
                    }
                    else if (param.FieldName == "ROI")
                    {
                        // ROI 特殊:直接显示 JSON 里的值(不是数值,不需格式化)
                        row[columnHeader] = "—";
                    }
                    else
                    {
                        row[columnHeader] = "—";
                    }
                }

                // 固定列在后:Result + RowIndex
                row["Result"] = record.ResultQuantity?.ToString("G4", CultureInfo.InvariantCulture) ?? "—";
                row["RowIndex"] = record.RowIndex;

                DisplayRows.Add(row);
            }
        }

        // 决定哪些字段作为列显示,返回(列标题,对应的 Param)
        private List<(string columnHeader, ExperimentParams param)> DetermineFieldsForColumns()
        {
            var result = new List<(string, ExperimentParams)>();
            if (CurrentGroup == null) return result;

            // 当前 Group 的类型名字集合
            var groupTypeNames = CurrentGroup.GroupExperimentTypes
                .Where(get => get.ExperimentType != null)
                .Select(get => get.ExperimentType.Name)
                .ToHashSet();

            // 硬编码规则:SHGImaging→加 PSHG,DRR→加 Tr
            if (groupTypeNames.Contains("SHGImage")) groupTypeNames.Add("PSHG");
            if (groupTypeNames.Contains("DRR")) groupTypeNames.Add("Tr");

            var relevantTypeIds = _allExpTypes
                .Where(t => groupTypeNames.Contains(t.Name))
                .Select(t => t.Id)
                .ToHashSet();

            // 属于本 Group 的类型的字段
            var typeFields = _allExpParams
                .Where(p => p.ExperimentTypeId.HasValue && relevantTypeIds.Contains(p.ExperimentTypeId.Value))
                .Where(p => !p.IsDeprecated)
                .ToList();

            // 温度规则:只在 TempDep 时保留
            if (!groupTypeNames.Contains("TempDep"))
            {
                typeFields = typeFields.Where(f => f.FieldName != "Temperature").ToList();
            }

            // 属于 None Category 的字段(要根据 ROI_mode 决定要不要显示 ROI)
            var noneFields = _allExpParams
                .Where(p => !p.IsDeprecated)
                .Where(p =>
                {
                    // 属于 None Category:要么没关联 Type,要么关联的 Type 是 None Category
                    if (p.ExperimentType == null) return true;
                    return string.IsNullOrEmpty(p.ExperimentType.Category)
                           || p.ExperimentType.Category == "None";
                })
                .ToList();

            // ROI 规则:检查所有 Records 里的 ROI_mode
            bool showRoi = ShouldShowRoi();
            if (showRoi)
            {
                // 只保留 ROI 一个字段
                var roi = noneFields.FirstOrDefault(f => f.FieldName == "ROI");
                if (roi != null) typeFields.Add(roi);
            }
            // ROI_mode 无论如何都不显示(不加到 typeFields)

            // 排序:温度在最前(如果有),其他按 DisplayOrder
            var temp = typeFields.FirstOrDefault(f => f.FieldName == "Temperature");
            var rest = typeFields.Where(f => f.FieldName != "Temperature")
                                 .OrderBy(f => f.DisplayOrder ?? int.MaxValue)
                                 .ThenBy(f => f.FieldName)
                                 .ToList();

            var ordered = new List<ExperimentParams>();
            if (temp != null) ordered.Add(temp);
            ordered.AddRange(rest);

            // 生成列标题(带单位)
            foreach (var p in ordered)
            {
                result.Add((FormatColumnHeader(p), p));
            }

            return result;
        }

        private bool ShouldShowRoi()
        {
            // 只要有一个 Record 的 ROI_mode 是 "not_enabled" 就不显示?
            // 按你的规则:not_enabled 表示不启用 → 不显示
            // 有多种模式的 Record 混在一起时,只要至少一个不是 not_enabled 就显示?
            // 我采取:所有 Records 都是 "not_enabled" 才隐藏,否则显示
            // 也就是:只要有任何一个 Record 启用了 ROI,就显示 ROI 列
            foreach (var r in _originalRecords)
            {
                var dict = ParseExptParams(r.ExptParams);
                if (dict.TryGetValue("ROI_mode", out var mode) && mode != "not_enabled")
                {
                    return true;
                }
            }
            return false;
        }

        private string FormatColumnHeader(ExperimentParams param)
        {
            var name = string.IsNullOrEmpty(param.DisplayName) ? param.FieldName : param.DisplayName;
            var unit = param.Unit ?? "";

            // DRR 相关:加 u
            if (param.ExperimentType?.Name == "DRR")
            {
                unit = string.IsNullOrEmpty(unit) ? "× 10⁶" : "u" + unit;
            }

            return string.IsNullOrEmpty(unit) ? name : $"{name} ({unit})";
        }

        private string FormatValue(ExperimentParams param, string raw)
        {
            // ROI 是字符串(比如 "225_260_236_272"),原样显示
            if (param.FieldName == "ROI")
            {
                return raw;
            }

            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return raw;  // 非数值直接原样
            }

            // 温度:2 位小数
            if (param.FieldName == "Temperature")
            {
                return value.ToString("F2", CultureInfo.InvariantCulture);
            }

            // DRR 相关:× 10^6,2 位有效数字
            if (param.ExperimentType?.Name == "DRR")
            {
                return FormatFourSignificantDigits(value * 1e6);
            }

            return FormatFourSignificantDigits(value);
        }

        private static string FormatFourSignificantDigits(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value.ToString();
            if (value == 0) return "0";

            double abs = Math.Abs(value);
            int magnitude = (int)Math.Floor(Math.Log10(abs));
            int decimalPlaces = 3 - magnitude;

            if (decimalPlaces >= 0)
            {
                return value.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture);
            }
            else
            {
                double scale = Math.Pow(10, -decimalPlaces);
                double rounded = Math.Round(value / scale) * scale;
                return rounded.ToString("F0", CultureInfo.InvariantCulture);
            }
        }

        private static Dictionary<string, string> ParseExptParams(string? json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                }
            }
            catch { }
            return result;
        }
        private void UpdateInspectorItems()
        {
            if (SelectedRecord == null || CurrentGroup == null)
            {
                InspectorItems = new ObservableCollection<InspectorItem>();
                return;
            }

            try
            {
                var items = InspectorService.GetInspectorItemsForRecord(
                    SelectedRecord,
                    CurrentGroup,
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
    }
}