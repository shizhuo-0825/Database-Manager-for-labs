using DXApplication2.Common.Processing;
using ScottPlot;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class LinePlotRenderer : IPlotRenderer
    {
        public string PlotType => "Line";

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            PlotStyle.ResetAxesState(plot);
            if (data.Curves.Count > 0)
            {
                for (int i = 0; i < data.Curves.Count; i++)
                {
                    var curve = data.Curves[i];
                    var scatter = plot.Add.Scatter(curve.X, curve.Y);
                    var color = PlotStyle.Palette[i % PlotStyle.Palette.Length];
                    ApplyMarkerAndColor(scatter, color, config);
                    scatter.LegendText = curve.Label;
                }
            }
            else if (data.X != null && data.Y != null)
            {
                var scatter = plot.Add.Scatter(data.X, data.Y);
                ApplyMarkerAndColor(scatter, PlotStyle.Palette[0], config);
            }
            else
            {
                return;
            }

            var xLabel = !string.IsNullOrEmpty(config.XLabelOverride) ? config.XLabelOverride : data.XLabel;
            var yLabel = !string.IsNullOrEmpty(config.YLabelOverride) ? config.YLabelOverride : data.YLabel;
            if (!string.IsNullOrEmpty(xLabel)) plot.Axes.Bottom.Label.Text = xLabel;
            if (!string.IsNullOrEmpty(yLabel)) plot.Axes.Left.Label.Text = yLabel;

            PlotStyle.Apply(plot, config);
            plot.Axes.AutoScale();
            PlotStyle.AppendExponentToAxisLabels(plot);   // 新加
        }

        private static void ApplyMarkerAndColor(ScottPlot.Plottables.Scatter scatter, ScottPlot.Color color, PlotConfig config)
        {
            scatter.Color = color;
            scatter.LineWidth = config.LineWidth;
            scatter.MarkerSize = config.MarkerSize;

            if (config.MarkerSize <= 0)
            {
                // 大小 0 = 不显示点
                return;
            }

            switch (config.MarkerStyle)
            {
                case MarkerStyleKind.OpenCircle:
                    scatter.MarkerShape = ScottPlot.MarkerShape.OpenCircle;
                    scatter.MarkerFillColor = ScottPlot.Colors.White;
                    scatter.MarkerLineColor = color;
                    scatter.MarkerLineWidth = 1.5f;
                    break;

                case MarkerStyleKind.FilledCircle:
                    scatter.MarkerShape = ScottPlot.MarkerShape.FilledCircle;
                    scatter.MarkerFillColor = color;
                    scatter.MarkerLineWidth = 0;
                    break;

                case MarkerStyleKind.FilledOutlined:
                    scatter.MarkerShape = ScottPlot.MarkerShape.FilledCircle;
                    scatter.MarkerFillColor = color;
                    scatter.MarkerLineColor = ScottPlot.Colors.White;
                    scatter.MarkerLineWidth = 2f;
                    break;
            }
        }
    }
}