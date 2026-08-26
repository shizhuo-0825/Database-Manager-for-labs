using DXApplication2.Common.Processing;
using ScottPlot;
using System.Linq;

namespace DXApplication2.Common.Plotting
{
    public class ScatterPlotRenderer : IPlotRenderer
    {
        public string PlotType => "Scatter";

        public void Render(Plot plot, ProcessedData data, PlotConfig config)
        {
            plot.Clear();
            PlotStyle.ResetAxesState(plot);
            // 使用固定的点大小(如果 config 里 MarkerSize=0 就默认 5)
            float markerSize = config.MarkerSize > 0 ? config.MarkerSize : 5f;

            if (data.Curves.Count > 0)
            {
                for (int i = 0; i < data.Curves.Count; i++)
                {
                    var curve = data.Curves[i];
                    var scatter = plot.Add.Scatter(curve.X, curve.Y);
                    var color = PlotStyle.Palette[i % PlotStyle.Palette.Length];
                    ApplyScatter(scatter, color, config, markerSize);
                    scatter.LegendText = curve.Label;
                }
            }
            else if (data.X != null && data.Y != null)
            {
                var scatter = plot.Add.Scatter(data.X, data.Y);
                ApplyScatter(scatter, PlotStyle.Palette[0], config, markerSize);
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
            PlotStyle.AppendExponentToAxisLabels(plot);
        }

        private static void ApplyScatter(ScottPlot.Plottables.Scatter scatter, ScottPlot.Color color, PlotConfig config, float markerSize)
        {
            scatter.Color = color;
            scatter.LineWidth = 0;   // 关键:纯 scatter 无线条
            scatter.MarkerSize = markerSize;

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