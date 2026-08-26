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
    /// <summary>
    /// 兜底 Processor:没有专用 Processor 时用这个。
    /// X 轴 = Group 涉及的 Method/Pump 关联 Params 里 Id 最小的字段(找不到就用第一个数值字段)
    /// Y 轴 = ResultQuantity(如果没值,尝试用 JSON 里的第二个数值字段)
    /// 每 Group 一条曲线,Legend = "Material, Date"
    /// </summary>
    public class DefaultProcessor : IDataProcessor
    {
        public string SourceType => "Default";

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

            var xFieldName = context.XAxisFieldName ?? DetermineXField(groups.First(), allParams, context.Records[0]);
            var yFieldName = context.YAxisFieldName ?? FieldOptionsService.ResultFieldName;

            var result = new ProcessedData
            {
                SourceType = SourceType,
                XLabel = FormatFieldLabel(xFieldName, allParams),
                YLabel = FormatFieldLabel(yFieldName, allParams),
            };

            var byGroup = context.Records.GroupBy(r => r.DataGroupId).ToList();

            foreach (var groupRecs in byGroup)
            {
                var group = groups.FirstOrDefault(g => g.Id == groupRecs.Key);
                if (group == null) continue;

                var pairs = new List<(double x, double y)>();
                foreach (var rec in groupRecs)
                {
                    var parsed = ParseJson(rec.ExptParams);
                    if (!parsed.TryGetValue(xFieldName, out var xStr)) continue;
                    if (!double.TryParse(xStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var x)) continue;

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
                if (pairs.Count == 0) continue;

                var label = byGroup.Count > 1
                    ? $"{group.Material}, {group.ExperimentDate:yyyy.MM.dd}"
                    : "";

                result.Curves.Add(new Curve
                {
                    X = pairs.Select(p => p.x).ToArray(),
                    Y = pairs.Select(p => p.y).ToArray(),
                    Label = label
                });
            }

            // 单曲线 → 直接用 X/Y
            if (result.Curves.Count == 1)
            {
                var only = result.Curves[0];
                result.X = only.X;
                result.Y = only.Y;
                result.Curves.Clear();
            }

            return result;
        }

        private static string FormatFieldLabel(string fieldName, List<ExperimentParams> allParams)
        {
            if (fieldName == FieldOptionsService.ResultFieldName) return "Result";
            var param = allParams.FirstOrDefault(p => p.FieldName == fieldName);
            var display = param?.DisplayName;
            if (string.IsNullOrEmpty(display)) display = fieldName;
            var unit = param?.Unit ?? "";
            return string.IsNullOrEmpty(unit) ? display : $"{display} ({unit})";
        }

        private static string DetermineXField(DataGroup group, List<ExperimentParams> allParams, DataRecord sample)
        {
            // 优先 Method/Pump 关联 Params 里 Id 最小的
            var methodPumpTypeIds = group.GroupExperimentTypes
                .Where(get => get.ExperimentType != null)
                .Where(get => get.ExperimentType.Category == "Method" || get.ExperimentType.Category == "Pump")
                .Select(get => get.ExperimentType.Id)
                .ToHashSet();

            var candidate = allParams
                .Where(p => p.ExperimentTypeId.HasValue && methodPumpTypeIds.Contains(p.ExperimentTypeId.Value))
                .Where(p => !p.IsDeprecated)
                .OrderBy(p => p.Id)
                .FirstOrDefault();

            if (candidate != null) return candidate.FieldName;

            // 兜底:JSON 里第一个数值字段
            var parsed = ParseJson(sample.ExptParams);
            foreach (var kvp in parsed)
            {
                if (double.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    return kvp.Key;
            }
            return "Index";
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