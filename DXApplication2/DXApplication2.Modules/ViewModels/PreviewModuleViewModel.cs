using DevExpress.Mvvm;
using DXApplication2.Common.IO;
using DXApplication2.Common.Messages;
using DXApplication2.Common.Models;
using DXApplication2.Common.Plotting;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Roi;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DXApplication2.Modules.ViewModels
{

    public class PreviewModuleViewModel : ViewModelBase
    {
        public string Title => "Preview";

        private bool _isActive;
        public bool IsActive { get => _isActive; set { _isActive = value; RaisePropertyChanged(nameof(IsActive)); } }
        private bool _isClosed;
        public bool IsClosed { get => _isClosed; set { _isClosed = value; RaisePropertyChanged(nameof(IsClosed)); } }

        // 当前 Record 和 Matrix
        private DataRecord? _currentRecord;
        public DataRecord? CurrentRecord
        {
            get => _currentRecord;
            private set { _currentRecord = value; RaisePropertyChanged(nameof(CurrentRecord)); RaisePropertyChanged(nameof(HeaderText)); }
        }

        private double[,]? _rawMatrix;
        private double[,]? _matrix;
        public double[,]? Matrix
        {
            get => _matrix;
            private set
            {
                _matrix = value;
                RaisePropertyChanged(nameof(Matrix));
                RenderRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public string HeaderText =>
            CurrentRecord == null
                ? "No record selected. Select a record and click Preview from the ribbon."
                : $"Preview: Record #{CurrentRecord.Id} — {System.IO.Path.GetFileName(CurrentRecord.ImageFilePath)}";

        // 三种模式(Radio)
        private PreviewMode _mode = PreviewMode.Raw;
        public PreviewMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    RaisePropertyChanged(nameof(Mode));
                    RaisePropertyChanged(nameof(IsRawMode));
                    RaisePropertyChanged(nameof(IsPreprocessedMode));
                    RaisePropertyChanged(nameof(IsSelectRoiMode));
                    UpdateDisplayMatrix();
                }
            }
        }

        public bool IsRawMode
        {
            get => Mode == PreviewMode.Raw;
            set { if (value) Mode = PreviewMode.Raw; }
        }
        public bool IsPreprocessedMode
        {
            get => Mode == PreviewMode.Preprocessed;
            set { if (value) Mode = PreviewMode.Preprocessed; }
        }
        public bool IsSelectRoiMode
        {
            get => Mode == PreviewMode.SelectRoi;
            set { if (value) Mode = PreviewMode.SelectRoi; }
        }

        // Colormap
        public ColormapKind Colormap
        {
            get => _colormap;
            set { _colormap = value; RaisePropertyChanged(nameof(Colormap)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }
        private ColormapKind _colormap = ColormapKind.Magma;

        public System.Collections.Generic.IEnumerable<ColormapKind> AvailableColormaps { get; } =
            System.Enum.GetValues<ColormapKind>();

        // Z 范围
        private double _zRangeMin;
        public double ZRangeMin
        {
            get => _zRangeMin;
            private set { _zRangeMin = value; RaisePropertyChanged(nameof(ZRangeMin)); }
        }

        private double _zRangeMax = 1;
        public double ZRangeMax
        {
            get => _zRangeMax;
            private set { _zRangeMax = value; RaisePropertyChanged(nameof(ZRangeMax)); }
        }

        private double? _zMinOverride;
        public double ZMinValue
        {
            get => _zMinOverride ?? ZRangeMin;
            set { _zMinOverride = value; RaisePropertyChanged(nameof(ZMinValue)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        private double? _zMaxOverride;
        public double ZMaxValue
        {
            get => _zMaxOverride ?? ZRangeMax;
            set { _zMaxOverride = value; RaisePropertyChanged(nameof(ZMaxValue)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        // Preprocessing 配置
        public PreprocessingConfig PreprocConfig { get; } = PreprocessingConfigStore.Load();

        public double HotspotSize
        {
            get => PreprocConfig.HotspotSize;
            set { PreprocConfig.HotspotSize = (int)value; RaisePropertyChanged(nameof(HotspotSize)); }
        }
        public double HotspotRatio
        {
            get => PreprocConfig.HotspotRatio;
            set { PreprocConfig.HotspotRatio = value; RaisePropertyChanged(nameof(HotspotRatio)); }
        }
        public double HardThreshold
        {
            get => PreprocConfig.HardThreshold ?? 0;
            set { PreprocConfig.HardThreshold = value > 0 ? value : null; RaisePropertyChanged(nameof(HardThreshold)); }
        }
        public double GaussianSigma
        {
            get => PreprocConfig.GaussianSigma;
            set { PreprocConfig.GaussianSigma = value; RaisePropertyChanged(nameof(GaussianSigma)); }
        }

        public DelegateCommand UpdatePreprocessingCommand { get; }

        // ========== ROI 相关 ==========
        public System.Collections.Generic.IReadOnlyList<string> AvailableTags => RoiTags.All;

        private string _selectedTag = RoiTags.PSHG;
        public string SelectedTag
        {
            get => _selectedTag;
            set
            {
                if (_selectedTag != value)
                {
                    _selectedTag = value;
                    RaisePropertyChanged(nameof(SelectedTag));
                    RefreshTimestamps();
                    ActiveRoiState.Set(value, SelectedTimestamp);
                }
            }
        }

        public ObservableCollection<string> AvailableTimestamps { get; } = new();

        private string? _selectedTimestamp;
        public string? SelectedTimestamp
        {
            get => _selectedTimestamp;
            set
            {
                if (_selectedTimestamp != value)
                {
                    _selectedTimestamp = value;
                    RaisePropertyChanged(nameof(SelectedTimestamp));
                    RaisePropertyChanged(nameof(CurrentRoi));
                    ActiveRoiState.Set(SelectedTag, value);
                    RenderRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _showRoi;
        public bool ShowRoi
        {
            get => _showRoi;
            set { _showRoi = value; RaisePropertyChanged(nameof(ShowRoi)); RenderRequested?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>当前应显示的 ROI(view 层用)</summary>
        public RoiRect? CurrentRoi
        {
            get
            {
                if (!ShowRoi) return null;
                if (string.IsNullOrEmpty(SelectedTag) || string.IsNullOrEmpty(SelectedTimestamp)) return null;
                return RoiStore.Load(SelectedTag, SelectedTimestamp);
            }
        }

        public DelegateCommand SelectRoiCommand { get; }
        public DelegateCommand DeleteRoiCommand { get; }

        // 状态
        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; RaisePropertyChanged(nameof(StatusMessage)); } }

        public event EventHandler? RenderRequested;

        public PreviewModuleViewModel()
        {
            UpdatePreprocessingCommand = new DelegateCommand(ExecuteUpdatePreprocessing);
            SelectRoiCommand = new DelegateCommand(ExecuteSelectRoi);
            DeleteRoiCommand = new DelegateCommand(ExecuteDeleteRoi);

            Messenger.Default.Register<RequestPreviewMessage>(this, async msg =>
            {
                await LoadPreviewAsync(msg.Record, msg.InitialMode);
            });
            RefreshTimestamps();
            ActiveRoiState.Set(SelectedTag, SelectedTimestamp);
        }

        private void RefreshTimestamps()
        {
            AvailableTimestamps.Clear();
            var timestamps = RoiStore.ListTimestamps(SelectedTag);
            foreach (var ts in timestamps) AvailableTimestamps.Add(ts);

            SelectedTimestamp = AvailableTimestamps.FirstOrDefault();
            RaisePropertyChanged(nameof(CurrentRoi));
            RenderRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task LoadPreviewAsync(DataRecord? record, PreviewMode initialMode)
        {
            if (record == null)
            {
                CurrentRecord = null;
                _rawMatrix = null;
                Matrix = null;
                StatusMessage = "No record.";
                return;
            }

            CurrentRecord = record;

            if (string.IsNullOrEmpty(record.ImageFilePath))
            {
                StatusMessage = "Record has no ImageFilePath.";
                _rawMatrix = null;
                Matrix = null;
                return;
            }

            try
            {
                StatusMessage = "Loading...";
                _rawMatrix = await AscMatrixReader.ReadAsync(record.ImageFilePath);

                UpdateDisplayMatrix();

                StatusMessage = $"Loaded {_rawMatrix.GetLength(0)} × {_rawMatrix.GetLength(1)} pixels";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Preview Error");
                StatusMessage = $"Error: {ex.Message}";
                _rawMatrix = null;
                Matrix = null;
            }
            Mode = (PreviewMode)initialMode;
        }

        private void UpdateDisplayMatrix()
        {
            if (_rawMatrix == null)
            {
                Matrix = null;
                return;
            }

            double[,] display;

            // Raw 模式显示原图,其他两个模式(Preprocessed / SelectRoi)显示预处理后的
            if (Mode == PreviewMode.Raw)
            {
                display = _rawMatrix;
            }
            else
            {
                try
                {
                    display = PreprocessingService.Apply(_rawMatrix, PreprocConfig);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Preprocessing Error");
                    display = _rawMatrix;
                }
            }

            var (min, max) = AscMatrixReader.GetRange(display);
            ZRangeMin = min;
            ZRangeMax = max;
            _zMinOverride = null;
            _zMaxOverride = null;
            RaisePropertyChanged(nameof(ZMinValue));
            RaisePropertyChanged(nameof(ZMaxValue));

            Matrix = display;
        }

        private void ExecuteUpdatePreprocessing()
        {
            PreprocessingConfigStore.Save(PreprocConfig);
            if (Mode == PreviewMode.Preprocessed || Mode == PreviewMode.SelectRoi)
            {
                UpdateDisplayMatrix();
            }
            StatusMessage = "Preprocessing config updated.";
        }

        private void ExecuteSelectRoi()
        {
            if (Matrix == null)
            {
                System.Windows.MessageBox.Show("No image loaded.", "Select ROI");
                return;
            }
            if (string.IsNullOrEmpty(SelectedTag))
            {
                System.Windows.MessageBox.Show("Select a tag first.", "Select ROI");
                return;
            }

            var roi = OpenCvRoiPicker.PickRoi(Matrix, $"Select {SelectedTag} ROI (Enter=confirm, ESC=cancel)");
            if (roi == null)
            {
                StatusMessage = "ROI selection cancelled.";
                return;
            }

            roi.Tag = SelectedTag;
            roi.Timestamp = RoiStore.GenerateTimestamp();
            RoiStore.Save(roi);

            RefreshTimestamps();
            SelectedTimestamp = roi.Timestamp;   // 自动选中新的

            StatusMessage = $"Saved ROI: {roi.Tag} @ {roi.Timestamp}";
        }

        private void ExecuteDeleteRoi()
        {
            if (string.IsNullOrEmpty(SelectedTag) || string.IsNullOrEmpty(SelectedTimestamp))
            {
                System.Windows.MessageBox.Show("Select a tag and timestamp first.", "Delete ROI");
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete ROI: {SelectedTag} @ {SelectedTimestamp}?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            if (RoiStore.Delete(SelectedTag, SelectedTimestamp))
            {
                RefreshTimestamps();
                StatusMessage = "ROI deleted.";
            }
            else
            {
                StatusMessage = "Delete failed.";
            }
        }
    }
}