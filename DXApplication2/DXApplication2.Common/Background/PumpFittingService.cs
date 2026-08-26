using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DXApplication2.Common.Background
{
    /// <summary>
    /// 用傅里叶分量提取拟合 pump background:
    ///   y(θ) = A + Σ_{k=1..MaxK} B_k * cos(k * θ_rad + φ_k)
    /// 其中 θ 是 Probe_P_Analyzer(度)
    /// 
    /// 只用 co polar 的数据点(|Analyzer - Polarizer| < 45°)。
    /// 按 Analyzer 值分箱平均后再拟合。
    /// </summary>
    public static class PumpFittingService
    {
        public const string AnalyzerField = "Probe_P_Analyzer";
        public const string PolarizerField = "Probe_P_Polarizer";
        public const double CrossCoThreshold = 45.0;

        public class FitResult
        {
            public bool Success { get; set; }
            public string? WarningMessage { get; set; }
            public double A { get; set; }
            public double[] Bk { get; set; } = Array.Empty<double>();
            public double[] Phik { get; set; } = Array.Empty<double>();

            // 用于展示的原始数据点(按 Analyzer 平均后的)
            public double[] AnalyzerVals { get; set; } = Array.Empty<double>();
            public double[] AveragedYs { get; set; } = Array.Empty<double>();
        }

        public static async Task<FitResult> FitAsync(List<int> recordIds)
        {
            using var db = new AppDbContext();
            var records = await db.DataRecords
                .Where(r => recordIds.Contains(r.Id))
                .ToListAsync();

            // 1. 过滤:必须有 ResultQuantity + Analyzer + Polarizer
            var points = new List<(double analyzer, double y)>();
            foreach (var r in records)
            {
                if (!r.ResultQuantity.HasValue) continue;
                var analyzer = TryGetDouble(r.ExptParams, AnalyzerField);
                var polarizer = TryGetDouble(r.ExptParams, PolarizerField);
                if (analyzer == null || polarizer == null) continue;

                // co polar 过滤:|Analyzer - Polarizer| < 45°(考虑周期性)
                double diff = Math.Abs(analyzer.Value - polarizer.Value) % 180;
                if (diff > 90) diff = 180 - diff;
                if (diff > CrossCoThreshold) continue;

                points.Add((analyzer.Value, r.ResultQuantity.Value));
            }

            if (points.Count < 4)
            {
                return new FitResult { Success = false, WarningMessage = $"Only {points.Count} co-polar points, need at least 4." };
            }

            // 2. 按 Analyzer 分箱(相同值取平均)
            var grouped = points
                .GroupBy(p => System.Math.Round(p.analyzer, 3))   // 3 位小数分箱
                .OrderBy(g => g.Key)
                .Select(g => (analyzer: g.Key, y: g.Average(p => p.y)))
                .ToList();

            var analyzerVals = grouped.Select(g => g.analyzer).ToArray();
            var yVals = grouped.Select(g => g.y).ToArray();

            // 3. 傅里叶分量
            int n = analyzerVals.Length;
            double A = yVals.Average();

            var Bk = new double[PumpBackgroundInfo.MaxK];
            var Phik = new double[PumpBackgroundInfo.MaxK];

            for (int k = 1; k <= PumpBackgroundInfo.MaxK; k++)
            {
                double sumCos = 0, sumSin = 0;
                for (int i = 0; i < n; i++)
                {
                    double theta = analyzerVals[i] * Math.PI / 180.0;
                    sumCos += (yVals[i] - A) * Math.Cos(k * theta);
                    sumSin += (yVals[i] - A) * Math.Sin(k * theta);
                }
                double ak = 2.0 * sumCos / n;
                double bk = 2.0 * sumSin / n;
                Bk[k - 1] = Math.Sqrt(ak * ak + bk * bk);
                Phik[k - 1] = -Math.Atan2(bk, ak);
            }

            // 4. 软约束检查:A > 0 且 A > max(Bk)
            string? warning = null;
            if (A <= 0)
                warning = $"Warning: DC offset A={A:G4} <= 0.";
            else
            {
                var maxB = Bk.Max();
                if (maxB >= A)
                    warning = $"Warning: max(Bk)={maxB:G4} >= A={A:G4}. Background may go negative.";
            }

            return new FitResult
            {
                Success = true,
                WarningMessage = warning,
                A = A,
                Bk = Bk,
                Phik = Phik,
                AnalyzerVals = analyzerVals,
                AveragedYs = yVals
            };
        }

        /// <summary>用拟合结果重构一条平滑曲线(把 θ 从 0-360° 采样 M 点)</summary>
        public static (double[] thetaDeg, double[] y) Reconstruct(FitResult fit, int nSamples = 360)
        {
            var thetas = new double[nSamples];
            var ys = new double[nSamples];
            for (int i = 0; i < nSamples; i++)
            {
                thetas[i] = 360.0 * i / (nSamples - 1);
                double theta = thetas[i] * Math.PI / 180.0;
                double y = fit.A;
                for (int k = 1; k <= PumpBackgroundInfo.MaxK; k++)
                {
                    y += fit.Bk[k - 1] * Math.Cos(k * theta + fit.Phik[k - 1]);
                }
                ys[i] = y;
            }
            return (thetas, ys);
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