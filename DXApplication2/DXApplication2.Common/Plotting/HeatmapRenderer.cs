using DXApplication2.Common.IO;
using DXApplication2.Common.Processing;
using ScottPlot;
using System;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class HeatmapRenderer : IPlotRenderer
    {
        public string PlotType => "Heatmap";

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            var panels = plot.Axes.GetPanels().ToList();
            foreach (var panel in panels)
            {
                if (panel is ScottPlot.Panels.ColorBar)
                    plot.Axes.Remove(panel);
            }

            if (data.Matrix == null ||
                data.HeatmapXValues == null ||
                data.HeatmapYValues == null ||
                data.HeatmapXValues.Length < 2 ||
                data.HeatmapYValues.Length < 2)
            {
                return;
            }

            var xVals = data.HeatmapXValues;
            var yVals = data.HeatmapYValues;
            var matrix = data.Matrix;

            var zMin = config.ZMinOverride ?? data.ZMin ?? 0;
            var zMax = config.ZMaxOverride ?? data.ZMax ?? 1;

            var colormap = ColormapResolver.Resolve(config.Colormap);

            // 等间距(SHGImage pixel 索引等)用内置 Heatmap;
            // 不等间距(通用参数扫描)用手动矩形。
            if (IsUniformSpacing(xVals) && IsUniformSpacing(yVals))
            {
                RenderUniform(plot, matrix, xVals, yVals, zMin, zMax, colormap, data.ZLabel);
            }
            else
            {
                RenderNonUniform(plot, matrix, xVals, yVals, zMin, zMax, colormap, data.ZLabel);
            }

            var xLabel = !string.IsNullOrEmpty(config.XLabelOverride) ? config.XLabelOverride : data.XLabel;
            var yLabel = !string.IsNullOrEmpty(config.YLabelOverride) ? config.YLabelOverride : data.YLabel;
            if (!string.IsNullOrEmpty(xLabel)) plot.Axes.Bottom.Label.Text = xLabel;
            if (!string.IsNullOrEmpty(yLabel)) plot.Axes.Left.Label.Text = yLabel;

            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
        }

        /// <summary>
        /// 等间距:直接用 ScottPlot 内置 Heatmap
        /// </summary>
        private static void RenderUniform(
            Plot plot, double[,] matrix, double[] xVals, double[] yVals,
            double zMin, double zMax, IColormap colormap, string zLabel)
        {
            var heatmap = plot.Add.Heatmap(MatrixFlipHelper.FlipY(matrix));
            heatmap.Colormap = colormap;
            heatmap.ManualRange = new ScottPlot.Range((float)zMin, (float)zMax);

            // cell 边界 = 中心 ± 半间距
            double dx = xVals[1] - xVals[0];
            double dy = yVals[1] - yVals[0];
            double xLeft = xVals[0] - dx / 2.0;
            double xRight = xVals[xVals.Length - 1] + dx / 2.0;
            double yBottom = yVals[0] - dy / 2.0;
            double yTop = yVals[yVals.Length - 1] + dy / 2.0;

            heatmap.Extent = new CoordinateRect(xLeft, xRight, yBottom, yTop);

            try
            {
                var cbar = plot.Add.ColorBar(heatmap);
                cbar.Label = zLabel;
            }
            catch { }
        }

        /// <summary>
        /// 不等间距:手动画矩形(原方案,保留兼容)
        /// </summary>
        private static void RenderNonUniform(
            Plot plot, double[,] matrix, double[] xVals, double[] yVals,
            double zMin, double zMax, IColormap colormap, string zLabel)
        {
            var xEdges = ComputeEdges(xVals);
            var yEdges = ComputeEdges(yVals);

            for (int i = 0; i < yVals.Length; i++)
            {
                for (int j = 0; j < xVals.Length; j++)
                {
                    var z = matrix[i, j];
                    if (double.IsNaN(z)) continue;

                    var normalized = (z - zMin) / (zMax - zMin);
                    if (double.IsNaN(normalized) || double.IsInfinity(normalized)) normalized = 0;
                    normalized = Math.Clamp(normalized, 0, 1);

                    var color = colormap.GetColor(normalized);

                    double dx = xEdges[j + 1] - xEdges[j];
                    double dy = yEdges[i + 1] - yEdges[i];
                    double epsX = Math.Max(dx * 0.001, 1e-10);
                    double epsY = Math.Max(dy * 0.001, 1e-10);

                    var rect = plot.Add.Rectangle(
                        xEdges[j] - epsX,
                        xEdges[j + 1] + epsX,
                        yEdges[i] - epsY,
                        yEdges[i + 1] + epsY);
                    rect.FillStyle.Color = color;
                    rect.LineStyle.Width = 0;
                    rect.LineStyle.Color = Colors.Transparent;
                }
            }

            // Colorbar:用隐藏的 heatmap 承载
            try
            {
                var dummyMatrix = new double[,] { { zMin, zMax } };
                var heatmap = plot.Add.Heatmap(dummyMatrix);
                heatmap.Colormap = colormap;
                heatmap.IsVisible = false;
                var cbar = plot.Add.ColorBar(heatmap);
                cbar.Label = zLabel;
            }
            catch { }
        }

        /// <summary>
        /// 判断数组是否等间距(相对容差)
        /// </summary>
        private static bool IsUniformSpacing(double[] values, double tolerance = 1e-6)
        {
            if (values.Length < 3) return true;
            double firstDiff = values[1] - values[0];
            if (Math.Abs(firstDiff) < 1e-12) return false;
            for (int i = 1; i < values.Length - 1; i++)
            {
                double diff = values[i + 1] - values[i];
                if (Math.Abs((diff - firstDiff) / firstDiff) > tolerance) return false;
            }
            return true;
        }

        private static double[] ComputeEdges(double[] centers)
        {
            var n = centers.Length;
            var edges = new double[n + 1];
            for (int i = 0; i < n - 1; i++)
                edges[i + 1] = (centers[i] + centers[i + 1]) / 2.0;
            edges[0] = centers[0] - (edges[1] - centers[0]);
            edges[n] = centers[n - 1] + (centers[n - 1] - edges[n - 1]);
            return edges;
        }
    }
}