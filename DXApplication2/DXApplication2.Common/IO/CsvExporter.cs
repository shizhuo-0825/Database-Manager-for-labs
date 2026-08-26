using DXApplication2.Common.Background;
using DXApplication2.Common.Preprocessing;
using DXApplication2.Common.Processing;
using DXApplication2.Common.Roi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DXApplication2.Common.IO
{
    /// <summary>
    /// 把 Plot 的数据 + 元信息导出为 CSV。
    /// UTF-8 with BOM,#开头是元数据注释。
    /// </summary>
    public static class CsvExporter
    {
        public static void Export(string filePath, ProcessedData data, string plotType, int? recordCount = null)
        {
            var sb = new StringBuilder();

            // === 头部元信息(注释) ===
            sb.AppendLine($"# Plot type: {plotType}");
            sb.AppendLine($"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (recordCount.HasValue)
                sb.AppendLine($"# Records: {recordCount.Value}");
            if (!string.IsNullOrEmpty(data.SourceType))
                sb.AppendLine($"# Source type: {data.SourceType}");
            if (!string.IsNullOrEmpty(data.XLabel))
                sb.AppendLine($"# X label: {data.XLabel}");
            if (!string.IsNullOrEmpty(data.YLabel))
                sb.AppendLine($"# Y label: {data.YLabel}");
            if (!string.IsNullOrEmpty(data.ZLabel))
                sb.AppendLine($"# Z label: {data.ZLabel}");

            // Preprocessing
            var pp = PreprocessingConfigStore.Load();
            sb.AppendLine($"# Preprocessing: HotspotSize={pp.HotspotSize}, HotspotRatio={pp.HotspotRatio}, HardThreshold={pp.HardThreshold?.ToString(CultureInfo.InvariantCulture) ?? "off"}, GaussianSigma={pp.GaussianSigma}");

            // ROI
            var pshgTs = RoiStore.ListTimestamps(RoiTags.PSHG);
            var bgTs = RoiStore.ListTimestamps(RoiTags.Background);
            if (pshgTs.Count > 0)
            {
                var pshg = RoiStore.Load(RoiTags.PSHG, pshgTs[0]);
                if (pshg != null)
                    sb.AppendLine($"# PSHG ROI: [row {pshg.RowStart}-{pshg.RowEnd}, col {pshg.ColStart}-{pshg.ColEnd}] @ {pshg.Timestamp}");
            }
            if (bgTs.Count > 0)
            {
                var bg = RoiStore.Load(RoiTags.Background, bgTs[0]);
                if (bg != null)
                    sb.AppendLine($"# Background ROI: [row {bg.RowStart}-{bg.RowEnd}, col {bg.ColStart}-{bg.ColEnd}] @ {bg.Timestamp}");
            }

            // Pump background
            var bgConfig = BackgroundStore.Load();
            if (bgConfig.ApplyToPshgRaw && bgConfig.Pump != null && bgConfig.Pump.IsFitted)
            {
                var p = bgConfig.Pump;
                var bkStr = string.Join(",", p.FitBk.Select(v => v.ToString("G4", CultureInfo.InvariantCulture)));
                var phiStr = string.Join(",", p.FitPhik.Select(v => v.ToString("G4", CultureInfo.InvariantCulture)));
                sb.AppendLine($"# Pump BG: Apply=True, Scale={bgConfig.PumpScale}, A={p.FitA:G4}, Bk=[{bkStr}], Phik=[{phiStr}]");
            }
            else if (bgConfig.ApplyToPshgRaw)
            {
                sb.AppendLine($"# Pump BG: Apply=True (but not fitted or unavailable)");
            }
            else
            {
                sb.AppendLine($"# Pump BG: Apply=False");
            }

            sb.AppendLine("#");   // 分隔

            // === 数据 ===
            if (data.Matrix != null && data.HeatmapXValues != null && data.HeatmapYValues != null)
            {
                // Heatmap:矩阵形式
                WriteHeatmapMatrix(sb, data);
            }
            else if (data.Curves.Count > 0)
            {
                // 多条 curve:各 curve 的 X,Y 依次导出
                WriteCurves(sb, data.Curves, data.XLabel, data.YLabel);
            }
            else if (data.X != null && data.Y != null)
            {
                // 单曲线
                WriteSingleXY(sb, data.X, data.Y, data.XLabel, data.YLabel);
            }
            else
            {
                sb.AppendLine("# No data.");
            }

            // === 写入(UTF-8 BOM) ===
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        private static void WriteHeatmapMatrix(StringBuilder sb, ProcessedData data)
        {
            var xs = data.HeatmapXValues!;
            var ys = data.HeatmapYValues!;
            var m = data.Matrix!;

            // 第 1 行:空 + X 值
            sb.Append(",");
            sb.AppendLine(string.Join(",", xs.Select(v => v.ToString("G6", CultureInfo.InvariantCulture))));

            // 后续每行:Y 值 + 该行 Z 数据
            for (int i = 0; i < ys.Length; i++)
            {
                sb.Append(ys[i].ToString("G6", CultureInfo.InvariantCulture));
                for (int j = 0; j < xs.Length; j++)
                {
                    var z = m[i, j];
                    sb.Append(",");
                    if (double.IsNaN(z)) sb.Append("");
                    else sb.Append(z.ToString("G6", CultureInfo.InvariantCulture));
                }
                sb.AppendLine();
            }
        }

        private static void WriteCurves(StringBuilder sb, List<Curve> curves, string xLabel, string yLabel)
        {
            // 每条 curve 单独一段(因为 X 可能不同)
            // 格式:一条 curve 一个 block,以空行分隔
            for (int c = 0; c < curves.Count; c++)
            {
                var curve = curves[c];
                sb.AppendLine($"# Curve {c + 1}: {curve.Label}");
                sb.AppendLine($"{Escape(xLabel)},{Escape(curve.Label)}");
                int n = System.Math.Min(curve.X.Length, curve.Y.Length);
                for (int i = 0; i < n; i++)
                {
                    sb.Append(curve.X[i].ToString("G6", CultureInfo.InvariantCulture));
                    sb.Append(",");
                    var y = curve.Y[i];
                    if (double.IsNaN(y)) sb.AppendLine("");
                    else sb.AppendLine(y.ToString("G6", CultureInfo.InvariantCulture));
                }
                sb.AppendLine();
            }
        }

        private static void WriteSingleXY(StringBuilder sb, double[] x, double[] y, string xLabel, string yLabel)
        {
            sb.AppendLine($"{Escape(xLabel)},{Escape(yLabel)}");
            int n = System.Math.Min(x.Length, y.Length);
            for (int i = 0; i < n; i++)
            {
                sb.Append(x[i].ToString("G6", CultureInfo.InvariantCulture));
                sb.Append(",");
                var yv = y[i];
                if (double.IsNaN(yv)) sb.AppendLine("");
                else sb.AppendLine(yv.ToString("G6", CultureInfo.InvariantCulture));
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }
}