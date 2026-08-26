using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    public enum PlotKind
    {
        LineOrScatter,
        Heatmap,
        Polar,          // 新加
        PolarFilled     // 新加
    }

    public static class PlotDispatcher
    {
        public static async Task<IDataProcessor> ResolveProcessorAsync(
    List<DataRecord> records,
    PlotKind kind = PlotKind.LineOrScatter,
    ProcessingContext? context = null)   // ← 加这个参数
        {
            if (records.Count == 0)
                return new DefaultProcessor();

            if (kind == PlotKind.Heatmap)
            {
                System.Diagnostics.Debug.WriteLine(
        $"[Dispatcher] Heatmap: X='{context?.XAxisFieldName}', Y='{context?.YAxisFieldName}', ctx={(context == null ? "NULL" : "OK")}");
                var groupIds2 = records.Select(r => r.DataGroupId).Distinct().ToList();
                using var db2 = new AppDbContext();
                var sourceTypes2 = await db2.DataGroups
                    .Where(g => groupIds2.Contains(g.Id))
                    .SelectMany(g => g.GroupExperimentTypes)
                    .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                    .Select(get => get.ExperimentType.Name)
                    .Distinct()
                    .ToListAsync();

                // SHGImage 角度图:仅在用户没手动指定 X/Y axis 时走
                bool userSpecifiedAxis =
                    !string.IsNullOrEmpty(context?.XAxisFieldName) ||
                    !string.IsNullOrEmpty(context?.YAxisFieldName);

                if (sourceTypes2.Count == 1 && sourceTypes2[0] == "SHGImage" && !userSpecifiedAxis)
                    return new ShgImageAngleMapProcessor();

                return new HeatmapProcessor();
            }

            // Polar / PolarFilled 保持不变
            if (kind == PlotKind.Polar || kind == PlotKind.PolarFilled)
            {
                var groupIds1 = records.Select(r => r.DataGroupId).Distinct().ToList();
                using var db1 = new AppDbContext();
                var sourceTypes1 = await db1.DataGroups
                    .Where(g => groupIds1.Contains(g.Id))
                    .SelectMany(g => g.GroupExperimentTypes)
                    .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                    .Select(get => get.ExperimentType.Name)
                    .Distinct()
                    .ToListAsync();
                if (sourceTypes1.Count == 1 && sourceTypes1[0] == "PSHG")
                    return new PolarPlotProcessor();
                return new PolarPlotProcessor();
            }

            // Line/Scatter 保持不变
            var groupIds = records.Select(r => r.DataGroupId).Distinct().ToList();
            using var db = new AppDbContext();
            var sourceTypes = await db.DataGroups
                .Where(g => groupIds.Contains(g.Id))
                .SelectMany(g => g.GroupExperimentTypes)
                .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                .Select(get => get.ExperimentType.Name)
                .Distinct()
                .ToListAsync();

            if (sourceTypes.Count != 1)
                return new DefaultProcessor();

            return sourceTypes[0] switch
            {
                "DRR" => new DrrProcessor(),
                _ => new DefaultProcessor()
            };
        }
    }
}