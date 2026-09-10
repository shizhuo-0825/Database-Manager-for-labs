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
    /// Heatmap 处理器:
    /// - X 轴:用户指定,兜底 = Line Plot 的默认 X
    /// - Y 轴:用户指定,兜底 = 方差最大的跨 Group/跨分组的字段
    /// - Z 轴:用户指定,兜底 = 当前 Source 的默认 Y(比如 DRR = LockinValueX)
    /// - 输出:Matrix[y, x] + HeatmapXValues[x] + HeatmapYValues[y]
    /// - Y 方差=0 时抛异常(UI 层弹错误)
    /// </summary>
    


    public class HeatmapProcessor : IDataProcessor
    {
        private class HeatmapVertex : MIConvexHull.IVertex
        {
            public double[] Position { get; }
            public double Z { get; }
            public HeatmapVertex(double xNorm, double yNorm, double z)
            {
                Position = new[] { xNorm, yNorm };
                Z = z;
            }
        }

        private static void FillMissingCells(
            double[,] matrix, List<double> uniqueX, List<double> uniqueY)
        {
            int ny = uniqueY.Count, nx = uniqueX.Count;

            // 归一化到 [0,1],避免各轴单位差把三角形拉扁
            double xMin = uniqueX.Min(), xMax = uniqueX.Max();
            double yMin = uniqueY.Min(), yMax = uniqueY.Max();
            double xRange = xMax - xMin, yRange = yMax - yMin;
            if (xRange < 1e-15 || yRange < 1e-15) return;
            double Nx(double x) => (x - xMin) / xRange;
            double Ny(double y) => (y - yMin) / yRange;

            // 收集已知点
            var known = new List<HeatmapVertex>();
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                    if (!double.IsNaN(matrix[i, j]))
                        known.Add(new HeatmapVertex(Nx(uniqueX[j]), Ny(uniqueY[i]), matrix[i, j]));

            if (known.Count < 3) { NearestNeighborFillAll(matrix, uniqueX, uniqueY, known, Nx, Ny); return; }

            // Delaunay,失败(共线/退化)时全用最近邻兜底
            List<MIConvexHull.DefaultTriangulationCell<HeatmapVertex>> triangles;
            try
            {
                var tri = MIConvexHull.Triangulation
                    .CreateDelaunay<HeatmapVertex, MIConvexHull.DefaultTriangulationCell<HeatmapVertex>>(known);
                triangles = tri.Cells.ToList();
            }
            catch
            {
                NearestNeighborFillAll(matrix, uniqueX, uniqueY, known, Nx, Ny);
                return;
            }

            // 逐 NaN 单元填
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                {
                    if (!double.IsNaN(matrix[i, j])) continue;
                    double px = Nx(uniqueX[j]), py = Ny(uniqueY[i]);

                    var interp = TryInterpolate(px, py, triangles);
                    matrix[i, j] = interp ?? NearestZ(px, py, known);
                }
        }

        private static double? TryInterpolate(
            double px, double py,
            List<MIConvexHull.DefaultTriangulationCell<HeatmapVertex>> triangles)
        {
            const double eps = 1e-9;
            foreach (var t in triangles)
            {
                var a = t.Vertices[0]; var b = t.Vertices[1]; var c = t.Vertices[2];
                double ax = a.Position[0], ay = a.Position[1];
                double bx = b.Position[0], by = b.Position[1];
                double cx = c.Position[0], cy = c.Position[1];
                double denom = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                if (Math.Abs(denom) < 1e-15) continue;
                double u = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / denom;
                double v = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / denom;
                double w = 1.0 - u - v;
                if (u >= -eps && v >= -eps && w >= -eps)
                    return u * a.Z + v * b.Z + w * c.Z;
            }
            return null;
        }

        private static double NearestZ(double px, double py, List<HeatmapVertex> pts)
        {
            double bestD2 = double.MaxValue, bestZ = 0;
            foreach (var p in pts)
            {
                double dx = p.Position[0] - px, dy = p.Position[1] - py;
                double d2 = dx * dx + dy * dy;
                if (d2 < bestD2) { bestD2 = d2; bestZ = p.Z; }
            }
            return bestZ;
        }

        private static void NearestNeighborFillAll(
            double[,] matrix, List<double> uniqueX, List<double> uniqueY,
            List<HeatmapVertex> known, Func<double, double> Nx, Func<double, double> Ny)
        {
            if (known.Count == 0) return;
            for (int i = 0; i < uniqueY.Count; i++)
                for (int j = 0; j < uniqueX.Count; j++)
                {
                    if (!double.IsNaN(matrix[i, j])) continue;
                    matrix[i, j] = NearestZ(Nx(uniqueX[j]), Ny(uniqueY[i]), known);
                }
        }
        public string SourceType => "Heatmap";
        private const double BinTolerance = 0.01;
        private static double Snap(double v) => Math.Round(v / BinTolerance) * BinTolerance;
        public async Task<ProcessedData> ProcessAsync(ProcessingContext context, CancellationToken ct = default)
        {
            var bgConfig = DXApplication2.Common.Background.BackgroundStore.Load();
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
                .ToListAsync(ct);

            // 决定 X / Y / Z 字段
            var xFieldName = context.XAxisFieldName ?? DetermineDefaultX(groups, allParams, context.Records);
            var yFieldName = context.YAxisFieldName ?? DetermineDefaultY(context.Records, xFieldName);
            var zFieldName = context.ZAxisFieldName ?? DetermineDefaultZ(groups);

            // 验证 Y 变化足够(方差>0)
            var yVals = ExtractValues(context.Records, yFieldName).Distinct().ToList();
            if (yVals.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Y axis '{yFieldName}' has only {yVals.Count} unique value(s). Not enough for heatmap.");
            }

            // 构建 Y 值排序列表
            var uniqueY = yVals.OrderBy(v => v).ToList();

            // 构建 X 值排序列表(合并所有 Records 的 X 值)
            var allXValues = new SortedSet<double>();
            foreach (var r in context.Records)
            {
                var parsed = ParseJson(r.ExptParams);
                if (parsed.TryGetValue(xFieldName, out var s) &&
                    double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                {
                    allXValues.Add(Snap(v));
                }
            }
            var uniqueX = allXValues.ToList();

            if (uniqueX.Count < 2)
            {
                throw new InvalidOperationException(
                    $"X axis '{xFieldName}' has only {uniqueX.Count} unique value(s). Not enough for heatmap.");
            }

            // 构建 Matrix[y, x]
            var matrix = new double[uniqueY.Count, uniqueX.Count];
            for (int i = 0; i < uniqueY.Count; i++)
                for (int j = 0; j < uniqueX.Count; j++)
                    matrix[i, j] = double.NaN;   // 默认缺失

            double minZ = double.MaxValue, maxZ = double.MinValue;
            int filledCount = 0;

            // 填数据
            foreach (var rec in context.Records)
            {
                var parsed = ParseJson(rec.ExptParams);
                if (!TryGetDouble(parsed, xFieldName, out var xVal)) continue;
                if (!TryGetDouble(parsed, yFieldName, out var yVal)) continue;

                double zVal;
                if (zFieldName == FieldOptionsService.ResultFieldName)
                {
                    if (!rec.ResultQuantity.HasValue) continue;
                    zVal = DXApplication2.Common.Background.PumpSubtractionHelper.ApplySubtraction(
                        rec, rec.ResultQuantity.Value, bgConfig);
                }
                else
                {
                    if (!TryGetDouble(parsed, zFieldName, out zVal)) continue;
                }

                int xIdx = uniqueX.IndexOf(Snap(xVal));
                int yIdx = uniqueY.IndexOf(Snap(yVal));
                if (xIdx < 0 || yIdx < 0) continue;

                matrix[yIdx, xIdx] = zVal;
                if (zVal < minZ) minZ = zVal;
                if (zVal > maxZ) maxZ = zVal;
                filledCount++;
            }

            if (filledCount == 0)
            {
                throw new InvalidOperationException("No valid Z data found for heatmap.");
            }
            FillMissingCells(matrix, uniqueX, uniqueY);
            return new ProcessedData
            {
                SourceType = SourceType,
                Matrix = matrix,
                HeatmapXValues = uniqueX.ToArray(),
                HeatmapYValues = uniqueY.ToArray(),
                XLabel = FormatFieldLabel(xFieldName, allParams),
                YLabel = FormatFieldLabel(yFieldName, allParams),
                ZLabel = FormatFieldLabel(zFieldName, allParams),
                ZMin = minZ,
                ZMax = maxZ,
            };
        }

        // ============================================================
        // 默认 X:用 DrrProcessor / DefaultProcessor 相同规则
        // ============================================================
        private static string DetermineDefaultX(List<DataGroup> groups, List<ExperimentParams> allParams, List<DataRecord> records)
        {
            var sourceTypes = groups
                .SelectMany(g => g.GroupExperimentTypes)
                .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                .Select(get => get.ExperimentType.Name)
                .Distinct()
                .ToList();

            // DRR
            if (sourceTypes.Count == 1 && sourceTypes[0] == "DRR")
                return DrrProcessor.DefaultXFieldName;

            // 兜底:Method/Pump 关联 Params Id 最小
            var group = groups.First();
            var methodPumpTypeIds = group.GroupExperimentTypes
                .Where(get => get.ExperimentType != null)
                .Where(get => get.ExperimentType.Category == "Method" || get.ExperimentType.Category == "Pump")
                .Select(get => get.ExperimentType.Id)
                .ToHashSet();

            var candidate = allParams
                .Where(p => p.ExperimentTypeId.HasValue && methodPumpTypeIds.Contains(p.ExperimentTypeId.Value))
                .OrderBy(p => p.Id)
                .FirstOrDefault();

            if (candidate != null) return candidate.FieldName;

            // 最兜底:JSON 里第一个数值字段
            var parsed = ParseJson(records[0].ExptParams);
            foreach (var kvp in parsed)
            {
                if (double.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    return kvp.Key;
            }
            return "";
        }

        // ============================================================
        // 默认 Y:方差最大的字段(排除 X 字段)
        // ============================================================
        private static string DetermineDefaultY(List<DataRecord> records, string xFieldName)
        {
            // 收集每个字段的所有值
            var fieldValues = new Dictionary<string, List<double>>();
            foreach (var r in records)
            {
                var parsed = ParseJson(r.ExptParams);
                foreach (var kvp in parsed)
                {
                    if (kvp.Key == xFieldName) continue;
                    if (!double.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) continue;

                    if (!fieldValues.TryGetValue(kvp.Key, out var list))
                    {
                        list = new List<double>();
                        fieldValues[kvp.Key] = list;
                    }
                    list.Add(v);
                }
            }

            // 计算标准化方差(除以平均值,避免单位差)
            string bestField = "";
            double bestScore = -1;
            foreach (var kvp in fieldValues)
            {
                var values = kvp.Value;
                if (values.Count < 2) continue;
                var distinct = values.Distinct().Count();
                if (distinct < 2) continue;

                var mean = values.Average();
                if (Math.Abs(mean) < 1e-12) continue;
                var variance = values.Select(v => (v - mean) * (v - mean)).Average();
                var normalized = Math.Sqrt(variance) / Math.Abs(mean);

                if (normalized > bestScore)
                {
                    bestScore = normalized;
                    bestField = kvp.Key;
                }
            }

            return bestField;
        }

        // ============================================================
        // 默认 Z:根据 Source 类型
        // ============================================================
        private static string DetermineDefaultZ(List<DataGroup> groups)
        {
            var sourceTypes = groups
                .SelectMany(g => g.GroupExperimentTypes)
                .Where(get => get.ExperimentType != null && get.ExperimentType.Category == "Source")
                .Select(get => get.ExperimentType.Name)
                .Distinct()
                .ToList();

            if (sourceTypes.Count == 1 && sourceTypes[0] == "DRR")
                return DrrProcessor.DefaultYFieldName;

            return FieldOptionsService.ResultFieldName;   // 兜底 Result
        }

        // ============================================================
        // 辅助
        // ============================================================
        private static IEnumerable<double> ExtractValues(List<DataRecord> records, string fieldName)
        {
            foreach (var r in records)
            {
                var parsed = ParseJson(r.ExptParams);
                if (TryGetDouble(parsed, fieldName, out var v))
                    yield return Snap(v);
            }
        }

        private static bool TryGetDouble(Dictionary<string, string> parsed, string field, out double value)
        {
            value = 0;
            if (!parsed.TryGetValue(field, out var s)) return false;
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
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