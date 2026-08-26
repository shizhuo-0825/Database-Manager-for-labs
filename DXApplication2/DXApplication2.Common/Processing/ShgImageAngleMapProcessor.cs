using DXApplication2.Common.Data;
using DXApplication2.Common.IO;
using DXApplication2.Common.Models;
using Microsoft.EntityFrameworkCore;
using System;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// SHG Image 的"极大值角度"筛法:
    /// - 只用 co polar records(|Analyzer - Polarizer| < 45°)
    /// - 用每个 record 的 output .asc(processed (S-B)/B)
    /// - 每个像素:遍历所有 record,找信号最大的那个,记录对应 Polarizer 角度
    /// - 输出:角度矩阵,颜色 = 2 × angle mod 360
    /// - Phase colormap 显示
    /// </summary>
    public class ShgImageAngleMapProcessor : IDataProcessor
    {
        public string SourceType => "SHGImage_AngleMap";

        public const string PolarizerField = "Probe_P_Polarizer";
        public const string AnalyzerField = "Probe_P_Analyzer";
        public const double CoCrossThreshold = 45.0;

        public async Task<ProcessedData> ProcessAsync(ProcessingContext context, CancellationToken ct = default)
        {
            if (context.Records.Count == 0)
                return new ProcessedData { SourceType = SourceType };

            // 1. 过滤:必须有 output/.asc 存在(ResultPath) + Polarizer + Analyzer + co polar
            var candidates = new List<(DataRecord Rec, double Polarizer, string AscPath)>();
            foreach (var r in context.Records)
            {
                if (string.IsNullOrEmpty(r.ResultPath)) continue;
                if (!File.Exists(r.ResultPath)) continue;

                var pol = TryGetDouble(r.ExptParams, PolarizerField);
                var ana = TryGetDouble(r.ExptParams, AnalyzerField);
                if (!pol.HasValue || !ana.HasValue) continue;

                // Co polar 过滤
                double diff = Math.Abs(pol.Value - ana.Value) % 180;
                if (diff > 90) diff = 180 - diff;
                if (diff > CoCrossThreshold) continue;

                candidates.Add((r, pol.Value, r.ResultPath));
            }

            if (candidates.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Not enough co-polar records with processed output. Got {candidates.Count}, need at least 2. " +
                    "Make sure SHGImage records are processed first (Default Process).");
            }

            // 2. 读第一个 asc 确定尺寸
            var first = await AscMatrixReader.ReadAsync(candidates[0].AscPath);
            int rows = first.GetLength(0);
            int cols = first.GetLength(1);

            // 3. 初始化两个矩阵
            var maxValue = new double[rows, cols];
            var maxAngle = new double[rows, cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    maxValue[i, j] = double.NegativeInfinity;

            // 4. 遍历所有候选,逐 pixel 比较,更新最大值和对应角度
            int processedCount = 0;
            foreach (var c in candidates)
            {
                ct.ThrowIfCancellationRequested();
                processedCount++;
                context.Progress?.Report((processedCount, candidates.Count,
                    $"Processing record #{c.Rec.Id}"));
                double[,] mat;
                if (c.AscPath == candidates[0].AscPath)
                    mat = first;
                else
                    mat = await AscMatrixReader.ReadAsync(c.AscPath);

                if (mat.GetLength(0) != rows || mat.GetLength(1) != cols)
                {
                    throw new InvalidOperationException(
                        $"Record #{c.Rec.Id} image size ({mat.GetLength(0)} × {mat.GetLength(1)}) " +
                        $"doesn't match first record ({rows} × {cols}). All records must have same size.");
                }

                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        var v = mat[i, j];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        if (v > maxValue[i, j])
                        {
                            maxValue[i, j] = v;
                            maxAngle[i, j] = c.Polarizer;
                        }
                    }
                }
            }

            // 5. 生成显示矩阵:2 × angle mod 360
            var displayMatrix = new double[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (double.IsNegativeInfinity(maxValue[i, j]))
                    {
                        displayMatrix[i, j] = double.NaN;
                    }
                    else
                    {
                        double a = (maxAngle[i, j]) % 180;
                        if (a < 0) a += 180;
                        displayMatrix[i, j] = a;
                    }
                }
            }

            // 6. 生成 Heatmap X/Y 值(用像素索引)
            var xs = new double[cols];
            var ys = new double[rows];
            for (int j = 0; j < cols; j++) xs[j] = j;
            for (int i = 0; i < rows; i++) ys[i] = i;

            return new ProcessedData
            {
                SourceType = SourceType,
                Matrix = displayMatrix,
                HeatmapXValues = xs,
                HeatmapYValues = ys,
                XLabel = "Column (pixel)",
                YLabel = "Row (pixel)",
                ZLabel = "Angle (mod 180°)",
                ZMin = 0,
                ZMax = 180,
            };
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