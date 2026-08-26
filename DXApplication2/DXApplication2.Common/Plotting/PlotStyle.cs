using ScottPlot;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public static class PlotStyle
    {
        // 默认值(如果 PlotConfig 有相同字段,以 PlotConfig 为准)
        public const string DefaultFontName = "Arial";
        public const float DefaultBaseFontSize = 12f;
        public const float DefaultLineWidth = 1.5f;

        // 导出规格
        public const int ExportDpi = 300;
        public const int ExportWidthPx = 990;
        public const int ExportHeightPx = 750;
        public static void ResetAxesState(Plot plot)
        {
            // 清 colorbar panel(HeatmapRenderer 会加)
            var panels = plot.Axes.GetPanels().ToList();
            foreach (var panel in panels)
            {
                if (panel is ScottPlot.Panels.ColorBar)
                    plot.Axes.Remove(panel);
            }

            // 清所有 axis rules(SquareUnits 等会往这里塞规则)
            plot.Axes.Rules.Clear();

            // 恢复所有轴可见
            plot.Axes.Left.IsVisible = true;
            plot.Axes.Bottom.IsVisible = true;
            plot.Axes.Top.IsVisible = true;
            plot.Axes.Right.IsVisible = true;
        }
        // 色盲友好(Wong 2011)
        public static readonly Color[] Palette = new[]
        {
            Color.FromHex("#0072B2"),
            Color.FromHex("#E69F00"),
            Color.FromHex("#009E73"),
            Color.FromHex("#CC79A7"),
            Color.FromHex("#56B4E9"),
            Color.FromHex("#D55E00"),
            Color.FromHex("#F0E442"),
            Color.FromHex("#000000"),
        };

        /// <summary>应用配置到 Plot(字体、边框、边距、图例位置)</summary>
        public static void Apply(Plot plot, PlotConfig config)
        {
            var fontName = config.FontName;
            var baseSize = config.BaseFontSize;

            // 字体
            plot.Axes.Bottom.Label.FontName = fontName;
            plot.Axes.Left.Label.FontName = fontName;
            plot.Axes.Bottom.Label.FontSize = baseSize;
            plot.Axes.Left.Label.FontSize = baseSize;
            plot.Axes.Bottom.TickLabelStyle.FontName = fontName;
            plot.Axes.Left.TickLabelStyle.FontName = fontName;
            plot.Axes.Bottom.TickLabelStyle.FontSize = baseSize;
            plot.Axes.Left.TickLabelStyle.FontSize = baseSize;

            plot.Axes.Bottom.FrameLineStyle.Width = 1f;
            plot.Axes.Left.FrameLineStyle.Width = 1f;
            plot.Axes.Top.FrameLineStyle.Width = config.ShowFrame ? 1f : 0f;
            plot.Axes.Right.FrameLineStyle.Width = config.ShowFrame ? 1f : 0f;

            plot.HideGrid();

            plot.Legend.FontName = fontName;
            plot.Legend.FontSize = baseSize;
            plot.Legend.OutlineWidth = 0f;
            plot.Legend.BackgroundColor = Colors.Transparent;

            plot.Legend.IsVisible = config.LegendPosition != LegendPos.Hidden;
            switch (config.LegendPosition)
            {
                case LegendPos.TopRight: plot.Legend.Alignment = Alignment.UpperRight; break;
                case LegendPos.TopLeft: plot.Legend.Alignment = Alignment.UpperLeft; break;
                case LegendPos.BottomRight: plot.Legend.Alignment = Alignment.LowerRight; break;
                case LegendPos.BottomLeft: plot.Legend.Alignment = Alignment.LowerLeft; break;
            }

            plot.FigureBackground.Color = Colors.White;
            plot.DataBackground.Color = Colors.White;

            ApplyMargin(plot, config.Margin);
            ApplyScientificTickFormat(plot);
        }

        private static void ApplyScientificTickFormat(Plot plot)
        {
            ApplyToAxis(plot.Axes.Left);
            ApplyToAxis(plot.Axes.Bottom);
        }

        private static void ApplyToAxis(ScottPlot.IAxis axis)
        {
            if (axis.TickGenerator is not ScottPlot.TickGenerators.NumericAutomatic gen)
            {
                gen = new ScottPlot.TickGenerators.NumericAutomatic();
                axis.TickGenerator = gen;
            }

            gen.LabelFormatter = (double value) =>
            {
                var range = axis.Range;
                double maxAbs = System.Math.Max(System.Math.Abs(range.Min), System.Math.Abs(range.Max));
                if (maxAbs < 1e-300) return "0";

                int exp = (int)System.Math.Floor(System.Math.Log10(maxAbs));
                if (exp < 4 && exp > -2)
                    return value.ToString("G4");

                double mantissa = value / System.Math.Pow(10, exp);
                return mantissa.ToString("F3");
            };
        }

        /// <summary>
        /// 在轴 label 后面拼接"(×10^n)"。必须在 AutoScale 之后调用。
        /// </summary>
        public static void AppendExponentToAxisLabels(Plot plot)
        {
            AppendToOne(plot.Axes.Left);
            AppendToOne(plot.Axes.Bottom);
        }

        private static void AppendToOne(ScottPlot.IAxis axis)
        {
            var range = axis.Range;
            double maxAbs = System.Math.Max(System.Math.Abs(range.Min), System.Math.Abs(range.Max));
            if (maxAbs < 1e-300) return;

            int exp = (int)System.Math.Floor(System.Math.Log10(maxAbs));
            if (exp < 4 && exp > -2) return;

            string expStr = ToSuperscript(exp);
            string suffix = $" (×10{expStr})";

            // 拼接(避免重复拼接:如果 label 已经含 "×10" 就跳过)
            var currentLabel = axis.Label.Text ?? "";
            if (currentLabel.Contains("×10")) return;
            axis.Label.Text = currentLabel + suffix;
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

        private static void ApplyMargin(Plot plot, MarginStyle style)
        {
            switch (style)
            {
                case MarginStyle.Tight:
                    plot.Axes.Margins(0, 0);
                    break;
                case MarginStyle.Common:
                    plot.Axes.Margins(0.05, 0.05);
                    break;
                case MarginStyle.Loose:
                    plot.Axes.Margins(0.2, 0.2);
                    break;
            }
        }
    }
}