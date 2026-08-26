using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// Raw processing 的分发中枢:根据 Source Type 分发到不同 Service。
    /// - PSHG → PshgRawService
    /// - SHGImage → ShgImageService
    /// - (未来)RaSHG → RaShgRawService
    /// - 未知类型 → 跳过 + 报告 error
    /// </summary>
    public static class RawProcessingDispatcher
    {
        public class AggregateResult
        {
            public int ProcessedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Errors { get; set; } = new();
            public bool WasCancelled { get; set; }
        }

        public static async Task<AggregateResult> ProcessAsync(
            List<DataRecord> records,
            bool skipAlreadyProcessed,
            IProgress<(int current, int total, string status)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var agg = new AggregateResult();
            if (records.Count == 0) return agg;

            // 1. 查各 record 属于哪种 Source Type
            var groupIds = records.Select(r => r.DataGroupId).Distinct().ToList();
            using var db = new AppDbContext();
            var groupSourceMap = await db.DataGroups
                .Where(g => groupIds.Contains(g.Id))
                .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                .ToDictionaryAsync(
                    g => g.Id,
                    g => g.GroupExperimentTypes
                        .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                        .Select(get => get.ExperimentType.Name)
                        .FirstOrDefault() ?? "Unknown");

            // 2. 分组 records
            var buckets = new Dictionary<string, List<DataRecord>>();
            foreach (var r in records)
            {
                if (!groupSourceMap.TryGetValue(r.DataGroupId, out var src)) src = "Unknown";
                if (!buckets.TryGetValue(src, out var list))
                {
                    list = new List<DataRecord>();
                    buckets[src] = list;
                }
                list.Add(r);
            }

            // 3. 分发到各 Service(顺序执行,合并结果)
            int totalDone = 0;
            int totalAll = records.Count;

            foreach (var kvp in buckets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    agg.WasCancelled = true;
                    break;
                }

                var sourceType = kvp.Key;
                var bucket = kvp.Value;

                // 每个 bucket 的 progress 转发时加上前面的 totalDone 偏移
                int done0 = totalDone;
                var subProgress = new Progress<(int current, int total, string status)>(p =>
                {
                    progress?.Report((done0 + p.current, totalAll, $"[{sourceType}] {p.status}"));
                });

                try
                {
                    switch (sourceType)
                    {
                        case "PSHG":
                            {
                                var r = await PshgRawService.ProcessAsync(bucket, skipAlreadyProcessed, subProgress, cancellationToken);
                                agg.ProcessedCount += r.ProcessedCount;
                                agg.SkippedCount += r.SkippedCount;
                                agg.Errors.AddRange(r.Errors);
                                if (r.WasCancelled) agg.WasCancelled = true;
                                break;
                            }
                        case "SHGImage":
                            {
                                var r = await ShgImageService.ProcessAsync(bucket, skipAlreadyProcessed, subProgress, cancellationToken);
                                agg.ProcessedCount += r.ProcessedCount;
                                agg.SkippedCount += r.SkippedCount;
                                agg.Errors.AddRange(r.Errors);
                                if (r.WasCancelled) agg.WasCancelled = true;
                                break;
                            }
                        // 未来:
                        // case "RaSHG": ...

                        default:
                            agg.Errors.Add($"[{sourceType}] No processor available for {bucket.Count} records.");
                            agg.SkippedCount += bucket.Count;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    agg.Errors.Add($"[{sourceType}] {ex.Message}");
                    agg.SkippedCount += bucket.Count;
                }

                totalDone += bucket.Count;
            }

            return agg;
        }
    }
}