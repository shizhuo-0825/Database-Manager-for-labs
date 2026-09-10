using DXApplication2.Common.IO;
using DXApplication2.Common.Processing;
using ScottPlot;
using System;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class PolarHeatmapRenderer : IPlotRenderer
    {
        public string PlotType => "PolarHeatmap";
        private const int Resolution = 512;

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            foreach (var p in plot.Axes.GetPanels().ToList())
                if (p is ScottPlot.Panels.ColorBar) plot.Axes.Remove(p);

            if (data.Matrix == null || data.HeatmapXValues == null || data.HeatmapYValues == null ||
                data.HeatmapXValues.Length < 2 || data.HeatmapYValues.Length < 2) return;

            // 约定:X = θ (角度,度),Y = R (半径)。Matrix[y, x] = Matrix[R_idx, θ_idx]
            var radius = data.HeatmapXValues;
            var angleDeg = data.HeatmapYValues;
            var mat = Transpose(data.Matrix);  // 转成 mat[radius_idx, angle_idx]

            double rMin = radius[0], rMax = radius[^1];
            double zMin = config.ZMinOverride ?? data.ZMin ?? 0;
            double zMax = config.ZMaxOverride ?? data.ZMax ?? 1;
            var colormap = ColormapResolver.Resolve(config.Colormap);

            var cart = RasterizePolar(mat, radius, angleDeg, rMin, rMax);

            var heatmap = plot.Add.Heatmap(cart);
            heatmap.Colormap = colormap;
            heatmap.ManualRange = new ScottPlot.Range((float)zMin, (float)zMax);
            // Smooth 保持关闭 -> Origin 风格锐利阶梯
            heatmap.Extent = new CoordinateRect(-rMax, rMax, -rMax, rMax);

            plot.Axes.SquareUnits();   // 保持圆形不变形

            try
            {
                var cbar = plot.Add.ColorBar(heatmap);
                cbar.Label = data.ZLabel;
            }
            catch { }

            // 极坐标下轴标题
            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
            PolarAxes.Configure(plot, radius[0], radius[^1], rLabel: $"{data.XLabel}");
        }

        private static double[,] RasterizePolar(
            double[,] mat, double[] radius, double[] angleDeg, double rMin, double rMax)
        {
            int N = Resolution;
            var cart = new double[N, N];
            double half = (N - 1) / 2.0;
            double rRange = rMax - rMin;

            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    double dx = (j - half) / half;
                    double dy = (half - i) / half;
                    double rNorm = Math.Sqrt(dx * dx + dy * dy);
                    if (rNorm > 1.0) { cart[i, j] = double.NaN; continue; }  // 圆外透明

                    double r = rMin + rNorm * rRange;
                    double theta = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (theta < 0) theta += 360;

                    int ri = NearestIndex(radius, r);
                    int ai = WrappedAngleIndex(theta, angleDeg);
                    cart[i, j] = mat[ri, ai];
                }
            }
            return cart;
        }

        // 源范围 < 200° 视为 180-周期(polarizer 常见),否则 360-周期。
        private static int WrappedAngleIndex(double theta, double[] angleDeg)
        {
            double srcMin = angleDeg[0], srcMax = angleDeg[^1];
            double range = srcMax - srcMin;
            double period = (range < 200) ? 180.0 : 360.0;
            double offset = theta - srcMin;
            offset = ((offset % period) + period) % period;
            double wrapped = srcMin + Math.Min(offset, range);
            return NearestIndex(angleDeg, wrapped);
        }

        private static int NearestIndex(double[] arr, double v)
        {
            if (v <= arr[0]) return 0;
            if (v >= arr[^1]) return arr.Length - 1;
            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int m = (lo + hi) >> 1;
                if (arr[m] <= v) lo = m; else hi = m;
            }
            return (v - arr[lo] < arr[hi] - v) ? lo : hi;
        }
        private static double[,] Transpose(double[,] m)
        {
            int r0 = m.GetLength(0), c0 = m.GetLength(1);
            var t = new double[c0, r0];
            for (int i = 0; i < r0; i++)
                for (int j = 0; j < c0; j++)
                    t[j, i] = m[i, j];
            return t;
        }
    }
}