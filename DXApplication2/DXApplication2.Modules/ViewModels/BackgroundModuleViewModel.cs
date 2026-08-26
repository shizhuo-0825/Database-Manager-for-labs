using DevExpress.Mvvm;
using DXApplication2.Common.Background;
using DXApplication2.Common.Data;
using DXApplication2.Common.IO;
using DXApplication2.Common.Messages;
using DXApplication2.Common.Models;
using DXApplication2.Common.Plotting;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Processing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DXApplication2.Modules.ViewModels
{
    public enum BackgroundViewMode
    {
        None,
        Image,
        Pump
    }

    public class BackgroundModuleViewModel : ViewModelBase
    {
        public string Title => "Background";

        private bool _isActive;
        public bool IsActive { get => _isActive; set { _isActive = value; RaisePropertyChanged(nameof(IsActive)); } }
        private bool _isClosed;
        public bool IsClosed { get => _isClosed; set { _isClosed = value; RaisePropertyChanged(nameof(IsClosed)); } }

        public BackgroundConfig Config { get; private set; } = BackgroundStore.Load();
        public double PumpScale
        {
            get => Config.PumpScale;
            set
            {
                if (System.Math.Abs(Config.PumpScale - value) < 1e-9) return;
                Config.PumpScale = value;
                RaisePropertyChanged(nameof(PumpScale));
                BackgroundStore.Save(Config);
                // 广播:让 Plot 收到重画
                Messenger.Default.Send(new PumpScaleChangedMessage { Scale = value });
            }
        }
        // ========== 模式 ==========
        private BackgroundViewMode _mode = BackgroundViewMode.None;
        public BackgroundViewMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                RaisePropertyChanged(nameof(Mode));
                RaisePropertyChanged(nameof(IsNoneMode));
                RaisePropertyChanged(nameof(IsImageMode));
                RaisePropertyChanged(nameof(IsPumpMode));
                _ = RefreshDisplayAsync();
            }
        }

        public bool IsNoneMode
        {
            get => Mode == BackgroundViewMode.None;
            set { if (value) Mode = BackgroundViewMode.None; }
        }
        public bool IsImageMode
        {
            get => Mode == BackgroundViewMode.Image;
            set { if (value) Mode = BackgroundViewMode.Image; }
        }
        public bool IsPumpMode
        {
            get => Mode == BackgroundViewMode.Pump;
            set { if (value) Mode = BackgroundViewMode.Pump; }
        }

        // ========== Apply to PSHG raw ==========
        public bool ApplyToPshgRaw
        {
            get => Config.ApplyToPshgRaw;
            set
            {
                Config.ApplyToPshgRaw = value;
                RaisePropertyChanged(nameof(ApplyToPshgRaw));
                BackgroundStore.Save(Config);
            }
        }

        // ========== Image 相关 ==========
        public string ImageInfo
        {
            get
            {
                var i = Config.Image;
                if (i == null) return "No image background set.";
                return $"Record #{i.RecordId}: {i.RecordName}\nSet: {i.SetTimestamp}";
            }
        }

        public DelegateCommand ClearImageCommand { get; }

        // ========== Pump 相关 ==========
        public string PumpInfo
        {
            get
            {
                var p = Config.Pump;
                if (p == null) return "No pump background set.";

                string fitStatus;
                if (!p.IsFitted)
                {
                    fitStatus = "Not fitted yet.";
                }
                else
                {
                    var maxBk = p.FitBk.Length > 0 ? p.FitBk.Max() : 0;
                    var mainK = p.FitBk.Length > 0
                        ? System.Array.IndexOf(p.FitBk, maxBk) + 1
                        : 0;
                    fitStatus = $"Fitted: A={p.FitA:G4}, main k={mainK} (Bk={maxBk:G4})";
                }

                return $"Group #{p.GroupId}: {p.GroupName} ({p.RecordIds.Count} records)\nSet: {p.SetTimestamp}\n{fitStatus}";
            }
        }

        public DelegateCommand ClearPumpCommand { get; }
        public DelegateCommand FitPumpCommand { get; }

        // ========== 显示数据 ==========
        private double[,]? _imageMatrix;
        public double[,]? ImageMatrix
        {
            get => _imageMatrix;
            private set { _imageMatrix = value; RaisePropertyChanged(nameof(ImageMatrix)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        private ProcessedData? _pumpData;
        public ProcessedData? PumpData
        {
            get => _pumpData;
            private set { _pumpData = value; RaisePropertyChanged(nameof(PumpData)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        // Colormap 等(用于 Image 显示)
        public ColormapKind Colormap { get => _cm; set { _cm = value; RaisePropertyChanged(nameof(Colormap)); RenderRequested?.Invoke(this, EventArgs.Empty); } }
        private ColormapKind _cm = ColormapKind.Magma;

        public IEnumerable<ColormapKind> AvailableColormaps { get; } = Enum.GetValues<ColormapKind>();

        private double _zRangeMin;
        public double ZRangeMin { get => _zRangeMin; private set { _zRangeMin = value; RaisePropertyChanged(nameof(ZRangeMin)); } }

        private double _zRangeMax = 1;
        public double ZRangeMax { get => _zRangeMax; private set { _zRangeMax = value; RaisePropertyChanged(nameof(ZRangeMax)); } }

        private double? _zMin, _zMax;
        public double ZMinValue { get => _zMin ?? ZRangeMin; set { _zMin = value; RaisePropertyChanged(nameof(ZMinValue)); RenderRequested?.Invoke(this, EventArgs.Empty); } }
        public double ZMaxValue { get => _zMax ?? ZRangeMax; set { _zMax = value; RaisePropertyChanged(nameof(ZMaxValue)); RenderRequested?.Invoke(this, EventArgs.Empty); } }

        // ========== 状态 ==========
        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; RaisePropertyChanged(nameof(StatusMessage)); } }

        public event EventHandler? RenderRequested;
        private double[]? _fitCurveTheta;
        public double[]? FitCurveTheta 
        { 
            get => _fitCurveTheta; 
            private set { _fitCurveTheta = value; RaisePropertyChanged(nameof(FitCurveTheta)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        private double[]? _fitCurveY;
        public double[]? FitCurveY 
        { 
            get => _fitCurveY; 
            private set { _fitCurveY = value; RaisePropertyChanged(nameof(FitCurveY)); }
        }
        public BackgroundModuleViewModel()
        {
            ClearImageCommand = new DelegateCommand(ExecuteClearImage);
            ClearPumpCommand = new DelegateCommand(ExecuteClearPump);
            FitPumpCommand = new DelegateCommand(async () => await ExecuteFitPumpAsync());
            // 拟合曲线数据(用于叠加在 polar plot 上)

            Messenger.Default.Register<RequestBackgroundMessage>(this, async _ =>
            {
                await RefreshDisplayAsync();
            });

            Messenger.Default.Register<SetImageBackgroundMessage>(this, async msg =>
            {
                if (msg.Record == null) return;
                Config.Image = new ImageBackgroundInfo
                {
                    RecordId = msg.Record.Id,
                    RecordName = msg.Record.RecordName ?? "",
                    ImageFilePath = msg.Record.ImageFilePath ?? "",
                    SetTimestamp = BackgroundStore.GenerateTimestamp()
                };
                BackgroundStore.Save(Config);
                RaisePropertyChanged(nameof(ImageInfo));
                Mode = BackgroundViewMode.Image;
                await RefreshDisplayAsync();
                StatusMessage = "Image background set.";
            });

            Messenger.Default.Register<SetPumpBackgroundMessage>(this, async msg =>
            {
                if (msg.Records.Count == 0) return;
                Config.Pump = new PumpBackgroundInfo
                {
                    RecordIds = msg.Records.Select(r => r.Id).ToList(),
                    GroupId = msg.GroupId,
                    GroupName = msg.GroupName,
                    SetTimestamp = BackgroundStore.GenerateTimestamp(),
                    IsFitted = false
                };
                BackgroundStore.Save(Config);
                RaisePropertyChanged(nameof(PumpInfo));
                Mode = BackgroundViewMode.Pump;
                await RefreshDisplayAsync();
                StatusMessage = "Pump background set (not yet fitted).";
            });

            // 载入时刷新
            _ = RefreshDisplayAsync();
        }

        private async Task RefreshDisplayAsync()
        {
            try
            {
                if (Mode == BackgroundViewMode.Image && Config.Image != null)
                {
                    var path = Config.Image.ImageFilePath;
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    {
                        StatusMessage = "Image file not found.";
                        ImageMatrix = null;
                        return;
                    }
                    var raw = await AscMatrixReader.ReadAsync(path);
                    var preprocConfig = PreprocessingConfigStore.Load();
                    var processed = PreprocessingService.Apply(raw, preprocConfig);
                    var (min, max) = AscMatrixReader.GetRange(processed);
                    ZRangeMin = min; ZRangeMax = max;
                    _zMin = null; _zMax = null;
                    RaisePropertyChanged(nameof(ZMinValue));
                    RaisePropertyChanged(nameof(ZMaxValue));
                    ImageMatrix = processed;
                    StatusMessage = $"Loaded image background: {processed.GetLength(0)} × {processed.GetLength(1)} pixels";
                }
                else if (Mode == BackgroundViewMode.Pump && Config.Pump != null)
                {
                    // 显示 pump records 的 polar plot(尚未拟合,只显示原始数据)
                    var recordIds = Config.Pump.RecordIds;
                    using var db = new AppDbContext();
                    var records = await db.DataRecords
                        .Where(r => recordIds.Contains(r.Id))
                        .ToListAsync();

                    if (records.Count == 0)
                    {
                        StatusMessage = "Pump records not found in database.";
                        PumpData = null;
                        return;
                    }

                    // 用 PolarPlotProcessor 处理(需要 ResultQuantity 已算出)
                    var ctx = new ProcessingContext { Records = records };
                    var proc = new PolarPlotProcessor();
                    var data = await proc.ProcessAsync(ctx);
                    PumpData = data;
                    // 如果已经拟合过,重构叠加曲线
                    if (Config.Pump.IsFitted)
                    {
                        var fit = new PumpFittingService.FitResult
                        {
                            Success = true,
                            A = Config.Pump.FitA,
                            Bk = Config.Pump.FitBk,
                            Phik = Config.Pump.FitPhik
                        };
                        var (thetas, ys) = PumpFittingService.Reconstruct(fit);
                        FitCurveTheta = thetas;
                        FitCurveY = ys;
                    }
                    else
                    {
                        FitCurveTheta = null;
                        FitCurveY = null;
                    }
                    StatusMessage = $"Loaded pump background: {records.Count} records";
                }
                else
                {
                    ImageMatrix = null;
                    PumpData = null;
                    StatusMessage = "Nothing to display.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ImageMatrix = null;
                PumpData = null;
            }
        }
        private async Task ExecuteFitPumpAsync()
        {
            if (Config.Pump == null || Config.Pump.RecordIds.Count == 0)
            {
                System.Windows.MessageBox.Show("No pump background set.", "Fit");
                return;
            }

            try
            {
                StatusMessage = "Fitting...";
                var fit = await PumpFittingService.FitAsync(Config.Pump.RecordIds);
                if (!fit.Success)
                {
                    System.Windows.MessageBox.Show(fit.WarningMessage ?? "Fit failed.", "Fit");
                    StatusMessage = "Fit failed.";
                    return;
                }

                // 保存拟合参数
                Config.Pump.IsFitted = true;
                Config.Pump.FitA = fit.A;
                Config.Pump.FitBk = fit.Bk;
                Config.Pump.FitPhik = fit.Phik;
                BackgroundStore.Save(Config);
                RaisePropertyChanged(nameof(PumpInfo));

                // 重构平滑曲线,供叠加渲染
                var (thetas, ys) = PumpFittingService.Reconstruct(fit);
                FitCurveTheta = thetas;
                FitCurveY = ys;

                StatusMessage = fit.WarningMessage != null
                    ? $"Fit done. {fit.WarningMessage}"
                    : $"Fit done: A={fit.A:G4}, max Bk={fit.Bk.Max():G4}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Fit Error");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        private void ExecuteClearImage()
        {
            var result = System.Windows.MessageBox.Show(
                "Clear image background?", "Confirm",
                System.Windows.MessageBoxButton.YesNo);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            Config.Image = null;
            BackgroundStore.Save(Config);
            RaisePropertyChanged(nameof(ImageInfo));
            ImageMatrix = null;
            StatusMessage = "Image background cleared.";
        }

        private void ExecuteClearPump()
        {
            var result = System.Windows.MessageBox.Show(
                "Clear pump background?", "Confirm",
                System.Windows.MessageBoxButton.YesNo);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            Config.Pump = null;
            BackgroundStore.Save(Config);
            RaisePropertyChanged(nameof(PumpInfo));
            PumpData = null;
            FitCurveTheta = null;   // ← 加这行
            FitCurveY = null;       // ← 加这行
            StatusMessage = "Pump background cleared.";
        }
    }
}