using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    public class DrrProcessor : IDataProcessor
    {
        public string SourceType => "DRR";

        public const string DefaultXFieldName = "Time_delay(ps)";
        public const string DefaultYFieldName = "LockinValueX";

        public async Task<ProcessedData> ProcessAsync(ProcessingContext context, CancellationToken ct = default)
        {
            if (context.Records.Count == 0)
                return new ProcessedData { SourceType = SourceType };

            using var db = new AppDbContext();
            var groupIds = context.Records.Select(r => r.DataGroupId).Distinct().ToList();

            var groups = await db.DataGroups
                .Where(g => groupIds.Contains(g.Id))
                .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                .ToListAsync(ct);

            var allParams = await db.ExperimentParamss
                .Include(p => p.ExperimentType)
                .ToListAsync(ct);

            var xFieldName = context.XAxisFieldName ?? DefaultXFieldName;
            var yFieldName = context.YAxisFieldName ?? DefaultYFieldName;

            var result = new ProcessedData
            {
                SourceType = SourceType,
                XLabel = FormatFieldLabel(xFieldName, allParams),
                YLabel = FormatFieldLabel(yFieldName, allParams),
            };

            var byGroup = context.Records.GroupBy(r => r.DataGroupId).ToList();

            // 场景 A: 单 Group + 尝试按 Method/Pump 分组
            if (byGroup.Count == 1)
            {
                var group = groups.FirstOrDefault(g => g.Id == byGroup[0].Key);
                if (group == null) return result;

                var groupingField = DetermineGroupingField(group, allParams);

                if (!string.IsNullOrEmpty(groupingField))
                {
                    var groupingParam = allParams.FirstOrDefault(p => p.FieldName == groupingField);
                    var unit = groupingParam?.Unit ?? "";

                    // 按分组变量的值 group
                    var grouped = byGroup[0]
                        .Select(r => new { Rec = r, Parsed = ParseJson(r.ExptParams) })
                        .Where(x => x.Parsed.ContainsKey(groupingField))
                        .GroupBy(x => x.Parsed[groupingField])
                        .ToList();

                    // 有效分组:每组至少 2 个点
                    var validGroups = grouped.Where(g => g.Count() >= 2).ToList();

                    if (validGroups.Count >= 2)
                    {
                        foreach (var g in validGroups.OrderBy(g => TryOrder(g.Key)))
                        {
                            var pairs = g.Select(x => (x.Parsed, x.Rec)).ToList();
                            var (xs, ys) = ExtractSorted(pairs, xFieldName, yFieldName);
                            if (xs.Length == 0) continue;

                            var label = string.IsNullOrEmpty(unit) ? g.Key : $"{g.Key} {unit}";
                            result.Curves.Add(new Curve { X = xs, Y = ys, Label = label });
                        }
                        return result;
                    }
                }

                // 分组无效 → 单条
                // 分组无效 → 单条
                var parsedAll = byGroup[0].Select(r => (ParseJson(r.ExptParams), r)).ToList();
                var (x1, y1) = ExtractSorted(parsedAll, xFieldName, yFieldName);
                result.X = x1;
                result.Y = y1;
                return result;
            }

            // 场景 B: 多 Group → 每 Group 一条
            foreach (var groupRecs in byGroup)
            {
                var group = groups.FirstOrDefault(g => g.Id == groupRecs.Key);
                if (group == null) continue;

                var parsed = groupRecs.Select(r => (ParseJson(r.ExptParams), r)).ToList();
                var (xs, ys) = ExtractSorted(parsed, xFieldName, yFieldName);
                if (xs.Length == 0) continue;

                result.Curves.Add(new Curve
                {
                    X = xs,
                    Y = ys,
                    Label = $"{group.Material}, {group.ExperimentDate:yyyy.MM.dd}"
                });
            }

            return result;
        }

        // ============================================================
        // 辅助:决定分组字段(Method/Pump 类型关联的 Params 里 Id 最小的)
        // ============================================================
        private static string DetermineGroupingField(DataGroup group, List<ExperimentParams> allParams)
        {
            var methodPumpTypeIds = group.GroupExperimentTypes
                .Where(get => get.ExperimentType != null)
                .Where(get => get.ExperimentType.Category == "Method" || get.ExperimentType.Category == "Pump")
                .Select(get => get.ExperimentType.Id)
                .ToHashSet();

            if (methodPumpTypeIds.Count == 0) return "";

            var candidate = allParams
                .Where(p => p.ExperimentTypeId.HasValue && methodPumpTypeIds.Contains(p.ExperimentTypeId.Value))
                .Where(p => !p.IsDeprecated)
                .OrderBy(p => p.Id)
                .FirstOrDefault();

            return candidate?.FieldName ?? "";
        }

        // ============================================================
        // 辅助:格式化 Label = DisplayName (Unit)
        // ============================================================
        private static string FormatFieldLabel(string fieldName, List<ExperimentParams> allParams)
        {
            if (fieldName == FieldOptionsService.ResultFieldName) return "Result";
            var param = allParams.FirstOrDefault(p => p.FieldName == fieldName);
            var display = param?.DisplayName;
            if (string.IsNullOrEmpty(display)) display = fieldName;
            var unit = param?.Unit ?? "";
            return string.IsNullOrEmpty(unit) ? display : $"{display} ({unit})";
        }

        // ============================================================
        // 辅助:从 parsed JSON 提取 (X, Y),按 X 数值排序
        // ============================================================
        private static (double[] X, double[] Y) ExtractSorted(
            List<(Dictionary<string, string> Parsed, DataRecord Rec)> parsedList,
            string xFieldName, string yFieldName)
        {
            var pairs = new List<(double x, double y)>();
            foreach (var (parsed, rec) in parsedList)
            {
                // X
                if (!parsed.TryGetValue(xFieldName, out var xStr)) continue;
                if (!double.TryParse(xStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var x)) continue;

                // Y: 特殊处理 ResultQuantity
                double y;
                if (yFieldName == FieldOptionsService.ResultFieldName)
                {
                    if (!rec.ResultQuantity.HasValue) continue;
                    y = rec.ResultQuantity.Value;
                }
                else
                {
                    if (!parsed.TryGetValue(yFieldName, out var yStr)) continue;
                    if (!double.TryParse(yStr, NumberStyles.Any, CultureInfo.InvariantCulture, out y)) continue;
                }

                pairs.Add((x, y));
            }
            pairs.Sort((a, b) => a.x.CompareTo(b.x));
            return (pairs.Select(p => p.x).ToArray(), pairs.Select(p => p.y).ToArray());
        }

        private static double TryOrder(string key)
        {
            return double.TryParse(key, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                ? v : double.MaxValue;
        }

        private static Dictionary<string, string> ParseJson(string? json)
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
    }
}