using DXApplication2.Common.Models;
using System.Globalization;
using System.Text.Json;


namespace DXApplication2.Common.Background
{
    /// <summary>
    /// 帮助 Processor 阶段做 pump background 减法。
    /// 判断是否需要减、算 pump 值、应用 scale。
    /// </summary>
    public static class PumpSubtractionHelper
    {
        /// <summary>
        /// 从一个 record 的 raw ResultQuantity 减去 Scale × pump value。
        /// 如果配置未开启、pump 未拟合、或 record 无 Analyzer 值,返回原值。
        /// </summary>
        public static double ApplySubtraction(DataRecord rec, double rawValue, BackgroundConfig bgConfig)
        {
            if (!bgConfig.ApplyToPshgRaw) return rawValue;
            if (bgConfig.Pump == null || !bgConfig.Pump.IsFitted) return rawValue;

            var analyzer = TryGetDouble(rec.ExptParams, "Probe_P_Analyzer");
            if (!analyzer.HasValue) return rawValue;

            double pumpValue = bgConfig.Pump.Evaluate(analyzer.Value);
            return rawValue - bgConfig.PumpScale * pumpValue;
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