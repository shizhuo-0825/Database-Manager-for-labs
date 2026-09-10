using ScottPlot;
using ScottPlot.TickGenerators;
using System;
using System.Collections.Generic;

namespace DXApplication2.Common.Plotting
{
    /// <summary>
    /// 极坐标 heatmap/contour 共用的坐标轴配置。
    /// 隐藏底部 X;左侧 Y 变成 r 轴,Y ∈ [-rMax, rMax] 对应物理 r ∈ [rMin, rMax]。
    /// Y=0 显示 rMin;|Y|=rMax 显示 rMax;关于 0 对称。
    /// </summary>
    public static class PolarAxes
    {
        public static void Configure(Plot plot, double rMin, double rMax, string rLabel)
        {
            // 1) 隐藏底部 X 轴
            plot.Axes.Bottom.TickGenerator = new NumericManual();  // 空刻度
            plot.Axes.Bottom.MajorTickStyle.Length = 0;
            plot.Axes.Bottom.MinorTickStyle.Length = 0;
            plot.Axes.Bottom.FrameLineStyle.Width = 0;
            plot.Axes.Bottom.Label.Text = "";

            // 2) 左侧 Y 轴 → r 轴
            double range = rMax - rMin;
            if (range < 1e-15) return;
            var ticks = new NumericManual();
            foreach (var r in NiceTicks(rMin, rMax, 5))
            {
                double y = (r - rMin) / range * rMax;
                string label = FormatTick(r);
                ticks.AddMajor(y, label);
                if (y > 1e-9) ticks.AddMajor(-y, label);
            }
            plot.Axes.Left.TickGenerator = ticks;
            plot.Axes.Left.Label.Text = rLabel;
        }

        private static IEnumerable<double> NiceTicks(double lo, double hi, int target)
        {
            if (hi <= lo) yield break;
            double rough = (hi - lo) / target;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            double n = rough / mag;
            double step = n < 1.5 ? mag : n < 3 ? 2 * mag : n < 7 ? 5 * mag : 10 * mag;
            double first = Math.Ceiling(lo / step) * step;
            for (double v = first; v <= hi + 1e-9; v += step) yield return v;
        }

        private static string FormatTick(double v)
        {
            if (v == 0) return "0";
            double a = Math.Abs(v);
            return (a >= 1e4 || a < 1e-2) ? v.ToString("G3") : v.ToString("G4");
        }
    }
}