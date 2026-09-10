using DevExpress.Mvvm;
using DXApplication2.Common.Messages;
using DXApplication2.Common.Models;
using DXApplication2.Common.Plotting;
using DXApplication2.Common.Processing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using DXApplication2.Common.IO;
using Microsoft.Win32;
using DXApplication2.Modules.Views;
using DXApplication2.Modules.ViewModels;
using DevExpress.Xpf.Core;


namespace DXApplication2.Modules.ViewModels
{
    public class PlotModuleViewModel : ViewModelBase
    {
        public class CurveOption : ViewModelBase
        {
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    RaisePropertyChanged(nameof(IsSelected));
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            private string _label = string.Empty;
            public string Label
            {
                get => _label;
                set
                {
                    if (_label == value) return;
                    _label = value;
                    RaisePropertyChanged(nameof(Label));
                }
            }

            public event EventHandler? SelectionChanged;
        }
        public string Title => "Plot";
        // 是否 Polar 模式(用于 UI 显示控制)
        public bool IsPolar => CurrentPlotType == "Polar" || CurrentPlotType == "PolarFilled";
        private bool _suppressCurveSelectionUpdate = false;
        // 所有可选的曲线 label
        public ObservableCollection<CurveOption> AvailableCurves { get; } = new();
        private bool _isActive;
        public bool IsActive { get => _isActive; set { _isActive = value; RaisePropertyChanged(nameof(IsActive)); } }
        private bool _isClosed;
        public bool IsClosed { get => _isClosed; set { _isClosed = value; RaisePropertyChanged(nameof(IsClosed)); } }
        private string _currentPlotType = "Line";
        public string CurrentPlotType
        {
            get => _currentPlotType;
            set
            {
                _currentPlotType = value;
                RaisePropertyChanged(nameof(CurrentPlotType));
                RaisePropertyChanged(nameof(IsHeatmap));
                RaisePropertyChanged(nameof(IsPolar));    // ← 新加
                RenderRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        private List<DataRecord> _selectedRecords = new();

        private ProcessedData? _currentData;
        public ProcessedData? CurrentData
        {
            get => _currentData;
            private set
            {
                _currentData = value;
                RaisePropertyChanged(nameof(CurrentData));
                RenderRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // 配置(单例式:一份对象,属性变化触发保存 + 重绘)
        public PlotConfig Config { get; } = PlotConfigStore.Load();

        // 暴露给 UI 绑定的属性 - 转发到 Config
        public string FontName
        {
            get => Config.FontName;
            set { Config.FontName = value; OnConfigChanged(nameof(FontName)); }
        }
        public float BaseFontSize
        {
            get => Config.BaseFontSize;
            set { Config.BaseFontSize = value; OnConfigChanged(nameof(BaseFontSize)); }
        }
        public bool ShowFrame
        {
            get => Config.ShowFrame;
            set { Config.ShowFrame = value; OnConfigChanged(nameof(ShowFrame)); }
        }
        public MarginStyle Margin
        {
            get => Config.Margin;
            set { Config.Margin = value; OnConfigChanged(nameof(Margin)); }
        }
        public LegendPos LegendPosition
        {
            get => Config.LegendPosition;
            set { Config.LegendPosition = value; OnConfigChanged(nameof(LegendPosition)); }
        }
        public string XLabelOverride
        {
            get => Config.XLabelOverride;
            set { Config.XLabelOverride = value; OnConfigChanged(nameof(XLabelOverride)); }
        }
        public string YLabelOverride
        {
            get => Config.YLabelOverride;
            set { Config.YLabelOverride = value; OnConfigChanged(nameof(YLabelOverride)); }
        }
        public MarkerStyleKind MarkerStyle
        {
            get => Config.MarkerStyle;
            set { Config.MarkerStyle = value; OnConfigChanged(nameof(MarkerStyle)); }
        }

        public float MarkerSize
        {
            get => Config.MarkerSize;
            set { Config.MarkerSize = value; OnConfigChanged(nameof(MarkerSize)); }
        }

        public bool IsMarkerOpen
        {
            get => MarkerStyle == MarkerStyleKind.OpenCircle;
            set { if (value) MarkerStyle = MarkerStyleKind.OpenCircle; RaiseMarkerChanged(); }
        }
        public bool IsMarkerFilled
        {
            get => MarkerStyle == MarkerStyleKind.FilledCircle;
            set { if (value) MarkerStyle = MarkerStyleKind.FilledCircle; RaiseMarkerChanged(); }
        }
        public bool IsMarkerFilledOutlined
        {
            get => MarkerStyle == MarkerStyleKind.FilledOutlined;
            set { if (value) MarkerStyle = MarkerStyleKind.FilledOutlined; RaiseMarkerChanged(); }
        }
        private void RaiseMarkerChanged()
        {
            RaisePropertyChanged(nameof(IsMarkerOpen));
            RaisePropertyChanged(nameof(IsMarkerFilled));
            RaisePropertyChanged(nameof(IsMarkerFilledOutlined));
        }
        // 便捷:Margin 三个 radio 的 bool
        public bool IsMarginTight
        {
            get => Margin == MarginStyle.Tight;
            set { if (value) Margin = MarginStyle.Tight; RaisePropertyChanged(nameof(IsMarginTight)); RaisePropertyChanged(nameof(IsMarginCommon)); RaisePropertyChanged(nameof(IsMarginLoose)); }
        }
        public bool IsMarginCommon
        {
            get => Margin == MarginStyle.Common;
            set { if (value) Margin = MarginStyle.Common; RaisePropertyChanged(nameof(IsMarginTight)); RaisePropertyChanged(nameof(IsMarginCommon)); RaisePropertyChanged(nameof(IsMarginLoose)); }
        }
        public bool IsMarginLoose
        {
            get => Margin == MarginStyle.Loose;
            set { if (value) Margin = MarginStyle.Loose; RaisePropertyChanged(nameof(IsMarginTight)); RaisePropertyChanged(nameof(IsMarginCommon)); RaisePropertyChanged(nameof(IsMarginLoose)); }
        }

        // Legend 5 个 radio
        public bool IsLegendTopRight
        {
            get => LegendPosition == LegendPos.TopRight;
            set { if (value) LegendPosition = LegendPos.TopRight; RaiseLegendChanged(); }
        }
        public bool IsLegendTopLeft
        {
            get => LegendPosition == LegendPos.TopLeft;
            set { if (value) LegendPosition = LegendPos.TopLeft; RaiseLegendChanged(); }
        }
        public bool IsLegendBottomRight
        {
            get => LegendPosition == LegendPos.BottomRight;
            set { if (value) LegendPosition = LegendPos.BottomRight; RaiseLegendChanged(); }
        }
        public bool IsLegendBottomLeft
        {
            get => LegendPosition == LegendPos.BottomLeft;
            set { if (value) LegendPosition = LegendPos.BottomLeft; RaiseLegendChanged(); }
        }
        public bool IsLegendHidden
        {
            get => LegendPosition == LegendPos.Hidden;
            set { if (value) LegendPosition = LegendPos.Hidden; RaiseLegendChanged(); }
        }
        private void RaiseLegendChanged()
        {
            RaisePropertyChanged(nameof(IsLegendTopRight));
            RaisePropertyChanged(nameof(IsLegendTopLeft));
            RaisePropertyChanged(nameof(IsLegendBottomRight));
            RaisePropertyChanged(nameof(IsLegendBottomLeft));
            RaisePropertyChanged(nameof(IsLegendHidden));
        }

        // 字体 +/- 命令
        public DelegateCommand IncreaseFontCommand { get; }
        public DelegateCommand DecreaseFontCommand { get; }
        public DelegateCommand ExportCsvCommand { get; }
        public DelegateCommand EditLegendCommand { get; }
        // 鼠标坐标 + 状态
        private string _mouseCoordinate = "";
        public string MouseCoordinate { get => _mouseCoordinate; set { _mouseCoordinate = value; RaisePropertyChanged(nameof(MouseCoordinate)); } }

        private string _statusMessage = "Select records and click 'Line Plot' from the Plot ribbon";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; RaisePropertyChanged(nameof(StatusMessage)); } }

        public event EventHandler? RenderRequested;

        public PlotModuleViewModel()
        {
            IncreaseFontCommand = new DelegateCommand(() => { BaseFontSize += 1f; });
            DecreaseFontCommand = new DelegateCommand(() => { BaseFontSize -= 1f; });
            ExportCsvCommand = new DelegateCommand(ExecuteExportCsv);
            EditLegendCommand = new DelegateCommand(ExecuteEditLegend);
            Messenger.Default.Register<SelectedRecordsChangedMessage>(this, msg => { _selectedRecords = msg.Records; });
            Messenger.Default.Register<RequestLinePlotMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "Line";
                await GenerateLinePlotAsync();
            });
            Messenger.Default.Register<RequestScatterPlotMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "Scatter";
                await GenerateLinePlotAsync();   // 复用现有的处理方法(阶段 1 简单实现)
            });
            Messenger.Default.Register<RequestHeatmapMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "Heatmap";
                await GenerateHeatmapAsync();
            });
            Messenger.Default.Register<RequestPolarPlotMessage>(this, async msg =>
            {
                System.Diagnostics.Debug.WriteLine($"Polar msg: {msg.Records.Count} records, WithFill={msg.WithFill}");
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = msg.WithFill ? "PolarFilled" : "Polar";
                System.Diagnostics.Debug.WriteLine($"CurrentPlotType set to {CurrentPlotType}");

                await GenerateLinePlotAsync();
            });
            Messenger.Default.Register<PumpScaleChangedMessage>(this, async _ =>
            {
                // 只在 Polar/PolarFilled/Heatmap 模式下,且有数据时,才需要重跑
                if (_selectedRecords.Count == 0) return;
                await RegenerateAsync();
            });
            Messenger.Default.Register<RequestPolarHeatmapMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "PolarHeatmap";
                await GenerateHeatmapAsync();   // 完全复用 heatmap 的字段选择逻辑
            });
            Messenger.Default.Register<RequestContourMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "Contour";
                await GenerateHeatmapAsync();   // 完全复用
            });
            Messenger.Default.Register<RequestPolarContourMessage>(this, async msg =>
            {
                if (msg.Records.Count > 0) _selectedRecords = msg.Records;
                CurrentPlotType = "PolarContour";
                await GenerateHeatmapAsync();
            });
        }
        private void ExecuteExportCsv()
        {
            if (CurrentData == null)
            {
                System.Windows.MessageBox.Show("No data to export.", "Export");
                return;
            }

            var defaultName = $"{CurrentPlotType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var dialog = new SaveFileDialog
            {
                FileName = defaultName,
                DefaultExt = ".csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                CsvExporter.Export(dialog.FileName, CurrentData, CurrentPlotType, _selectedRecords.Count);
                StatusMessage = $"Exported to {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Export Error");
            }
        }
        private void ExecuteEditLegend()
        {
            if (CurrentData == null || CurrentData.Curves.Count == 0)
            {
                System.Windows.MessageBox.Show("No curves to edit. Generate a plot first.", "Edit Legend");
                return;
            }

            var dialog = new Views.EditLegendDialog(CurrentData.Curves);
            if (dialog.ShowDialog() == true)
            {
                // 同步 AvailableCurves 的 Label 和 SelectedLabels(按位置对应,因为 dialog 是按顺序改的)
                _suppressCurveSelectionUpdate = true;
                for (int i = 0; i < CurrentData.Curves.Count && i < AvailableCurves.Count; i++)
                {
                    AvailableCurves[i].Label = CurrentData.Curves[i].Label;
                }
                // 用新 Label 重建 SelectedLabels
                CurrentData.SelectedLabels = new HashSet<string>(
                    AvailableCurves.Where(c => c.IsSelected).Select(c => c.Label));
                _suppressCurveSelectionUpdate = false;

                RenderRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        private async Task GenerateHeatmapAsync()
        {
            if (_selectedRecords.Count == 0)
            {
                StatusMessage = "No records selected.";
                CurrentData = null;
                return;
            }

            try
            {
                StatusMessage = $"Loading fields...";
                var available = await FieldOptionsService.GetAvailableFieldsAsync(_selectedRecords);

                _suppressRegenerate = true;
                AvailableFields.Clear();
                foreach (var f in available.Fields) AvailableFields.Add(f);
                _selectedXField = null;
                //_selectedXField = AvailableFields.FirstOrDefault(f => f.FieldName == available.DefaultXFieldName)
                //                 ?? AvailableFields.FirstOrDefault();
                // Y 和 Z 用 HeatmapProcessor 默认判断
                // 这里先不设,让 processor 内部决定
                _selectedYField = null;
                _selectedZField = null;
                RaisePropertyChanged(nameof(SelectedXField));
                RaisePropertyChanged(nameof(SelectedYField));
                RaisePropertyChanged(nameof(SelectedZField));
                _suppressRegenerate = false;

                await RegenerateAsync();

                // 生成后,ProcessedData 里有 ZMin/ZMax,同步到 slider 范围
                if (CurrentData?.ZMin.HasValue == true && CurrentData?.ZMax.HasValue == true)
                {
                    ZRangeMin = CurrentData.ZMin.Value;
                    ZRangeMax = CurrentData.ZMax.Value;
                    // 首次或字段变化时,重置为自然范围
                    Config.ZMinOverride = null;
                    Config.ZMaxOverride = null;
                    RaisePropertyChanged(nameof(ZMinValue));
                    RaisePropertyChanged(nameof(ZMaxValue));

                    // 从 processor 结果里回填 Y/Z 的默认字段
                    var yField = AvailableFields.FirstOrDefault(f =>
                        f.DisplayLabel == CurrentData.YLabel ||
                        CurrentData.YLabel.Contains(f.DisplayLabel));
                    var zField = AvailableFields.FirstOrDefault(f =>
                        f.DisplayLabel == CurrentData.ZLabel ||
                        CurrentData.ZLabel.Contains(f.DisplayLabel));
                    _selectedYField = yField;
                    _selectedZField = zField;
                    RaisePropertyChanged(nameof(SelectedYField));
                    RaisePropertyChanged(nameof(SelectedZField));
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Plot Error");
                StatusMessage = $"Error: {ex.Message}";
                CurrentData = null;
            }
        }
        private void OnConfigChanged(string propertyName)
        {
            RaisePropertyChanged(propertyName);
            PlotConfigStore.Save(Config);        // 每次改动即时保存
            RenderRequested?.Invoke(this, EventArgs.Empty);
        }
        public ObservableCollection<FieldOption> AvailableFields { get; } = new();

        private FieldOption? _selectedXField;
        public FieldOption? SelectedXField
        {
            get => _selectedXField;
            set
            {
                if (_selectedXField == value) return;
                _selectedXField = value;
                RaisePropertyChanged(nameof(SelectedXField));

                // ← 加这两行:清 override,让新数据的 XLabel 生效
                Config.XLabelOverride = "";
                RaisePropertyChanged(nameof(XLabelOverride));

                _ = RegenerateAsync();
            }
        }

        private FieldOption? _selectedYField;
        public FieldOption? SelectedYField
        {
            get => _selectedYField;
            set
            {
                if (_selectedYField == value) return;
                _selectedYField = value;
                RaisePropertyChanged(nameof(SelectedYField));

                Config.YLabelOverride = "";
                RaisePropertyChanged(nameof(YLabelOverride));

                _ = RegenerateAsync();
            }
        }
        private async Task GenerateLinePlotAsync()
        {
            if (_selectedRecords.Count == 0)
            {
                StatusMessage = "No records selected.";
                CurrentData = null;
                return;
            }

            try
            {
                StatusMessage = $"Loading fields...";

                // 1. 拉可用字段
                var available = await FieldOptionsService.GetAvailableFieldsAsync(_selectedRecords);

                // 2. 更新 UI 下拉源(不触发重画,用私有字段)
                _suppressRegenerate = true;
                AvailableFields.Clear();
                foreach (var f in available.Fields) AvailableFields.Add(f);

                _selectedXField = AvailableFields.FirstOrDefault(f => f.FieldName == available.DefaultXFieldName)
                                 ?? AvailableFields.FirstOrDefault();
                _selectedYField = AvailableFields.FirstOrDefault(f => f.FieldName == available.DefaultYFieldName)
                                 ?? AvailableFields.FirstOrDefault();
                RaisePropertyChanged(nameof(SelectedXField));
                RaisePropertyChanged(nameof(SelectedYField));
                _suppressRegenerate = false;

                // 3. 生成图
                await RegenerateAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Plot Error");
                StatusMessage = $"Error: {ex.Message}";
                CurrentData = null;
            }
        }

        private bool _suppressRegenerate = false;

        private async Task RegenerateAsync()
        {
            if (_suppressRegenerate) return;
            if (_selectedRecords.Count == 0) return;

            PlotKind kind;
            if (CurrentPlotType == "Heatmap") kind = PlotKind.Heatmap;
            else if (CurrentPlotType == "PolarHeatmap") kind = PlotKind.PolarHeatmap;
            else if (CurrentPlotType == "Contour") kind = PlotKind.Contour;           // 新
            else if (CurrentPlotType == "PolarContour") kind = PlotKind.PolarContour; // 新
            else if (CurrentPlotType == "Polar") kind = PlotKind.Polar;
            else if (CurrentPlotType == "PolarFilled") kind = PlotKind.PolarFilled;
            else kind = PlotKind.LineOrScatter;

            var ctx = new ProcessingContext
            {
                Records = _selectedRecords,
                XAxisFieldName = SelectedXField?.FieldName,
                YAxisFieldName = SelectedYField?.FieldName,
                ZAxisFieldName = SelectedZField?.FieldName,
            };

            IDataProcessor processor;
            try
            {
                processor = await PlotDispatcher.ResolveProcessorAsync(_selectedRecords, kind, ctx);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Plot Error");
                StatusMessage = $"Error: {ex.Message}";
                CurrentData = null;
                return;
            }

            bool needProgress = processor.SourceType == "SHGImage_AngleMap";

            var vm = new DXSplashScreenViewModel()
            {
                IsIndeterminate = false,
                Progress = 0,
                Status = "Preparing..."
            };
            var splash = needProgress ? SplashScreenManager.CreateWaitIndicator(vm) : null;

            try
            {
                StatusMessage = $"Processing {_selectedRecords.Count} records...";

                splash?.Show();

                if (needProgress)
                {
                    ctx.Progress = new Progress<(int current, int total, string status)>(p =>
                    {
                        vm.Progress = 100.0 * p.current / p.total;
                        vm.Status =
                            $"{p.current}/{p.total} ({100.0 * p.current / p.total:F1}%)\n{p.status}";
                    });
                }

                var data = await processor.ProcessAsync(ctx);

                splash?.Close();
                splash = null;

                CurrentData = data;
                StatusMessage = $"Rendered {_selectedRecords.Count} records ({processor.SourceType})";
                if ((CurrentPlotType == "Heatmap" || CurrentPlotType == "PolarHeatmap" || CurrentPlotType == "Contour" || CurrentPlotType == "PolarContour") && data.ZMin.HasValue && data.ZMax.HasValue)
                {
                    ZRangeMin = data.ZMin.Value;
                    ZRangeMax = data.ZMax.Value;
                    Config.ZMinOverride = null;
                    Config.ZMaxOverride = null;
                    RaisePropertyChanged(nameof(ZMinValue));
                    RaisePropertyChanged(nameof(ZMaxValue));
                }
                // 更新曲线 label 下拉(仅 Polar 模式)
                if (IsPolar)
                {
                    _suppressCurveSelectionUpdate = true;
                    foreach (var old in AvailableCurves)
                        old.SelectionChanged -= OnCurveSelectionChanged;
                    AvailableCurves.Clear();
                    foreach (var c in data.Curves)
                    {
                        bool isAveraged = c.Label != null && c.Label.StartsWith("averaged", System.StringComparison.OrdinalIgnoreCase);
                        var option = new CurveOption { Label = c.Label, IsSelected = isAveraged };
                        option.SelectionChanged += OnCurveSelectionChanged;
                        AvailableCurves.Add(option);
                    }
                    CurrentData.SelectedLabels = new HashSet<string>(
                        data.Curves.Where(c => c.Label != null && c.Label.StartsWith("averaged", System.StringComparison.OrdinalIgnoreCase))
                                   .Select(c => c.Label));
                    _suppressCurveSelectionUpdate = false;
                }
                else
                {
                    _suppressCurveSelectionUpdate = true;
                    foreach (var old in AvailableCurves)
                        old.SelectionChanged -= OnCurveSelectionChanged;
                    AvailableCurves.Clear();
                    _suppressCurveSelectionUpdate = false;
                }
            }
            catch (Exception ex)
            {
                splash?.Close();
                System.Windows.MessageBox.Show(ex.Message, "Plot Error");
                StatusMessage = $"Error: {ex.Message}";
                CurrentData = null;
            }
        }
        // 已有的:private string _currentPlotType = "Line";
        // 修改设置逻辑,让 UI 能区分是否 Heatmap
        public bool IsHeatmap => _currentPlotType == "Heatmap" || _currentPlotType == "PolarHeatmap" || CurrentPlotType == "Contour" || CurrentPlotType == "PolarContour";

        private FieldOption? _selectedZField;
        public FieldOption? SelectedZField
        {
            get => _selectedZField;
            set
            {
                if (_selectedZField != value)
                {
                    _selectedZField = value;
                    RaisePropertyChanged(nameof(SelectedZField));
                    _ = RegenerateAsync();
                }
            }
        }
        public ColormapKind Colormap
        {
            get => Config.Colormap;
            set { Config.Colormap = value; OnConfigChanged(nameof(Colormap)); }
        }
        private void OnCurveSelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressCurveSelectionUpdate) return;
            if (CurrentData == null) return;

            CurrentData.SelectedLabels = new HashSet<string>(
                AvailableCurves.Where(c => c.IsSelected).Select(c => c.Label));
            RenderRequested?.Invoke(this, EventArgs.Empty);
        }
        // 提供可选项
        public IEnumerable<ColormapKind> AvailableColormaps { get; } =
            System.Enum.GetValues<ColormapKind>();
        // 数据的自然范围(用于 slider 的 Min/Max 边界)
        private double _zRangeMin;
        public double ZRangeMin
        {
            get => _zRangeMin;
            private set { _zRangeMin = value; RaisePropertyChanged(nameof(ZRangeMin)); }
        }

        private double _zRangeMax;
        public double ZRangeMax
        {
            get => _zRangeMax;
            private set { _zRangeMax = value; RaisePropertyChanged(nameof(ZRangeMax)); }
        }

        // 用户选中的范围(slider 值)
        public double ZMinValue
        {
            get => Config.ZMinOverride ?? ZRangeMin;
            set { Config.ZMinOverride = value; OnConfigChanged(nameof(ZMinValue)); }
        }

        public double ZMaxValue
        {
            get => Config.ZMaxOverride ?? ZRangeMax;
            set { Config.ZMaxOverride = value; OnConfigChanged(nameof(ZMaxValue)); }
        }
    }
}