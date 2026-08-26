using DXApplication2.Common.Data;
using DXApplication2.Common.IO;
using DXApplication2.Common.Models;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Roi;
using DXApplication2.Common.Background;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    public static class PshgRawService
    {
        private class Counter { public int Value; }
        private const int BatchSize = 50;   // 每 50 个 record 一批

        public class ProcessResult
        {
            public int ProcessedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Errors { get; set; } = new();
            public bool WasCancelled { get; set; }
        }

        public static async Task<ProcessResult> ProcessAsync(
            List<DataRecord> records,
            bool skipAlreadyProcessed,
            IProgress<(int current, int total, string status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ProcessResult();

            var pshgRoi = LoadActiveRoi(RoiTags.PSHG);
            var bgRoi = LoadActiveRoi(RoiTags.Background);
            if (pshgRoi == null)
                throw new InvalidOperationException("No PSHG ROI defined. Please select a PSHG ROI first.");
            if (bgRoi == null)
                throw new InvalidOperationException("No Background ROI defined. Please select a Background ROI first.");

            var preprocConfig = PreprocessingConfigStore.Load();
            var bgConfig = DXApplication2.Common.Background.BackgroundStore.Load();
            int total = records.Count;
            var counter = new Counter();

            for (int batchStart = 0; batchStart < total; batchStart += BatchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                var batch = records.Skip(batchStart).Take(BatchSize).ToList();
                await ProcessBatchAsync(batch, pshgRoi, bgRoi, preprocConfig, bgConfig,
                    skipAlreadyProcessed, result, progress, counter, total, cancellationToken);
            }

            return result;
        }


        private static async Task ProcessBatchAsync(
            List<DataRecord> batch,
            RoiRect pshgRoi,
            RoiRect bgRoi,
            PreprocessingConfig preprocConfig,
            BackgroundConfig bgConfig,
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
                var dbRecordsById = dbRecords.ToDictionary(r => r.Id);

                foreach (var rec in batch)
                {
                    if (ct.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        break;
                    }

                    counter.Value++;
                    progress?.Report((counter.Value, total, $"Processing Record #{rec.Id}"));

                    if (skipAlreadyProcessed && rec.ResultQuantity.HasValue)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        var value = await ProcessOneAsync(rec, pshgRoi, bgRoi, preprocConfig, bgConfig);

                        if (dbRecordsById.TryGetValue(rec.Id, out var dbRec))
                        {
                            dbRec.ResultQuantity = value;
                            rec.ResultQuantity = value;
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

        private static async Task<double> ProcessOneAsync(
            DataRecord rec,
            RoiRect pshgRoi,
            RoiRect bgRoi,
            PreprocessingConfig preprocConfig,
            BackgroundConfig bgConfig)
        {
            if (string.IsNullOrEmpty(rec.ImageFilePath))
                throw new InvalidOperationException("Record has no ImageFilePath.");

            var raw = await AscMatrixReader.ReadAsync(rec.ImageFilePath);
            var processed = PreprocessingService.Apply(raw, preprocConfig);

            int rows = processed.GetLength(0);
            int cols = processed.GetLength(1);

            ValidateRoi(pshgRoi, rows, cols, "PSHG");
            ValidateRoi(bgRoi, rows, cols, "Background");

            double sigSum = 0;
            int sigCount = 0;
            for (int i = pshgRoi.RowStart; i < pshgRoi.RowEnd; i++)
            {
                for (int j = pshgRoi.ColStart; j < pshgRoi.ColEnd; j++)
                {
                    var v = processed[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    sigSum += v;
                    sigCount++;
                }
            }
            if (sigCount == 0) throw new InvalidOperationException("PSHG ROI contains no valid pixels.");

            double bgSum = 0;
            int bgCount = 0;
            for (int i = bgRoi.RowStart; i < bgRoi.RowEnd; i++)
            {
                for (int j = bgRoi.ColStart; j < bgRoi.ColEnd; j++)
                {
                    var v = processed[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    bgSum += v;
                    bgCount++;
                }
            }
            if (bgCount == 0) throw new InvalidOperationException("Background ROI contains no valid pixels.");

            double bgAvg = bgSum / bgCount;
            return sigSum - bgAvg * sigCount;
        }

        private static RoiRect? LoadActiveRoi(string tag)
        {
            // 优先用 ActiveRoiState 里用户选的
            var activeTs = ActiveRoiState.Get(tag);
            if (!string.IsNullOrEmpty(activeTs))
            {
                var roi = RoiStore.Load(tag, activeTs);
                if (roi != null) return roi;
            }

            // Fallback: 用最新
            var timestamps = RoiStore.ListTimestamps(tag);
            if (timestamps.Count == 0) return null;
            return RoiStore.Load(tag, timestamps[0]);
        }
        private static double? TryGetDoubleFromJson(string? json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty(fieldName, out var prop)) return null;
                var s = prop.ValueKind == System.Text.Json.JsonValueKind.String
                    ? prop.GetString()
                    : prop.ToString();
                if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                    return v;
                return null;
            }
            catch { return null; }
        }
        private static void ValidateRoi(RoiRect roi, int rows, int cols, string tag)
        {
            if (roi.RowStart < 0 || roi.RowEnd > rows ||
                roi.ColStart < 0 || roi.ColEnd > cols ||
                roi.RowStart >= roi.RowEnd || roi.ColStart >= roi.ColEnd)
            {
                throw new InvalidOperationException(
                    $"{tag} ROI [{roi.RowStart}-{roi.RowEnd}, {roi.ColStart}-{roi.ColEnd}] " +
                    $"exceeds image bounds ({rows} x {cols}).");
            }
        }
    }
}