using DevExpress.Mvvm;
using DevExpress.Mvvm.POCO;
using System;
using System.Windows;
using DXApplication2.Common.Models;
using DXApplication2.Common.Data;
using MessageBox = System.Windows.MessageBox;
using DXApplication2.Main.Views;   // ← 加这个 using
using System.Linq;
using System.Collections.Generic;
using DXApplication2.Main.Services;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using DXApplication2.Common.Messages;
using DevExpress.Mvvm.ModuleInjection;
using DXApplication2.Common;
using DXApplication2.Common.Processing;
using DevExpress.Xpf.Core;
using System.Threading;
using DXApplication2.Common.IO;
using DXApplication2.Common.Roi;
using Microsoft.Win32;
using System.Text;
using System.Text.Json;
using AppModules = DXApplication2.Common.Modules;   // 跟 Bootstrapper 一致
// ...


namespace DXApplication2.Main.ViewModels
{
    public class MainViewModel : ViewModelBase   // ← 继承 ViewModelBase
    {
        // ========== 静态创建方法 ==========
        public static object Create()
        {
            return ViewModelSource.Create(() => new MainViewModel());   // ← 用 ViewModelSource
        }
        public DelegateCommand GenerateVideoCommand { get; }
        private List<DataRecord> _lastSelectedRecords = new();
        private string _lastSelectionSource = "";
        private bool _showHidden;
        public bool ShowHidden
        {
            get => _showHidden;
            set
            {
                if (_showHidden == value) return;
                _showHidden = value;
                UiPreferences.ShowHidden = value;
                RaisePropertyChanged(nameof(ShowHidden));
                Messenger.Default.Send(new RefreshCurrentModuleMessage { FolderPath = _currentFolder });
            }
        }
        // ========== Commands ==========
        public DelegateCommand DataOpenFolderCommand { get; }
        public DelegateCommand DataReloadCommand { get; }
        public DelegateCommand DataUpdateCommand { get; }
        public DelegateCommand DataDeleteAllCommand { get; }
        public DelegateCommand ConfigsUpdateExperimentTypesCommand { get; }
        public DelegateCommand ConfigsUpdateExperimentParametersCommand { get; }
        public DelegateCommand DataSaveCommand { get; }
        public DelegateCommand DataRefreshCommand { get; }
        public DelegateCommand LinePlotCommand { get; }
        public DelegateCommand DeleteAllCommand { get; }
        public DelegateCommand ScatterPlotCommand { get; }
        public DelegateCommand HeatmapCommand { get; }
        public DelegateCommand PreviewCommand { get; }
        public DelegateCommand ViewPreprocessingCommand { get; }     // 新
        public DelegateCommand SelectRoiCommand { get; }             // 新
        public DelegateCommand DefaultProcessCommand { get; }
        public DelegateCommand ReprocessCommand { get; }
        public DelegateCommand PolarPlotCommand { get; }         // 无填充
        public DelegateCommand PolarPlotFilledCommand { get; }   // 填充
        public DelegateCommand OpenBackgroundCommand { get; }
        public DelegateCommand SetImageBackgroundCommand { get; }
        public DelegateCommand SetPumpBackgroundCommand { get; }
        public DelegateCommand ExportResultCsvCommand { get; }
        public DelegateCommand DataHideCommand { get; }
        public DelegateCommand DataShowHideCommand { get; }
        public DelegateCommand DataDeleteCommand { get; }
        // ========== Constructor ==========
        // ViewModelSource 要求构造函数是 protected
        protected MainViewModel()
        {
            DataOpenFolderCommand = new DelegateCommand(async () => await ExecuteDataOpenFolder());
            DataReloadCommand = new DelegateCommand(ExecuteDataReload);
            DataUpdateCommand = new DelegateCommand(ExecuteDataUpdate);
            DataDeleteAllCommand = new DelegateCommand(ExecuteDataDelete);
            ConfigsUpdateExperimentTypesCommand = new DelegateCommand(ExecuteConfigsUpdateExperimentTypes);
            ConfigsUpdateExperimentParametersCommand = new DelegateCommand(ExecuteConfigsUpdateExperimentParameters);
            DataSaveCommand = new DelegateCommand(ExecuteSave);
            DataRefreshCommand = new DelegateCommand(ExecuteRefresh);
            LinePlotCommand = new DelegateCommand(ExecuteLinePlot);
            ScatterPlotCommand = new DelegateCommand(ExecuteScatterPlot);
            HeatmapCommand = new DelegateCommand(ExecuteHeatmap);
            PreviewCommand = new DelegateCommand(() => OpenPreview(PreviewMode.Raw));
            ViewPreprocessingCommand = new DelegateCommand(() => OpenPreview(PreviewMode.Preprocessed));
            SelectRoiCommand = new DelegateCommand(() => OpenPreview(PreviewMode.SelectRoi));
            Messenger.Default.Register<OpenRecordsForGroupMessage>(this, OnOpenRecordsRequested);
            DefaultProcessCommand = new DelegateCommand(async () => await ExecuteProcessAsync(skipAlreadyProcessed: true));
            ReprocessCommand = new DelegateCommand(async () => await ExecuteProcessAsync(skipAlreadyProcessed: false));
            PolarPlotCommand = new DelegateCommand(() => ExecutePolarPlot(withFill: false));
            PolarPlotFilledCommand = new DelegateCommand(() => ExecutePolarPlot(withFill: true));
            OpenBackgroundCommand = new DelegateCommand(ExecuteOpenBackground);
            SetImageBackgroundCommand = new DelegateCommand(ExecuteSetImageBackground);
            SetPumpBackgroundCommand = new DelegateCommand(ExecuteSetPumpBackground);
            GenerateVideoCommand = new DelegateCommand(async () => await ExecuteGenerateVideoAsync());
            ExportResultCsvCommand = new DelegateCommand(ExecuteExportResultCsv);
            DataHideCommand = new DelegateCommand(async () => await ApplyFlagAsync(hide: true, delete: false));
            DataShowHideCommand = new DelegateCommand(async () => await ApplyFlagAsync(hide: false, delete: false));
            DataDeleteCommand = new DelegateCommand(async () => await ApplyFlagAsync(hide: null, delete: true));
            System.Threading.Tasks.Task.Run(() =>
            {
                using var db = new AppDbContext();
                //db.Database.EnsureCreated();
                
                // 触发一次查询,让 EF Core 完成所有初始化
                db.ExperimentTypes.Any();
            });
            Messenger.Default.Register<SelectedRecordsChangedMessage>(this, msg =>
            {
                _lastSelectedRecords = msg.Records;
                _lastSelectionSource = msg.Source;
            });
        }

        // ========== Command Handlers ==========
        private void OnOpenRecordsRequested(OpenRecordsForGroupMessage msg)
        {
            var manager = ModuleManager.DefaultManager;

            // 用 InjectOrNavigate:如果没注入就注入,已注入就激活
            manager.InjectOrNavigate(Regions.Documents, AppModules.Records);

            // 广播 GroupId 给 Records Module
            Messenger.Default.Send(new LoadRecordsForGroupMessage
            {
                GroupId = msg.GroupId
            });
        }
        private async void ExecuteDataReload()
        {
            try
            {
                await new ImportService().FullRebuildImportAsync();
                //MessageBox.Show("Reload Complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                if (ex.InnerException != null)
                    Debug.WriteLine(ex.InnerException.ToString());

                throw;
            }
        }
        private async void ExecuteDataDelete()
        {
            try
            {
                await new ImportService().DeleteAllDataAsync();
                //MessageBox.Show("Reload Complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());

                if (ex.InnerException != null)
                    Debug.WriteLine(ex.InnerException.ToString());

                throw;
            }
        }
        private async void ExecuteDataUpdate()
        {
            try
            {
                await new ImportService().IncrementalImportAsync();
                //MessageBox.Show("Update Complete");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ExecuteConfigsUpdateExperimentTypes()
        {
            var window = new ExperimentTypesWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void ExecuteConfigsUpdateExperimentParameters()
        {
            var window = new ExperimentParamsWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private async Task ExecuteDataOpenFolder()
        {
            // 1. 弹出文件夹选择器
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Data Folder" };
            if (dialog.ShowDialog() != true) return;

            _currentFolder = dialog.FolderName;

            // 广播文件夹改变消息,所有 Module 都会收到并按此过滤
            Messenger.Default.Send(new FolderChangedMessage
            {
                FolderPath = _currentFolder
            });


            // 2. 判断路径是否已存在
            using var db = new AppDbContext();

            var existing = await db.DataFolders
                .FirstOrDefaultAsync(f => f.FolderPath == _currentFolder);

            if (existing != null)
            {
                // 已存在:更新 ImportedAt(记录用户最近一次打开的时间)
                existing.ImportedAt = DateTime.Now;
                await db.SaveChangesAsync();
                return;
            }
            try
            {
                var folder = new DataFolder
                {
                    FolderPath = _currentFolder,
                    ImportedAt = DateTime.Now
                };

                db.DataFolders.Add(folder);
                await db.SaveChangesAsync();
                await new ImportService().IncrementalImportAsync();
                //MessageBox.Show($"导入成功!\n路径: {folderPath}\nID: {folder.Id}","完成",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            
        }
        private void ExecuteSave()
        {
            // 广播 SaveCurrentModuleMessage 消息,任何监听这个消息的 Module 都会收到
            Messenger.Default.Send(new SaveCurrentModuleMessage());
        }

        private void ExecuteRefresh()
        {
            // 广播 Refresh 消息,同时带上当前的 Folder(如果有)
            Messenger.Default.Send(new RefreshCurrentModuleMessage
            {
                FolderPath = _currentFolder
            });
        }
        private void ExecuteLinePlot()
        {
            var manager = ModuleManager.DefaultManager;
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            manager.InjectOrNavigate(Regions.Documents, AppModules.PlotModule);

            // 延迟到主线程空闲,让 PlotModule 完成初始化和消息订阅
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestLinePlotMessage
                {

                    Records = effective
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ExecuteScatterPlot()
        {
            var manager = ModuleManager.DefaultManager;
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            manager.InjectOrNavigate(Regions.Documents, AppModules.PlotModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestScatterPlotMessage
                {
                    Records = effective
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        // 存储当前打开的 folder 路径(需要在 ExecuteDataOpenFolder 里更新)
        private string? _currentFolder;
        private void ExecuteHeatmap()
        {
            var manager = ModuleManager.DefaultManager;
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            manager.InjectOrNavigate(Regions.Documents, AppModules.PlotModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestHeatmapMessage
                {
                    Records = effective
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        private void OpenPreview(PreviewMode mode)
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            var record = effective.FirstOrDefault();
            if (record == null)
            {
                System.Windows.MessageBox.Show("Please select a record first.", "Preview");
                return;
            }

            var manager = ModuleManager.DefaultManager;
            manager.InjectOrNavigate(Regions.Documents, AppModules.PreviewModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestPreviewMessage
                {
                    Record = record,
                    InitialMode = mode
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        private async Task ExecuteProcessAsync(bool skipAlreadyProcessed)
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            if (effective.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select records first.", "Process");
                return;
            }

            var cts = new CancellationTokenSource();
            var vm = new DXSplashScreenViewModel()
            {
                IsIndeterminate = false,   // 关键：关闭无限转圈
                Progress = 0,
                Status = "Preparing..."
            };

            var splash = SplashScreenManager.CreateWaitIndicator(vm);
            try
            {
                splash.Show();

                var progress = new Progress<(int current, int total, string status)>(p =>
                {
                    splash.ViewModel.Progress = 100.0 * p.current / p.total;
                    splash.ViewModel.Status =
                        $"{p.current}/{p.total} ({100.0 * p.current / p.total:F1}%)\n{p.status}";
                });

                var result = await RawProcessingDispatcher.ProcessAsync(
                    effective,
                    skipAlreadyProcessed,
                    progress,
                    cts.Token);

                splash.Close();

                var summary = $"Processed: {result.ProcessedCount}, Skipped: {result.SkippedCount}";
                if (result.WasCancelled) summary = "Cancelled.\n" + summary;
                if (result.Errors.Count > 0)
                {
                    summary += $"\n\nErrors ({result.Errors.Count}):\n" +
                               string.Join("\n", result.Errors.Take(5));
                    if (result.Errors.Count > 5)
                        summary += $"\n... and {result.Errors.Count - 5} more.";
                }
                //System.Windows.MessageBox.Show(summary, "Processing Done");

                Messenger.Default.Send(new RefreshCurrentModuleMessage());
            }
            catch (Exception ex)
            {
                splash.Close();
                System.Windows.MessageBox.Show(ex.Message, "Process Error");
            }
        }
        private async void ExecutePolarPlot(bool withFill)
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            if (effective.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select records first.", "PolarPlot");
                return;
            }

            // 1. 判断是否 PSHG,是的话检查有没有未处理的
            bool needProcess = await NeedPshgAutoProcessAsync(effective);

            if (needProcess)
            {
                // 2. 检查 ROI 是否存在(否则弹窗提示)
                var pshgTimestamps = RoiStore.ListTimestamps(RoiTags.PSHG);
                var bgTimestamps = RoiStore.ListTimestamps(RoiTags.Background);
                if (pshgTimestamps.Count == 0 || bgTimestamps.Count == 0)
                {
                    var missing = new List<string>();
                    if (pshgTimestamps.Count == 0) missing.Add("PSHG");
                    if (bgTimestamps.Count == 0) missing.Add("Background");
                    System.Windows.MessageBox.Show(
                        $"Cannot auto-process: {string.Join(" and ", missing)} ROI not defined.\n\n" +
                        "Please open Preview → Select ROI mode and draw the required ROI(s) first.",
                        "Missing ROI");
                    return;
                }

                // 3. 自动处理未处理的 records
                var cts = new CancellationTokenSource();
                var vm = new DXSplashScreenViewModel()
                {
                    IsIndeterminate = false,   // 关键：关闭无限转圈
                    Progress = 0,
                    Status = "Preparing..."
                };

                var splash = SplashScreenManager.CreateWaitIndicator(vm);
                try
                {
                    splash.Show();

                    var progress = new Progress<(int current, int total, string status)>(p =>
                    {
                        splash.ViewModel.Progress = 100.0 * p.current / p.total;
                        splash.ViewModel.Status =
                            $"{p.current}/{p.total} ({100.0 * p.current / p.total:F1}%)\n{p.status}";
                    });

                    var result = await PshgRawService.ProcessAsync(
                        effective,
                        skipAlreadyProcessed: true,
                        progress,
                        cts.Token);

                    splash.Close();

                    var summary = $"Processed: {result.ProcessedCount}, Skipped: {result.SkippedCount}";
                    if (result.WasCancelled) summary = "Cancelled.\n" + summary;
                    if (result.Errors.Count > 0)
                    {
                        summary += $"\n\nErrors ({result.Errors.Count}):\n" +
                                   string.Join("\n", result.Errors.Take(5));
                        if (result.Errors.Count > 5)
                            summary += $"\n... and {result.Errors.Count - 5} more.";
                    }
                    //System.Windows.MessageBox.Show(summary, "Processing Done");

                    Messenger.Default.Send(new RefreshCurrentModuleMessage());
                }
                catch (Exception ex)
                {
                    splash.Close();
                    System.Windows.MessageBox.Show(ex.Message, "Process Error");
                }
            }

            // 4. 画图
            var manager = ModuleManager.DefaultManager;
            manager.InjectOrNavigate(Regions.Documents, AppModules.PlotModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestPolarPlotMessage
                {
                    Records = effective,
                    WithFill = withFill
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        /// <summary>
        /// 判断:选中的 records 里,是否有 PSHG 类型的 Group 且存在未处理的 record.
        /// </summary>
        private async Task<bool> NeedPshgAutoProcessAsync(List<DataRecord> records)
        {
            if (records.Count == 0) return false;

            // 先看有没有未处理的
            var hasUnprocessed = records.Any(r => !r.ResultQuantity.HasValue);
            if (!hasUnprocessed) return false;

            // 查这些 records 所属的 Group 是否 PSHG
            var groupIds = records.Select(r => r.DataGroupId).Distinct().ToList();
            using var db = new AppDbContext();
            var isPshg = await db.DataGroups
                .Where(g => groupIds.Contains(g.Id))
                .SelectMany(g => g.GroupExperimentTypes)
                .AnyAsync(get => get.ExperimentType != null &&
                                 get.ExperimentType.Category == "Source" &&
                                 get.ExperimentType.Name == "PSHG");

            return isPshg;
        }
        private void ExecuteOpenBackground()
        {
            var manager = ModuleManager.DefaultManager;
            manager.InjectOrNavigate(Regions.Documents, AppModules.BackgroundModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new RequestBackgroundMessage());
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ExecuteSetImageBackground()
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            var record = effective.FirstOrDefault();
            if (record == null)
            {
                System.Windows.MessageBox.Show("Please select a record first.", "Set Image Background");
                return;
            }

            var manager = ModuleManager.DefaultManager;
            manager.InjectOrNavigate(Regions.Documents, AppModules.BackgroundModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new SetImageBackgroundMessage { Record = record });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void ExecuteSetPumpBackground()
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            if (effective.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select records (or a group) first.", "Set Pump Background");
                return;
            }

            // 尝试从选中 records 找出 Group 信息
            var groupIds = effective.Select(r => r.DataGroupId).Distinct().ToList();
            if (groupIds.Count != 1)
            {
                System.Windows.MessageBox.Show(
                    "Pump background must come from a single group. Please select records within one group.",
                    "Set Pump Background");
                return;
            }

            var groupId = groupIds[0];
            string groupName = "";
            try
            {
                using var db = new AppDbContext();
                var group = await db.DataGroups.FindAsync(groupId);
                if (group != null)
                    groupName = $"{group.Material} {group.ExperimentDate:yyyy.MM.dd}";
            }
            catch { }

            var manager = ModuleManager.DefaultManager;
            manager.InjectOrNavigate(Regions.Documents, AppModules.BackgroundModule);

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Messenger.Default.Send(new SetPumpBackgroundMessage
                {
                    Records = effective,
                    GroupId = groupId,
                    GroupName = groupName
                });
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        private void ExecuteExportResultCsv()
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            if (effective.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select records first.", "Export CSV");
                return;
            }

            // 按 Id 排序
            var sorted = effective.OrderBy(r => r.Id).ToList();

            // 收集所有 ExptParams 里出现过的 key(联合集,保持首次出现顺序)
            var allKeys = new List<string>();
            var seenKeys = new HashSet<string>();
            foreach (var r in sorted)
            {
                if (string.IsNullOrWhiteSpace(r.ExptParams)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(r.ExptParams);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (seenKeys.Add(prop.Name))
                            allKeys.Add(prop.Name);
                    }
                }
                catch { }
            }

            // 弹保存对话框
            var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"result_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();

                // 表头:Id, RowIndex, RecordName, [expt keys], ResultQuantity
                var header = new List<string> { "Id", "RowIndex", "RecordName" };
                header.AddRange(allKeys);
                header.Add("ResultQuantity");
                sb.AppendLine(string.Join(",", header.Select(CsvEscape)));

                // 数据行
                foreach (var r in sorted)
                {
                    var row = new List<string>
            {
                r.Id.ToString(),
                r.RowIndex ?? "",
                r.RecordName ?? ""
            };

                    // 每个 expt key 的值
                    Dictionary<string, string> parsed = new();
                    if (!string.IsNullOrWhiteSpace(r.ExptParams))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(r.ExptParams);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                parsed[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                    ? prop.Value.GetString() ?? ""
                                    : prop.Value.ToString();
                            }
                        }
                        catch { }
                    }
                    foreach (var key in allKeys)
                        row.Add(parsed.GetValueOrDefault(key, ""));

                    row.Add(r.ResultQuantity?.ToString("G", System.Globalization.CultureInfo.InvariantCulture) ?? "");

                    sb.AppendLine(string.Join(",", row.Select(CsvEscape)));
                }

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                System.Windows.MessageBox.Show($"Exported {sorted.Count} records to:\n{dlg.FileName}", "Export CSV");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Export Error");
            }
        }

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
        private async Task ApplyFlagAsync(bool? hide, bool delete)
        {
            if (_lastSelectedRecords.Count == 0) return;
            using var db = new AppDbContext();

            if (_lastSelectionSource == "Group")
            {
                var groupIds = _lastSelectedRecords.Select(r => r.DataGroupId).Distinct().ToList();
                var groups = await db.DataGroups.Where(g => groupIds.Contains(g.Id)).ToListAsync();
                var records = await db.DataRecords.Where(r => groupIds.Contains(r.DataGroupId)).ToListAsync();
                foreach (var g in groups) { if (hide.HasValue) g.IsHidden = hide.Value; if (delete) g.IsDeleted = true; }
                foreach (var r in records) { if (hide.HasValue) r.IsHidden = hide.Value; if (delete) r.IsDeleted = true; }
            }
            else // "Records"
            {
                var ids = _lastSelectedRecords.Select(r => r.Id).ToList();
                var records = await db.DataRecords.Where(r => ids.Contains(r.Id)).ToListAsync();
                foreach (var r in records) { if (hide.HasValue) r.IsHidden = hide.Value; if (delete) r.IsDeleted = true; }
            }

            await db.SaveChangesAsync();
            Messenger.Default.Send(new RefreshCurrentModuleMessage { FolderPath = _currentFolder });
        }
        private async Task ExecuteGenerateVideoAsync()
        {
            var effective = _lastSelectedRecords
                .Where(r => !r.IsHidden && !r.IsDeleted).ToList();
            if (effective.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select records first.", "Generate Video");
                return;
            }

            var cts = new CancellationTokenSource();
            
            var vm = new DXSplashScreenViewModel()
            {
                IsIndeterminate = false,
                Progress = 0,
                Status = "Preparing..."
            };
            var splash = SplashScreenManager.CreateWaitIndicator(vm);

            try
            {
                splash.Show();

                var progress = new Progress<(int current, int total, string status)>(p =>
                {
                    vm.Progress = 100.0 * p.current / p.total;
                    vm.Status = $"{p.current}/{p.total} ({100.0 * p.current / p.total:F1}%)\n{p.status}";
                });

                var result = await VideoService.GenerateAsync(effective, progress, cts.Token);

                splash.Close();

                var summary = new System.Text.StringBuilder();
                if (result.CoGifPath != null)
                    summary.AppendLine($"Co video: {result.CoFrameCount} frames → {System.IO.Path.GetFileName(result.CoGifPath)}");
                if (result.CrossGifPath != null)
                    summary.AppendLine($"Cross video: {result.CrossFrameCount} frames → {System.IO.Path.GetFileName(result.CrossGifPath)}");
                if (result.CoGifPath == null && result.CrossGifPath == null)
                    summary.AppendLine("No videos generated.");
                if (result.Errors.Count > 0)
                {
                    summary.AppendLine();
                    summary.AppendLine("Errors:");
                    foreach (var e in result.Errors.Take(5))
                        summary.AppendLine(e);
                }

                System.Windows.MessageBox.Show(summary.ToString(), "Video Generated");

                // 完成后打开 output 文件夹
                if (!string.IsNullOrEmpty(result.OutputDir) && System.IO.Directory.Exists(result.OutputDir))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", result.OutputDir);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                splash.Close();
                System.Windows.MessageBox.Show(ex.Message, "Video Error");
            }
        }

    }
}