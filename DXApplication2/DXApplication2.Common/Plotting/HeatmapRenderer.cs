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

        // 重采样目标分辨率。256 对大多数展示都够,数据点上千也不慢。
        private const int ResampleWidth = 256;
        private const int ResampleHeight = 256;

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

            // 均匀数据可以直接用原矩阵;非均匀则 Delaunay 重采样。
            // 两条路都最终交给内置 Heatmap 渲染,开 Smooth 让缩放平滑。
            double[,] uniformMatrix;
            if (IsUniformSpacing(xVals) && IsUniformSpacing(yVals))
            {
                uniformMatrix = matrix;
            }
            else
            {
                uniformMatrix = HeatmapInterpolator.ResampleToUniform(
                    matrix, xVals, yVals, ResampleWidth, ResampleHeight);
            }

            RenderHeatmap(plot, uniformMatrix, xVals, yVals, zMin, zMax, colormap, data.ZLabel);

            var xLabel = !string.IsNullOrEmpty(config.XLabelOverride) ? config.XLabelOverride : data.XLabel;
            var yLabel = !string.IsNullOrEmpty(config.YLabelOverride) ? config.YLabelOverride : data.YLabel;
            if (!string.IsNullOrEmpty(xLabel)) plot.Axes.Bottom.Label.Text = xLabel;
            if (!string.IsNullOrEmpty(yLabel)) plot.Axes.Left.Label.Text = yLabel;

            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
        }

        private static void RenderHeatmap(
            Plot plot, double[,] matrix, double[] xVals, double[] yVals,
            double zMin, double zMax, IColormap colormap, string zLabel)
        {
            var heatmap = plot.Add.Heatmap(MatrixFlipHelper.FlipY(matrix));
            heatmap.Colormap = colormap;
            heatmap.ManualRange = new ScottPlot.Range((float)zMin, (float)zMax);

            // Extent 用原始 xVals / yVals 的边界,保证坐标轴仍显示实际物理量
            double xLeft, xRight, yBottom, yTop;
            if (xVals.Length >= 2 && IsUniformSpacing(xVals))
            {
                double dx = xVals[1] - xVals[0];
                xLeft = xVals[0] - dx / 2.0;
                xRight = xVals[^1] + dx / 2.0;
            }
            else
            {
                xLeft = xVals[0];
                xRight = xVals[^1];
            }
            if (yVals.Length >= 2 && IsUniformSpacing(yVals))
            {
                double dy = yVals[1] - yVals[0];
                yBottom = yVals[0] - dy / 2.0;
                yTop = yVals[^1] + dy / 2.0;
            }
            else
            {
                yBottom = yVals[0];
                yTop = yVals[^1];
            }
            heatmap.Extent = new CoordinateRect(xLeft, xRight, yBottom, yTop);

            try
            {
                var cbar = plot.Add.ColorBar(heatmap);
                cbar.Label = zLabel;
            }
            catch { }
        }

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
    }
}