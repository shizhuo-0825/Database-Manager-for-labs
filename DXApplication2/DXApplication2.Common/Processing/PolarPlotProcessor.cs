using DXApplication2.Common.Data;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// PSHG PolarPlot 处理器:
    /// - 按 RowIndex 排序
    /// - polarizer 变化 > 180° 断成不同片段
    /// - 每片段按 |Analyzer - Polarizer| 分类:>45° = cross, <45° = co
    /// - 生成 averaged_cross / averaged_co(插值到统一网格再平均)
    /// - 负值裁剪到 0
    /// </summary>
    public class PolarPlotProcessor : IDataProcessor
    {
        public string SourceType => "PSHG_PolarPlot";

        // 当前 PSHG 阶段的字段名
        public const string PolarizerField = "Probe_P_Polarizer";
        public const string AnalyzerField = "Probe_P_Analyzer";

        // 分片段的阈值(度)
        private const double SegmentBreakThreshold = 180.0;

        // cross/co 分类阈值(度)
        private const double CrossCoThreshold = 45.0;

        // averaged 插值网格步长
        private const double AverageGridStep = 5.0;

        // 全局角度 offset(代码内锁定,不出现在 UI)
        public const double AngleOffset = 0.0;

        public async Task<ProcessedData> ProcessAsync(ProcessingContext context, CancellationToken ct = default)
        {
            if (context.Records.Count == 0)
                return new ProcessedData { SourceType = SourceType };

            var result = new ProcessedData
            {
                SourceType = SourceType,
                XLabel = "Polarizer (deg)",
                YLabel = "Signal",
            };

            // 1. 过滤:有 ResultQuantity 的
            var valid = context.Records
                .Where(r => r.ResultQuantity.HasValue)
                .OrderBy(r => r.DataGroupId)
                .ThenBy(r => r.RowIndex)
                .ToList();

            if (valid.Count < 2) return result;

            // 2. 解析每个 record 的 polarizer / analyzer
            var bgConfig = DXApplication2.Common.Background.BackgroundStore.Load();

            var parsed = valid.Select(r => new
            {
                Record = r,
                Polarizer = TryGetDouble(r.ExptParams, PolarizerField),
                Analyzer = TryGetDouble(r.ExptParams, AnalyzerField),
                Signal = Math.Max(0, DXApplication2.Common.Background.PumpSubtractionHelper.ApplySubtraction(
                    r, r.ResultQuantity!.Value, bgConfig))
            })
            .Where(x => x.Polarizer.HasValue && x.Analyzer.HasValue)
            .ToList();

            if (parsed.Count < 2) return result;

            // 3. 分片段(polarizer 变化 > 180° 断开)
            var segments = new List<List<int>>();  // 每个 List 是"这段包含的 parsed 索引"
            var currentSegment = new List<int> { 0 };
            for (int i = 1; i < parsed.Count; i++)
            {
                var delta = Math.Abs(parsed[i].Polarizer!.Value - parsed[i - 1].Polarizer!.Value);
                if (delta > SegmentBreakThreshold)
                {
                    // 断开
                    if (currentSegment.Count > 0) segments.Add(currentSegment);
                    currentSegment = new List<int>();
                }
                currentSegment.Add(i);
            }
            if (currentSegment.Count > 0) segments.Add(currentSegment);

            // 4. 每个片段生成 Curve
            var crossCurves = new List<Curve>();
            var coCurves = new List<Curve>();

            foreach (var seg in segments)
            {
                if (seg.Count < 2) continue;   // 太短不成曲线

                // 判断 cross / co(用片段第一个 record 的 analyzer/polarizer 差)
                var first = parsed[seg[0]];
                var diff = Math.Abs(first.Analyzer!.Value - first.Polarizer!.Value);
                // 归一化到 0-90(考虑周期性)
                diff = NormalizeAngleDiff(diff);

                bool isCross = diff > CrossCoThreshold;
                var startRowIndex = first.Record.RowIndex;
                var label = isCross ? $"cross_{startRowIndex}" : $"co_{startRowIndex}";

                var xs = seg.Select(i => ApplyOffset(parsed[i].Polarizer!.Value)).ToArray();
                var ys = seg.Select(i => parsed[i].Signal).ToArray();

                var curve = new Curve { X = xs, Y = ys, Label = label };
                if (isCross) crossCurves.Add(curve);
                else coCurves.Add(curve);

                result.Curves.Add(curve);
            }

            // 5. Averaged cross / co
            if (crossCurves.Count > 0)
            {
                var avg = AverageCurves(crossCurves, "averaged_cross");
                if (avg != null) result.Curves.Add(avg);
            }
            if (coCurves.Count > 0)
            {
                var avg = AverageCurves(coCurves, "averaged_co");
                if (avg != null) result.Curves.Add(avg);
            }

            System.Diagnostics.Debug.WriteLine($"PolarPlotProcessor: {result.Curves.Count} curves generated");
            foreach (var c in result.Curves)
                System.Diagnostics.Debug.WriteLine($"  {c.Label}: {c.X.Length} points");
            return result;
        }

        // ============================================================
        // 辅助:多个 Curve 插值到统一网格后平均
        // ============================================================
        private static Curve? AverageCurves(List<Curve> curves, string label)
        {
            if (curves.Count == 0) return null;

            // 统一网格:0-360 步长 5
            var grid = new List<double>();
            for (double a = 0; a < 360; a += AverageGridStep) grid.Add(a);
            var gridArr = grid.ToArray();

            // 每个 curve 插值到网格上
            var interpolated = new List<double[]>();
            foreach (var c in curves)
            {
                var y = InterpolateToGrid(c.X, c.Y, gridArr);
                interpolated.Add(y);
            }

            // 平均
            var avg = new double[gridArr.Length];
            var counts = new int[gridArr.Length];
            for (int i = 0; i < gridArr.Length; i++)
            {
                double sum = 0;
                int n = 0;
                foreach (var y in interpolated)
                {
                    if (!double.IsNaN(y[i]))
                    {
                        sum += y[i];
                        n++;
                    }
                }
                avg[i] = n > 0 ? sum / n : double.NaN;
                counts[i] = n;
            }

            return new Curve { X = gridArr, Y = avg, Label = label };
        }

        // ============================================================
        // 线性插值:把 (xs, ys) 插值到 grid 网格上
        // xs 必须按角度排序(0-360 内),网格外的返回 NaN
        // ============================================================
        private static double[] InterpolateToGrid(double[] xs, double[] ys, double[] grid)
        {
            var result = new double[grid.Length];

            // 排序副本
            var pairs = xs.Zip(ys, (x, y) => (x, y))
                          .OrderBy(p => p.x)
                          .ToList();

            for (int i = 0; i < grid.Length; i++)
            {
                double gx = grid[i];

                // 找 gx 在 pairs 里的位置
                int idx = pairs.FindIndex(p => p.x >= gx);
                if (idx < 0 || idx >= pairs.Count)
                {
                    // 超出范围
                    result[i] = double.NaN;
                    continue;
                }
                if (idx == 0)
                {
                    // gx 小于最小值
                    if (Math.Abs(pairs[0].x - gx) < 0.01) result[i] = pairs[0].y;
                    else result[i] = double.NaN;
                    continue;
                }

                // 在 pairs[idx-1] 和 pairs[idx] 之间插值
                var (x1, y1) = pairs[idx - 1];
                var (x2, y2) = pairs[idx];
                if (Math.Abs(x2 - x1) < 1e-9)
                {
                    result[i] = y1;
                }
                else
                {
                    var t = (gx - x1) / (x2 - x1);
                    result[i] = y1 + t * (y2 - y1);
                }
            }

            return result;
        }

        // ============================================================
        // 辅助:角度差归一化到 0-90(考虑周期性)
        // 例:150° 和 30° 的差,180° 周期下等价 30°
        // ============================================================
        private static double NormalizeAngleDiff(double diff)
        {
            diff = Math.Abs(diff) % 180;
            if (diff > 90) diff = 180 - diff;
            return diff;
        }

        // ============================================================
        // 应用 offset,并归一化到 0-360
        // ============================================================
        private static double ApplyOffset(double angle)
        {
            var v = angle + AngleOffset;
            v = v % 360;
            if (v < 0) v += 360;
            return v;
        }

        // ============================================================
        // 从 JSON 里读 double
        // ============================================================
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