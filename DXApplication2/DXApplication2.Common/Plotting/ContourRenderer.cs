using DXApplication2.Common.IO;
using DXApplication2.Common.Processing;
using ScottPlot;
using System;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class ContourRenderer : IPlotRenderer
    {
        public string PlotType => "Contour";
        private const int Resolution = 512;
        private const int DefaultLevels = 24;

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            foreach (var p in plot.Axes.GetPanels().ToList())
                if (p is ScottPlot.Panels.ColorBar) plot.Axes.Remove(p);

            if (data.Matrix == null || data.HeatmapXValues == null || data.HeatmapYValues == null ||
                data.HeatmapXValues.Length < 2 || data.HeatmapYValues.Length < 2) return;

            var xs = data.HeatmapXValues;
            var ys = data.HeatmapYValues;
            var zMin = config.ZMinOverride ?? data.ZMin ?? 0;
            var zMax = config.ZMaxOverride ?? data.ZMax ?? 1;
            var colormap = ColormapResolver.Resolve(config.Colormap);
            int levels = config.ContourLevels ?? DefaultLevels;

            // 1) 补 NaN(源网格上,最近邻)
            var filled = FillNaN(data.Matrix, xs, ys);

            // 2) 高分辨率 bilinear 上采样
            var smooth = BilinearUpsample(filled, xs, ys, Resolution, Resolution);

            // 3) 对高分辨率结果 binning
            var quantized = Quantize(smooth, zMin, zMax, levels);

            // 4) 交给 Heatmap 直接显示,不需要 Smooth
            var heatmap = plot.Add.Heatmap(quantized);
            heatmap.Colormap = colormap;
            heatmap.ManualRange = new ScottPlot.Range((float)zMin, (float)zMax);
            heatmap.Smooth = false;
            heatmap.Extent = new CoordinateRect(xs[0], xs[^1], ys[0], ys[^1]);
            heatmap.FlipVertically = true;   // 让 y[0] 显示在下方

            try { var cbar = plot.Add.ColorBar(heatmap); cbar.Label = data.ZLabel; } catch { }

            var xLabel = !string.IsNullOrEmpty(config.XLabelOverride) ? config.XLabelOverride : data.XLabel;
            var yLabel = !string.IsNullOrEmpty(config.YLabelOverride) ? config.YLabelOverride : data.YLabel;
            if (!string.IsNullOrEmpty(xLabel)) plot.Axes.Bottom.Label.Text = xLabel;
            if (!string.IsNullOrEmpty(yLabel)) plot.Axes.Left.Label.Text = yLabel;

            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
        }

        // ==================== 共用工具 ====================
        internal static double[,] FillNaN(double[,] m, double[] xs, double[] ys)
        {
            int ny = ys.Length, nx = xs.Length;
            var r = (double[,])m.Clone();
            double xR = xs[^1] - xs[0], yR = ys[^1] - ys[0];
            if (xR < 1e-15 || yR < 1e-15) return r;

            var known = new System.Collections.Generic.List<(double nx, double ny, double z)>();
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                    if (!double.IsNaN(r[i, j]))
                        known.Add(((xs[j] - xs[0]) / xR, (ys[i] - ys[0]) / yR, r[i, j]));
            if (known.Count == 0) return r;

            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                {
                    if (!double.IsNaN(r[i, j])) continue;
                    double tx = (xs[j] - xs[0]) / xR, ty = (ys[i] - ys[0]) / yR;
                    double best = double.MaxValue, bz = 0;
                    foreach (var k in known)
                    {
                        double dx = k.nx - tx, dy = k.ny - ty;
                        double d2 = dx * dx + dy * dy;
                        if (d2 < best) { best = d2; bz = k.z; }
                    }
                    r[i, j] = bz;
                }
            return r;
        }

        internal static double[,] BilinearUpsample(double[,] src, double[] xs, double[] ys, int outW, int outH)
        {
            int ny = ys.Length, nx = xs.Length;
            var dst = new double[outH, outW];
            double xMin = xs[0], xMax = xs[^1], yMin = ys[0], yMax = ys[^1];
            for (int py = 0; py < outH; py++)
            {
                double wy = yMin + (yMax - yMin) * py / (outH - 1);
                int i = LowerBound(ys, wy);
                int i1 = Math.Min(i + 1, ny - 1);
                double ty = i1 > i ? Math.Clamp((wy - ys[i]) / (ys[i1] - ys[i]), 0, 1) : 0;
                for (int px = 0; px < outW; px++)
                {
                    double wx = xMin + (xMax - xMin) * px / (outW - 1);
                    int j = LowerBound(xs, wx);
                    int j1 = Math.Min(j + 1, nx - 1);
                    double tx = j1 > j ? Math.Clamp((wx - xs[j]) / (xs[j1] - xs[j]), 0, 1) : 0;
                    dst[py, px] = (1 - tx) * (1 - ty) * src[i, j]
                                + tx * (1 - ty) * src[i, j1]
                                + (1 - tx) * ty * src[i1, j]
                                + tx * ty * src[i1, j1];
                }
            }
            return dst;
        }

        internal static double[,] Quantize(double[,] m, double zMin, double zMax, int levels)
        {
            int ny = m.GetLength(0), nx = m.GetLength(1);
            var q = new double[ny, nx];
            double range = zMax - zMin;
            if (range < 1e-15 || levels < 2) { Array.Copy(m, q, m.Length); return q; }
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                {
                    double v = m[i, j];
                    if (double.IsNaN(v)) { q[i, j] = double.NaN; continue; }
                    int bin = (int)Math.Floor((v - zMin) / range * levels);
                    bin = Math.Clamp(bin, 0, levels - 1);
                    q[i, j] = zMin + (bin + 0.5) / levels * range;
                }
            return q;
        }

        internal static int LowerBound(double[] arr, double v)
        {
            if (v <= arr[0]) return 0;
            if (v >= arr[^1]) return arr.Length - 1;
            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int m = (lo + hi) >> 1;
                if (arr[m] <= v) lo = m; else hi = m;
            }
            return lo;
        }
    }
}