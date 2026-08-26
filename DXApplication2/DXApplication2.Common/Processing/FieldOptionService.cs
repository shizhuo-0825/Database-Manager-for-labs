using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 从 Records 提取所有可选字段(数值型),并按 Source 决定默认 X/Y
    /// </summary>
    public static class FieldOptionsService
    {
        public const string ResultFieldName = "__ResultQuantity";  // 特殊标识:不是 JSON 里的字段

        public static async Task<AvailableFieldsResult> GetAvailableFieldsAsync(List<DataRecord> records)
        {
            var result = new AvailableFieldsResult();
            if (records.Count == 0) return result;

            using var db = new AppDbContext();

            // 1. 收集所有 Group 涉及的 ExperimentType 名字(用于判断 Source)
            var groupIds = records.Select(r => r.DataGroupId).Distinct().ToList();
            var groups = await db.DataGroups
                .Where(g => groupIds.Contains(g.Id))
                .Include(g => g.GroupExperimentTypes)
                    .ThenInclude(get => get.ExperimentType)
                .ToListAsync();

            var allParams = await db.ExperimentParamss
                .Include(p => p.ExperimentType)
                .ToListAsync();

            // 2. 从所有 Records 的 JSON 里提取字段的"并集"(数值型)
            var fieldNames = new HashSet<string>();
            foreach (var r in records)
            {
                var parsed = ParseJson(r.ExptParams);
                foreach (var kvp in parsed)
                {
                    if (double.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        fieldNames.Add(kvp.Key);
                }
            }

            // 3. 把每个 FieldName 转成 FieldOption
            foreach (var name in fieldNames.OrderBy(n => n))
            {
                var param = allParams.FirstOrDefault(p => p.FieldName == name);
                var displayName = string.IsNullOrEmpty(param?.DisplayName) ? name : param!.DisplayName!;
                var unit = param?.Unit ?? "";
                result.Fields.Add(new FieldOption
                {
                    FieldName = name,
                    DisplayLabel = string.IsNullOrEmpty(unit) ? displayName : $"{displayName} ({unit})"
                });
            }

            // 4. 加特殊字段:ResultQuantity(如果任何 Record 有值)
            if (records.Any(r => r.ResultQuantity.HasValue))
            {
                result.Fields.Add(new FieldOption
                {
                    FieldName = ResultFieldName,
                    DisplayLabel = "Result"
                });
            }

            // 5. 按 Source 决定默认 X/Y
            var (defaultX, defaultY) = ResolveDefaults(groups, allParams, records);
            result.DefaultXFieldName = defaultX;
            result.DefaultYFieldName = defaultY;

            return result;
        }

        private static (string x, string y) ResolveDefaults(
            List<DataGroup> groups,
            List<ExperimentParams> allParams,
            List<DataRecord> records)
        {
            // 判断 Source
            var sourceTypes = groups
                .SelectMany(g => g.GroupExperimentTypes)
                .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                .Select(get => get.ExperimentType.Name)
                .Distinct()
                .ToList();

            // DRR 单类型:硬编码
            if (sourceTypes.Count == 1 && sourceTypes[0] == "DRR")
            {
                return (DrrProcessor.DefaultXFieldName, DrrProcessor.DefaultYFieldName);
            }
            if (sourceTypes.Count == 1 && sourceTypes[0] == "PSHG")
            {
                return (PolarPlotProcessor.PolarizerField, ResultFieldName);
            }

            // 兜底:Method/Pump Id 最小的 / ResultQuantity
            string x = DetermineFallbackX(groups.First(), allParams, records[0]);
            string y = records.Any(r => r.ResultQuantity.HasValue) ? ResultFieldName : x;
            return (x, y);
        }

        private static string DetermineFallbackX(DataGroup group, List<ExperimentParams> allParams, DataRecord sample)
        {
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
            return "";
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