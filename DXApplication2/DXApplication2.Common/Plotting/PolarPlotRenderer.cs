using DXApplication2.Common.Processing;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    /// <summary>
    /// 极坐标图渲染器。同时支持 with/without filling 两种模式。
    /// 数据点 (θ deg, r) 转换成 (x, y) = (r*cos(θ_rad), r*sin(θ_rad))
    /// </summary>
    public class PolarPlotRenderer : IPlotRenderer
    {
        private readonly bool _withFill;
        public PolarPlotRenderer(bool withFill = false) { _withFill = withFill; }

        public string PlotType => _withFill ? "PolarFilled" : "Polar";

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            PlotStyle.ResetAxesState(plot);
            if (data.Curves.Count == 0) return;

            // 1. 过滤:根据 SelectedLabels(如果有)
            var curvesToDraw = data.Curves;
            if (data.SelectedLabels != null && data.SelectedLabels.Count > 0)
            {
                curvesToDraw = data.Curves
                    .Where(c => data.SelectedLabels.Contains(c.Label))
                    .ToList();
            }

            if (curvesToDraw.Count == 0) return;

            // 2. 计算最大 r(用于绘制网格)
            double maxR = 0;
            foreach (var c in curvesToDraw)
            {
                foreach (var y in c.Y)
                {
                    if (double.IsNaN(y) || double.IsInfinity(y)) continue;
                    if (y > maxR) maxR = y;
                }
            }
            if (maxR <= 0) maxR = 1;

            // 3. 画极坐标网格
            DrawPolarGrid(plot, maxR, config);

            // 4. 画每条曲线
            for (int i = 0; i < curvesToDraw.Count; i++)
            {
                var curve = curvesToDraw[i];
                var color = PlotStyle.Palette[i % PlotStyle.Palette.Length];
                DrawCurve(plot, curve, color, config, _withFill);
            }

            // 5. 坐标轴设置:隐藏,让极坐标网格成为唯一"骨架"
            plot.Axes.Bottom.IsVisible = false;
            plot.Axes.Left.IsVisible = false;
            plot.Axes.Top.IsVisible = false;
            plot.Axes.Right.IsVisible = false;

            // 让图变成正方形(1:1 比例),避免圆变椭圆
            plot.Axes.SquareUnits();

            // 应用全局样式
            PlotStyle.Apply(plot, config);

            // 自动缩放
            plot.Axes.AutoScale();

        }

        // ============================================================
        // 极坐标网格:同心圆 + 辐射线
        // ============================================================
        private static void DrawPolarGrid(Plot plot, double maxR, PlotConfig config)
        {
            var gridColor = new Color(200, 200, 200);
            var labelColor = new Color(120, 120, 120);
            float baseSize = config.BaseFontSize;
            string fontName = string.IsNullOrEmpty(config.FontName) ? "Arial" : config.FontName;

            // 决定统一 exponent
            int exp = (int)System.Math.Floor(System.Math.Log10(maxR));
            bool useSci = (exp >= 4 || exp <= -2);
            double divisor = useSci ? System.Math.Pow(10, exp) : 1.0;

            // 同心圆 + r 标签
            int nRings = 4;
            for (int k = 1; k <= nRings; k++)
            {
                double r = maxR * k / nRings;
                DrawCircle(plot, r, gridColor, isDashed: k < nRings);

                // r 标签
                string rLabel = useSci ? (r / divisor).ToString("F3") : r.ToString("G3");
                var text = plot.Add.Text(rLabel, r, 0);
                text.LabelFontColor = labelColor;
                text.LabelFontSize = baseSize;
                text.LabelFontName = fontName;
                text.LabelAlignment = Alignment.LowerLeft;
            }

            // 辐射线 + 角度标签
            for (int angleDeg = 0; angleDeg < 360; angleDeg += 30)
            {
                double angleRad = angleDeg * System.Math.PI / 180.0;
                double x = maxR * 1.05 * System.Math.Cos(angleRad);
                double y = maxR * 1.05 * System.Math.Sin(angleRad);

                var line = plot.Add.Line(0, 0, x, y);
                line.Color = gridColor;
                line.LineWidth = 1;

                double lx = maxR * 1.15 * System.Math.Cos(angleRad);
                double ly = maxR * 1.15 * System.Math.Sin(angleRad);
                var label = plot.Add.Text($"{angleDeg}°", lx, ly);
                label.LabelFontColor = labelColor;
                label.LabelFontSize = baseSize;
                label.LabelFontName = fontName;
                label.LabelAlignment = Alignment.MiddleCenter;
                if (angleDeg % 90 == 0) label.LabelBold = true;
            }

            // 加"×10^n" annotation(只对 r 有效,角度是度不需要)
            if (useSci)
            {
                string expStr = ToSuperscript(exp);
                double posX = maxR * 1.15;
                double posY = maxR * 1.15;
                var text = plot.Add.Text($"×10{expStr}", posX, posY);
                text.LabelFontName = fontName;
                text.LabelFontSize = baseSize;
                text.LabelFontColor = Colors.Black;
                text.LabelAlignment = Alignment.LowerLeft;
            }
        }

        private static string ToSuperscript(int n)
        {
            var supDigits = new[] { '⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹' };
            var sb = new System.Text.StringBuilder();
            if (n < 0) { sb.Append('⁻'); n = -n; }
            foreach (var d in n.ToString())
                sb.Append(supDigits[d - '0']);
            return sb.ToString();
        }

        private static void DrawCircle(Plot plot, double r, Color color, bool isDashed)
        {
            const int segments = 72;   // 5° 一段
            var xs = new double[segments + 1];
            var ys = new double[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double angle = i * 2 * Math.PI / segments;
                xs[i] = r * Math.Cos(angle);
                ys[i] = r * Math.Sin(angle);
            }
            var scatter = plot.Add.Scatter(xs, ys);
            scatter.Color = color;
            scatter.LineWidth = 1;
            scatter.MarkerSize = 0;
            if (isDashed)
            {
                scatter.LinePattern = LinePattern.Dashed;
            }
        }

        // ============================================================
        // 画一条曲线(极坐标 → 笛卡尔)
        // ============================================================
        private static void DrawCurve(Plot plot, Curve curve, Color color, PlotConfig config, bool withFill)
        {
            if (curve.X.Length == 0) return;

            int n = curve.X.Length;
            // 转换到笛卡尔(NaN 也保留位置,连线会跳过)
            var xs = new double[n + 1];
            var ys = new double[n + 1];
            for (int i = 0; i < n; i++)
            {
                double angleDeg = curve.X[i];
                double r = curve.Y[i];
                if (double.IsNaN(r) || double.IsInfinity(r)) r = 0;
                double angleRad = angleDeg * Math.PI / 180.0;
                xs[i] = r * Math.Cos(angleRad);
                ys[i] = r * Math.Sin(angleRad);
            }
            // 闭合(最后一点回到第一点)
            xs[n] = xs[0];
            ys[n] = ys[0];

            if (withFill)
            {
                // Filled polygon
                var polygon = plot.Add.Polygon(
                    xs.Zip(ys, (x, y) => new Coordinates(x, y)).ToArray());
                polygon.FillColor = color.WithAlpha(60);
                polygon.LineColor = color;
                polygon.LineWidth = config.LineWidth;
                polygon.LegendText = curve.Label;

                // 如果 MarkerSize > 0,再叠一层散点(polygon 不支持 marker)
                if (config.MarkerSize > 0)
                {
                    // 用原始点(不含闭合点)
                    var pointsX = xs.Take(xs.Length - 1).ToArray();
                    var pointsY = ys.Take(ys.Length - 1).ToArray();
                    var overlay = plot.Add.Scatter(pointsX, pointsY);
                    ApplyStyle(overlay, color, config);
                    overlay.LineWidth = 0;   // 不画线,只显示点
                    overlay.LegendText = "";  // 不重复出现在 legend
                }
            }
            else
            {
                var scatter = plot.Add.Scatter(xs, ys);
                ApplyStyle(scatter, color, config);
                scatter.LegendText = curve.Label;
            }
        }
        private static void ApplyStyle(ScottPlot.Plottables.Scatter scatter, Color color, PlotConfig config)
        {
            scatter.Color = color;
            scatter.LineWidth = config.LineWidth;
            scatter.MarkerSize = config.MarkerSize;

            if (config.MarkerSize <= 0)
            {
                return;
            }

            switch (config.MarkerStyle)
            {
                case MarkerStyleKind.OpenCircle:
                    scatter.MarkerShape = MarkerShape.OpenCircle;
                    scatter.MarkerFillColor = Colors.White;
                    scatter.MarkerLineColor = color;
                    scatter.MarkerLineWidth = 1.5f;
                    break;
                case MarkerStyleKind.FilledCircle:
                    scatter.MarkerShape = MarkerShape.FilledCircle;
                    scatter.MarkerFillColor = color;
                    scatter.MarkerLineWidth = 0;
                    break;
                case MarkerStyleKind.FilledOutlined:
                    scatter.MarkerShape = MarkerShape.FilledCircle;
                    scatter.MarkerFillColor = color;
                    scatter.MarkerLineColor = Colors.White;
                    scatter.MarkerLineWidth = 2f;
                    break;
            }
        }
    }
}