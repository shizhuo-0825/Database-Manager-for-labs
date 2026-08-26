using DXApplication2.Common.Background;
using DXApplication2.Common.Data;
using DXApplication2.Common.IO;
using DXApplication2.Common.Models;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Roi;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// SHG Image 处理:
    /// - preprocess → (S-B)/B(如果有 Image Background)
    /// - 输出 .asc 到 {dir}/output/{基名}_processed.asc
    /// - 输出缩略图 png 到 {dir}/output/{基名}_thumb.png
    /// - 如果有 "SHGImage" ROI → 求和写入 ResultQuantity
    /// </summary>
    public static class ShgImageService
    {
        private const int BatchSize = 20;

        public class ProcessResult
        {
            public int ProcessedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Errors { get; set; } = new();
            public bool WasCancelled { get; set; }
        }

        private class Counter { public int Value; }

        public static async Task<ProcessResult> ProcessAsync(
            List<DataRecord> records,
            bool skipAlreadyProcessed,
            IProgress<(int current, int total, string status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ProcessResult();

            // 加载 Image Background(可选)
            var bgConfig = BackgroundStore.Load();
            double[,]? backgroundMatrix = null;
            var preprocConfig = PreprocessingConfigStore.Load();

            if (bgConfig.Image != null && !string.IsNullOrEmpty(bgConfig.Image.ImageFilePath)
                && File.Exists(bgConfig.Image.ImageFilePath))
            {
                var raw = await AscMatrixReader.ReadAsync(bgConfig.Image.ImageFilePath);
                backgroundMatrix = PreprocessingService.Apply(raw, preprocConfig);
            }

            // 加载 SHGImage ROI(可选)
            var roiTimestamps = RoiStore.ListTimestamps(RoiTags.SHGImage);
            RoiRect? roi = roiTimestamps.Count > 0 ? RoiStore.Load(RoiTags.SHGImage, roiTimestamps[0]) : null;

            int total = records.Count;
            var counter = new Counter();

            for (int start = 0; start < total; start += BatchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                var batch = records.Skip(start).Take(BatchSize).ToList();
                await ProcessBatchAsync(batch, backgroundMatrix, roi, preprocConfig,
                    skipAlreadyProcessed, result, progress, counter, total, cancellationToken);
            }

            return result;
        }

        private static async Task ProcessBatchAsync(
            List<DataRecord> batch,
            double[,]? backgroundMatrix,
            RoiRect? roi,
            PreprocessingConfig preprocConfig,
            bool skipAlreadyProcessed,
            ProcessResult result,
            IProgress<(int, int, string)>? progress,
            Counter counter,
            int total,
            CancellationToken ct)
        {
            using var db = new AppDbContext();
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var batchIds = batch.Select(r => r.Id).ToList();
                var dbRecords = await db.DataRecords
                    .Where(r => batchIds.Contains(r.Id))
                    .ToListAsync(ct);
                var dbById = dbRecords.ToDictionary(r => r.Id);

                foreach (var rec in batch)
                {
                    if (ct.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        break;
                    }
                    counter.Value++;
                    progress?.Report((counter.Value, total, $"Processing Record #{rec.Id}"));

                    // Default 模式跳过已有 ResultPath 的
                    if (skipAlreadyProcessed && !string.IsNullOrEmpty(rec.ResultPath) && File.Exists(rec.ResultPath))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        var (asPath, thumbPath, resultQty) = await ProcessOneAsync(rec, backgroundMatrix, roi, preprocConfig);

                        if (dbById.TryGetValue(rec.Id, out var dbRec))
                        {
                            dbRec.ResultPath = asPath;
                            if (resultQty.HasValue)
                                dbRec.ResultQuantity = resultQty.Value;
                            rec.ResultPath = asPath;
                            if (resultQty.HasValue)
                                rec.ResultQuantity = resultQty.Value;
                        }
                        result.ProcessedCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Record #{rec.Id}: {ex.Message}");
                        result.SkippedCount++;
                    }
                }

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        private static async Task<(string ascPath, string thumbPath, double? resultQuantity)> ProcessOneAsync(
            DataRecord rec,
            double[,]? backgroundMatrix,
            RoiRect? roi,
            PreprocessingConfig preprocConfig)
        {
            if (string.IsNullOrEmpty(rec.ImageFilePath))
                throw new InvalidOperationException("Record has no ImageFilePath.");

            // 1. Preprocess
            var raw = await AscMatrixReader.ReadAsync(rec.ImageFilePath);
            var processed = PreprocessingService.Apply(raw, preprocConfig);

            int rows = processed.GetLength(0);
            int cols = processed.GetLength(1);

            // 2. (S-B)/B(如果有 background)
            double[,] final;
            if (backgroundMatrix != null &&
                backgroundMatrix.GetLength(0) == rows &&
                backgroundMatrix.GetLength(1) == cols)
            {
                final = new double[rows, cols];
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
            }
            else
            {
                final = processed;   // 无 background,直接用 preprocessed
            }

            // 3. 输出路径
            var dir = Path.GetDirectoryName(rec.ImageFilePath)!;
            var basename = Path.GetFileNameWithoutExtension(rec.ImageFilePath);
            var outputDir = Path.Combine(dir, "output");
            Directory.CreateDirectory(outputDir);
            var ascPath = Path.Combine(outputDir, $"{basename}_processed.asc");
            var thumbPath = Path.Combine(outputDir, $"{basename}_thumb.png");

            // 4. 写 .asc
            WriteAscMatrix(final, ascPath);

            // 5. 写缩略图
            ThumbnailGenerator.GenerateAndSave(final, thumbPath);

            // 6. ROI 求和(如果有 SHGImage ROI)
            double? resultQty = null;
            if (roi != null)
            {
                // 验证边界
                if (roi.RowStart >= 0 && roi.RowEnd <= rows &&
                    roi.ColStart >= 0 && roi.ColEnd <= cols &&
                    roi.RowStart < roi.RowEnd && roi.ColStart < roi.ColEnd)
                {
                    double sum = 0;
                    int count = 0;
                    for (int i = roi.RowStart; i < roi.RowEnd; i++)
                    {
                        for (int j = roi.ColStart; j < roi.ColEnd; j++)
                        {
                            var v = final[i, j];
                            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                            sum += v;
                            count++;
                        }
                    }
                    if (count > 0) resultQty = sum;
                }
            }

            return (ascPath, thumbPath, resultQty);
        }

        /// <summary>写 .asc 格式(跟 AscMatrixReader 匹配:第一列序号,tab 分隔)</summary>
        private static void WriteAscMatrix(double[,] matrix, string path)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < rows; i++)
            {
                sb.Append(i);   // 第一列序号
                for (int j = 0; j < cols; j++)
                {
                    sb.Append('\t');
                    var v = matrix[i, j];
                    if (double.IsNaN(v)) sb.Append("NaN");
                    else sb.Append(v.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                }
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}