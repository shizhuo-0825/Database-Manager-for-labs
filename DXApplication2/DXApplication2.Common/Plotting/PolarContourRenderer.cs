using DXApplication2.Common.IO;
using DXApplication2.Common.Processing;
using ScottPlot;
using System;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class PolarContourRenderer : IPlotRenderer
    {
        public string PlotType => "PolarContour";
        private const int Resolution = 512;
        private const int DefaultLevels = 24;

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            foreach (var p in plot.Axes.GetPanels().ToList())
                if (p is ScottPlot.Panels.ColorBar) plot.Axes.Remove(p);

            if (data.Matrix == null || data.HeatmapXValues == null || data.HeatmapYValues == null ||
                data.HeatmapXValues.Length < 2 || data.HeatmapYValues.Length < 2) return;

            // 交换:X=r, Y=θ。数据 Matrix[y_idx, x_idx] = Matrix[angle_idx, radius_idx]
            var radius = data.HeatmapXValues;
            var angleDeg = data.HeatmapYValues;

            // 转置成 mat[radius_idx, angle_idx]
            var mat = Transpose(data.Matrix);

            double rMax = radius[^1];
            double zMin = config.ZMinOverride ?? data.ZMin ?? 0;
            double zMax = config.ZMaxOverride ?? data.ZMax ?? 1;
            var colormap = ColormapResolver.Resolve(config.Colormap);
            int levels = config.ContourLevels ?? DefaultLevels;

            // 1) 补 NaN
            var filled = ContourRenderer.FillNaN(mat, angleDeg, radius);  // 注意参数顺序:col=angle, row=radius

            // 2) 沿 θ 轴周期性扩展(左右各加一份)
            var (extMat, extAngles) = ExtendPeriodicOnTheta(filled, radius, angleDeg);

            // 3) 极坐标→cartesian 高分辨率 bilinear 光栅化
            var smooth = RasterizePolarBilinear(extMat, radius, extAngles);

            // 4) 对高分辨率结果 binning
            var quantized = ContourRenderer.Quantize(smooth, zMin, zMax, levels);

            var heatmap = plot.Add.Heatmap(quantized);
            heatmap.Colormap = colormap;
            heatmap.ManualRange = new ScottPlot.Range((float)zMin, (float)zMax);
            heatmap.Smooth = false;
            heatmap.Extent = new CoordinateRect(-rMax, rMax, -rMax, rMax);

            plot.Axes.SquareUnits();

            try { var cbar = plot.Add.ColorBar(heatmap); cbar.Label = data.ZLabel; } catch { }

            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
            PolarAxes.Configure(plot, radius[0], radius[^1], rLabel: $"{data.XLabel}");
        }

        private static (double[,] extMat, double[] extAngles) ExtendPeriodicOnTheta(
            double[,] mat, double[] rs, double[] angles)
        {
            int R = rs.Length;
            int A = angles.Length;
            double range = angles[^1] - angles[0];
            double period = (range < 200) ? 180.0 : 360.0;

            var extAngles = new double[3 * A];
            for (int k = 0; k < A; k++)
            {
                extAngles[k] = angles[k] - period;
                extAngles[k + A] = angles[k];
                extAngles[k + 2 * A] = angles[k] + period;
            }

            var extMat = new double[R, 3 * A];
            for (int i = 0; i < R; i++)
                for (int k = 0; k < A; k++)
                {
                    extMat[i, k] = mat[i, k];
                    extMat[i, k + A] = mat[i, k];
                    extMat[i, k + 2 * A] = mat[i, k];
                }
            return (extMat, extAngles);
        }

        private static double[,] RasterizePolarBilinear(double[,] mat, double[] radius, double[] extAngles)
        {
            int N = Resolution;
            var cart = new double[N, N];
            double half = (N - 1) / 2.0;
            double rMin = radius[0], rMax = radius[^1];

            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    double dx = (j - half) / half;
                    double dy = (half - i) / half;
                    double rNorm = Math.Sqrt(dx * dx + dy * dy);
                    if (rNorm > 1.0) { cart[i, j] = double.NaN; continue; }

                    double r = rMin + rNorm * (rMax - rMin);
                    double theta = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (theta < 0) theta += 360;
                    // θ 直接落在扩展 angles 范围里,不用 wrap

                    cart[i, j] = BilinearLookup(mat, radius, extAngles, r, theta);
                }
            }
            return cart;
        }

        private static double BilinearLookup(double[,] mat, double[] rs, double[] ths, double r, double th)
        {
            int ri = ContourRenderer.LowerBound(rs, r);
            int ai = ContourRenderer.LowerBound(ths, th);
            int ri1 = Math.Min(ri + 1, rs.Length - 1);
            int ai1 = Math.Min(ai + 1, ths.Length - 1);
            double tr = ri1 > ri ? Math.Clamp((r - rs[ri]) / (rs[ri1] - rs[ri]), 0, 1) : 0;
            double ta = ai1 > ai ? Math.Clamp((th - ths[ai]) / (ths[ai1] - ths[ai]), 0, 1) : 0;
            return (1 - tr) * (1 - ta) * mat[ri, ai]
                 + (1 - tr) * ta * mat[ri, ai1]
                 + tr * (1 - ta) * mat[ri1, ai]
                 + tr * ta * mat[ri1, ai1];
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