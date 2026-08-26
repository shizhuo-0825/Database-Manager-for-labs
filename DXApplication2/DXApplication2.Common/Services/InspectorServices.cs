using DXApplication2.Common.Models;
using DXApplication2.Common.ViewModels;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using SixLabors.ImageSharp;
using System.Diagnostics;
namespace DXApplication2.Common.Services
{
    /// <summary>
    /// 决定 Inspector 面板显示什么字段、如何格式化的核心服务。
    /// Group ViewModel 和 Record ViewModel 都调用它。
    /// </summary>
    public static class InspectorService
    {
        // ============================================================
        // 硬编码规则:类型关联(SHGImaging follow PSHG,DRR 带 Tr)
        // ============================================================
        private static readonly Dictionary<string, string[]> RelatedTypes = new()
        {
            { "SHGImaging", new[] { "PSHG" } },   // SHGImaging 时也包含 PSHG 参数
            { "DRR", new[] { "Tr" } },             // DRR 时也包含 Tr 参数
        };

        // 温度字段的固定名字
        private const string TemperatureFieldName = "Temperature";

        // ============================================================
        // 公开 API 1:Group Inspector
        // ============================================================
        public static List<InspectorItem> GetInspectorItemsForGroup(
            DataGroup group,
            List<DataRecord> records,
            List<ExperimentType> allExpTypes,
            List<ExperimentParams> allExpParams)
        {
            var result = new List<InspectorItem>();

            // 1. 找出应该显示的字段
            var fieldsToShow = DetermineFieldsToShow(group, allExpTypes, allExpParams);

            // SHGImage 类型:强制包含 Polarizer / Analyzer
            bool isShgImage = group.GroupExperimentTypes
                .Any(get => get.ExperimentType != null &&
                            get.ExperimentType.Category == "Source" &&
                            get.ExperimentType.Name == "SHGImage");

            if (isShgImage)
            {
                var extras = new List<ExperimentParams>();
                foreach (var fieldName in new[] { "Probe_P_Polarizer", "Probe_P_Analyzer" })
                {
                    if (fieldsToShow.Any(p => p.FieldName == fieldName)) continue;
                    var param = allExpParams.FirstOrDefault(p => p.FieldName == fieldName);
                    if (param != null) extras.Add(param);
                }
                fieldsToShow = fieldsToShow.Concat(extras).ToList();
            }

            // 2. 对每个字段,从所有 Records 的 ExptParams JSON 里聚合 min/max
            foreach (var param in fieldsToShow)
            {
                var values = ExtractNumericValues(records, param.FieldName);

                if (values.Count == 0)
                {
                    result.Add(new InspectorItem(FormatLabel(param), "—"));
                    continue;
                }

                double min = values.Min();
                double max = values.Max();

                string valueText;
                if (Math.Abs(min - max) < 1e-12)
                {
                    // min==max,只显示一个值
                    valueText = FormatNumber(param, min);
                }
                else
                {
                    valueText = $"{FormatNumber(param, min)} ~ {FormatNumber(param, max)}";
                }

                result.Add(new InspectorItem(FormatLabel(param), valueText));
            }

            return result;
        }

        // ============================================================
        // 公开 API 2:Record Inspector
        // ============================================================
        public static List<InspectorItem> GetInspectorItemsForRecord(
            DataRecord record,
            DataGroup group,
            List<ExperimentType> allExpTypes,
            List<ExperimentParams> allExpParams)
        {
            Debug.WriteLine($"rec.Id = {record.Id}");
            Debug.WriteLine($"group.Id = {group.Id}");
            Debug.WriteLine($"allExpTypes.Count = {allExpTypes.Count}");
            Debug.WriteLine($"allExpParams.Count = {allExpParams.Count}");
            var result = new List<InspectorItem>();

            var fieldsToShow = DetermineFieldsToShow(group, allExpTypes, allExpParams);
            foreach (var get in group.GroupExperimentTypes)
            {
                Debug.WriteLine(
                    $"Category={get.ExperimentType?.Category}, " +
                    $"Name={get.ExperimentType?.Name}");
            }
            bool isShgImage = group.GroupExperimentTypes
                .Any(get => get.ExperimentType != null &&
                            get.ExperimentType.Category == "Source" &&
                            get.ExperimentType.Name == "SHGImage");

            if (isShgImage)
            {
                var extras = new List<ExperimentParams>();
                foreach (var fieldName in new[] { "Probe_P_Polarizer", "Probe_P_Analyzer" })
                {
                    if (fieldsToShow.Any(p => p.FieldName == fieldName)) continue;
                    var param = allExpParams.FirstOrDefault(p => p.FieldName == fieldName);
                    if (param != null) extras.Add(param);
                }
                fieldsToShow = fieldsToShow.Concat(extras).ToList();
            }
            var paramsDict = ParseExptParams(record.ExptParams);

            foreach (var param in fieldsToShow)
            {
                if (!paramsDict.TryGetValue(param.FieldName, out var raw) ||
                    !TryParseDouble(raw, out var value))
                {
                    result.Add(new InspectorItem(FormatLabel(param), "—"));
                    continue;
                }

                result.Add(new InspectorItem(FormatLabel(param), FormatNumber(param, value)));
            }
            // 如果 record 处理过(有 ResultPath),追加缩略图
            if (!string.IsNullOrEmpty(record.ResultPath))
            {
                var dir = System.IO.Path.GetDirectoryName(record.ResultPath);
                var basename = System.IO.Path.GetFileNameWithoutExtension(record.ImageFilePath ?? "");
                if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(basename))
                {
                    var thumbPath = System.IO.Path.Combine(dir, $"{basename}_thumb.png");
                    if (System.IO.File.Exists(thumbPath))
                    {
                        result.Add(new InspectorItem("", thumbPath, true));
                    }
                }
            }
            foreach (var results in result)
            {
                Debug.WriteLine($"{results.Name} = {results.Value}");
            }
            return result;
        }

        // ============================================================
        // 内部:决定显示哪些字段
        // ============================================================
        private static List<ExperimentParams> DetermineFieldsToShow(
            DataGroup group,
            List<ExperimentType> allExpTypes,
            List<ExperimentParams> allExpParams)
        {
            // 收集当前 Group 的所有 ExperimentType 名字
            var groupTypeNames = group.GroupExperimentTypes
                .Where(get => get.ExperimentType != null)
                .Select(get => get.ExperimentType.Name)
                .ToHashSet();

            // 应用硬编码规则:SHGImaging → 加 PSHG,DRR → 加 Tr
            var expandedTypeNames = new HashSet<string>(groupTypeNames);
            foreach (var typeName in groupTypeNames)
            {
                if (RelatedTypes.TryGetValue(typeName, out var relatedNames))
                {
                    foreach (var related in relatedNames)
                        expandedTypeNames.Add(related);
                }
            }

            // 找出这些 ExperimentType 的 Id
            var relevantTypeIds = allExpTypes
                .Where(t => expandedTypeNames.Contains(t.Name))
                .Select(t => t.Id)
                .ToHashSet();

            // 找出关联到这些 Type 的所有 ExperimentParams
            var fields = allExpParams
                .Where(p => p.ExperimentTypeId.HasValue && relevantTypeIds.Contains(p.ExperimentTypeId.Value))
                .Where(p => !p.IsDeprecated)
                .ToList();

            // 温度总是加上(如果它不在里面)
            if (!fields.Any(f => f.FieldName == TemperatureFieldName))
            {
                var tempParam = allExpParams
                    .FirstOrDefault(p => p.FieldName == TemperatureFieldName && !p.IsDeprecated);
                if (tempParam != null)
                    fields.Insert(0, tempParam);   // 温度放最前
            }
            else
            {
                // 温度已在列表里,把它挪到最前
                var tempIdx = fields.FindIndex(f => f.FieldName == TemperatureFieldName);
                if (tempIdx > 0)
                {
                    var t = fields[tempIdx];
                    fields.RemoveAt(tempIdx);
                    fields.Insert(0, t);
                }
            }

            // 按 DisplayOrder 排序(温度已固定在最前,其他按 Order)
            var temperature = fields.FirstOrDefault(f => f.FieldName == TemperatureFieldName);
            var rest = fields.Where(f => f.FieldName != TemperatureFieldName)
                             .OrderBy(f => f.DisplayOrder ?? int.MaxValue)
                             .ThenBy(f => f.FieldName)
                             .ToList();

            var final = new List<ExperimentParams>();
            if (temperature != null) final.Add(temperature);
            final.AddRange(rest);

            return final;
        }

        // ============================================================
        // 内部:从 Records 的 ExptParams JSON 里提取数值
        // ============================================================
        private static List<double> ExtractNumericValues(List<DataRecord> records, string fieldName)
        {
            var values = new List<double>();
            foreach (var r in records)
            {
                var dict = ParseExptParams(r.ExptParams);
                if (dict.TryGetValue(fieldName, out var raw) && TryParseDouble(raw, out var v))
                    values.Add(v);
            }
            return values;
        }

        // ============================================================
        // 内部:解析 ExptParams JSON 到 Dictionary
        // ============================================================
        private static Dictionary<string, string> ParseExptParams(string? json)
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
            catch
            {
                // JSON 格式错就返回空,不抛异常
            }
            return result;
        }

        private static bool TryParseDouble(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        // ============================================================
        // 内部:格式化标签(带单位)
        // ============================================================
        private static string FormatLabel(ExperimentParams param)
        {
            var displayName = string.IsNullOrEmpty(param.DisplayName)
                ? param.FieldName
                : param.DisplayName;

            var unit = param.Unit ?? "";

            // DRR 相关字段:单位前加 u
            if (IsDRRRelated(param))
            {
                unit = string.IsNullOrEmpty(unit) ? "× 10⁶" : "u" + unit;
            }

            return string.IsNullOrEmpty(unit)
                ? displayName
                : $"{displayName} ({unit})";
        }

        // ============================================================
        // 内部:格式化数值
        // ============================================================
        private static string FormatNumber(ExperimentParams param, double value)
        {
            // 温度:保留 2 位小数
            if (param.FieldName == TemperatureFieldName)
            {
                return value.ToString("F2", CultureInfo.InvariantCulture);
            }

            // DRR 相关:× 10^6,再按 2 位有效数字
            if (IsDRRRelated(param))
            {
                var scaled = value * 1e6;
                return FormatTwoSignificantDigits(scaled);
            }

            // 其他:2 位有效数字
            return FormatTwoSignificantDigits(value);
        }

        // ============================================================
        // 内部:2 位有效数字(普通形式,不用科学计数法)
        // 示例:800.5 → "800",  12345 → "12000",  0.0345 → "0.035",  0 → "0"
        // ============================================================
        private static string FormatTwoSignificantDigits(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value.ToString();
            if (value == 0) return "0";

            double abs = Math.Abs(value);
            int magnitude = (int)Math.Floor(Math.Log10(abs));

            // magnitude=0 时数量级 1,decimalPlaces=1(留 1 位小数)
            // magnitude=2 时数量级 100,decimalPlaces=-1(保留整数,末位归零)
            // magnitude=-2 时数量级 0.01,decimalPlaces=3(留 3 位小数)
            int decimalPlaces = 3 - magnitude;

            if (decimalPlaces >= 0)
            {
                return value.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture);
            }
            else
            {
                // 大于等于 10 的数,保留 2 位有效数字,末位归零
                double scale = Math.Pow(10, -decimalPlaces);
                double rounded = Math.Round(value / scale) * scale;
                return rounded.ToString("F0", CultureInfo.InvariantCulture);
            }
        }

        // ============================================================
        // 内部:判断参数是否 DRR 相关
        // ============================================================
        private static bool IsDRRRelated(ExperimentParams param)
        {
            return param.ExperimentType?.Name == "DRR";
        }
    }
}