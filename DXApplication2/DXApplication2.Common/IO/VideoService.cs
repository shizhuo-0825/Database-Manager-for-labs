using DXApplication2.Common.Background;
using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.IO
{
    /// <summary>
    /// 从一组 records 生成 GIF 动画。
    /// - 自动按 co / cross polar 分成两个视频
    /// - 用 Image Background 做 (S-B)/B(无 BG 就用 preprocessed)
    /// - 左上角显示 Inspector 字段
    /// </summary>
    public static class VideoService
    {
        public const int Fps = 10;
        public const double CoCrossThreshold = 45.0;
        public const string PolarizerField = "Probe_P_Polarizer";
        public const string AnalyzerField = "Probe_P_Analyzer";

        public class VideoResult
        {
            public string? CoGifPath { get; set; }
            public string? CrossGifPath { get; set; }
            public string? OutputDir { get; set; }
            public int CoFrameCount { get; set; }
            public int CrossFrameCount { get; set; }
            public List<string> Errors { get; } = new();
        }

        public static async Task<VideoResult> GenerateAsync(
            List<DataRecord> records,
            IProgress<(int current, int total, string status)>? progress = null,
            CancellationToken ct = default)
        {
            var result = new VideoResult();

            if (records.Count == 0)
            {
                result.Errors.Add("No records selected.");
                return result;
            }

            // 1. 按 RowIndex 排序
            var sorted = records.OrderBy(r => r.Id).ToList();

            // 2. 分 co / cross
            var coRecords = new List<(DataRecord Rec, double Polarizer)>();
            var crossRecords = new List<(DataRecord Rec, double Polarizer)>();
            foreach (var r in sorted)
            {
                var pol = TryGetDouble(r.ExptParams, PolarizerField);
                var ana = TryGetDouble(r.ExptParams, AnalyzerField);
                if (!pol.HasValue || !ana.HasValue) continue;

                double diff = Math.Abs(pol.Value - ana.Value) % 180;
                if (diff > 90) diff = 180 - diff;

                if (diff < CoCrossThreshold)
                    coRecords.Add((r, pol.Value));
                else
                    crossRecords.Add((r, pol.Value));
            }

            // 3. 加载 Image Background(可选)
            var bgConfig = BackgroundStore.Load();
            double[,]? backgroundMatrix = null;
            var preprocConfig = PreprocessingConfigStore.Load();

            if (bgConfig.Image != null && File.Exists(bgConfig.Image.ImageFilePath))
            {
                var raw = await Common.IO.AscMatrixReader.ReadAsync(bgConfig.Image.ImageFilePath);
                backgroundMatrix = PreprocessingService.Apply(raw, preprocConfig);
            }

            // 4. 决定输出目录:用第一个 record 的目录
            var firstRec = records.FirstOrDefault(r => !string.IsNullOrEmpty(r.ImageFilePath));
            if (firstRec == null)
            {
                result.Errors.Add("No record has an image file path.");
                return result;
            }
            var outputDir = Path.Combine(Path.GetDirectoryName(firstRec.ImageFilePath)!, "output");
            Directory.CreateDirectory(outputDir);
            result.OutputDir = outputDir;

            // 5. 找 Group 信息用于命名
            using var db = new AppDbContext();
            var group = await db.DataGroups
                    .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                    .FirstOrDefaultAsync(
                      g => g.Id == firstRec.DataGroupId,
                        ct);
            var groupNameSafe = SafeFileName(group?.Material ?? "group");
            var dateStr = group?.ExperimentDate.ToString("yyyyMMdd") ?? "unknown";

            var allExpTypes = await db.ExperimentTypes.AsNoTracking().ToListAsync(ct);
            var allExpParams = await db.ExperimentParamss.AsNoTracking()
                .Include(p => p.ExperimentType).ToListAsync(ct);

            int totalFrames = coRecords.Count + crossRecords.Count;
            int frameCounter = 0;

            // 6. 生成 co video
            if (coRecords.Count > 0)
            {
                var path = Path.Combine(outputDir, $"{groupNameSafe}_{dateStr}_co.gif");
                await GenerateOneGifAsync(coRecords, backgroundMatrix, preprocConfig, group,
                    allExpTypes, allExpParams, path,
                    p => progress?.Report((frameCounter + p.current, totalFrames, $"[co] {p.status}")),
                    ct);
                frameCounter += coRecords.Count;
                result.CoGifPath = path;
                result.CoFrameCount = coRecords.Count;
            }

            // 7. 生成 cross video
            if (crossRecords.Count > 0)
            {
                var path = Path.Combine(outputDir, $"{groupNameSafe}_{dateStr}_cross.gif");
                await GenerateOneGifAsync(crossRecords, backgroundMatrix, preprocConfig, group,
                    allExpTypes, allExpParams, path,
                    p => progress?.Report((frameCounter + p.current, totalFrames, $"[cross] {p.status}")),
                    ct);
                result.CrossGifPath = path;
                result.CrossFrameCount = crossRecords.Count;
            }

            return result;
        }

        private static async Task GenerateOneGifAsync(
            List<(DataRecord Rec, double Polarizer)> records,
            double[,]? backgroundMatrix,
            PreprocessingConfig preprocConfig,
            DataGroup group,
            List<ExperimentType> allExpTypes,
            List<ExperimentParams> allExpParams,
            string outputPath,
            Action<(int current, int total, string status)>? progressReport,
            CancellationToken ct)
        {
            if (records.Count == 0) return;

            // 第一次遍历:决定全局 min/max(让 colormap 稳定)
            double globalMin = double.MaxValue, globalMax = double.MinValue;
            var processedMatrices = new List<double[,]>();
            for (int i = 0; i < records.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progressReport?.Invoke((i + 1, records.Count * 2, $"Prep {i + 1}/{records.Count}"));

                var mat = await ProcessMatrixAsync(records[i].Rec, backgroundMatrix, preprocConfig);
                processedMatrices.Add(mat);

                int rows = mat.GetLength(0);
                int cols = mat.GetLength(1);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var v = mat[r, c];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        if (v < globalMin) globalMin = v;
                        if (v > globalMax) globalMax = v;
                    }
            }
            if (globalMin >= globalMax) { globalMin = 0; globalMax = 1; }

            // 第二次遍历:渲染帧 → 组装 GIF
            using var gif = new Image<Rgba32>(FrameRenderer.OutputSize, FrameRenderer.OutputSize);
            int frameDelayCs = 100 / Fps;   // 1/100 秒

            for (int i = 0; i < records.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progressReport?.Invoke((records.Count + i + 1, records.Count * 2, $"Frame {i + 1}/{records.Count}"));

                var overlayLines = BuildOverlayLines(records[i].Rec, group, allExpTypes, allExpParams);
                using var frame = FrameRenderer.Render(processedMatrices[i], overlayLines, globalMin, globalMax);

                if (i == 0)
                {
                    gif.Mutate(ctx => ctx.DrawImage(frame, 1f));
                    var meta0 = gif.Frames.RootFrame.Metadata.GetGifMetadata();
                    meta0.FrameDelay = frameDelayCs;
                }
                else
                {
                    var newFrame = gif.Frames.AddFrame(frame.Frames.RootFrame);
                    var meta = newFrame.Metadata.GetGifMetadata();
                    meta.FrameDelay = frameDelayCs;
                }
            }

            // 设置 GIF 循环
            var gifMeta = gif.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = 0;   // 0 = 无限循环

            await gif.SaveAsGifAsync(outputPath, new GifEncoder(), ct);
        }

        private static async Task<double[,]> ProcessMatrixAsync(DataRecord rec,
            double[,]? backgroundMatrix, PreprocessingConfig preprocConfig)
        {
            var raw = await Common.IO.AscMatrixReader.ReadAsync(rec.ImageFilePath!);
            var processed = PreprocessingService.Apply(raw, preprocConfig);

            int rows = processed.GetLength(0);
            int cols = processed.GetLength(1);

            if (backgroundMatrix == null ||
                backgroundMatrix.GetLength(0) != rows ||
                backgroundMatrix.GetLength(1) != cols)
            {
                return processed;   // 无 BG,直接用 preprocessed
            }

            var final = new double[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double s = processed[i, j];
                    double b = backgroundMatrix[i, j];
                    if (b == 0 || double.IsNaN(b) || double.IsNaN(s))
                        final[i, j] = double.NaN;
                    else
                        final[i, j] = (s - b) / b;
                }
            }
            return final;
        }

        private static List<string> BuildOverlayLines(DataRecord rec, DataGroup group,
            List<ExperimentType> allExpTypes, List<ExperimentParams> allExpParams)
        {
            if (group == null) return new List<string> { $"Record #{rec.Id}" };

            var items = InspectorService.GetInspectorItemsForRecord(rec, group, allExpTypes, allExpParams);

            foreach (var item in items)
            {
                Debug.WriteLine($"{item.Name} = {item.Value}");
            }

            return items.Select(x => $"{x.Name} {x.Value}").ToList();
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (var c in name)
                sb.Append(invalid.Contains(c) ? '_' : c);
            return sb.ToString();
        }

        private static double? TryGetDouble(string? json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(fieldName, out var prop)) return null;
                var s = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    return v;
                return null;
            }
            catch { return null; }
        }
    }
}