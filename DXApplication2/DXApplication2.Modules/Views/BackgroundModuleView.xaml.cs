using DXApplication2.Common.Plotting;
using DXApplication2.Modules.ViewModels;
using ScottPlot;
using System;
using System.Linq;
using System.Windows.Controls;

namespace DXApplication2.Modules.Views
{
    public partial class BackgroundModuleView : UserControl
    {
        private BackgroundModuleViewModel? _vm;
        private readonly PolarPlotRenderer _polarRenderer = new(withFill: false);

        public BackgroundModuleView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.RenderRequested -= OnRenderRequested;
            _vm = DataContext as BackgroundModuleViewModel;
            if (_vm != null) _vm.RenderRequested += OnRenderRequested;
        }

        private void OnRenderRequested(object? sender, EventArgs e) => RenderCurrent();

        private void RenderCurrent()
        {
            if (_vm == null)
            {
                PlotControl.Plot.Clear();
                PlotControl.Refresh();
                return;
            }

            var plot = PlotControl.Plot;
            plot.Clear();

            // 清 colorbar panel
            var panels = plot.Axes.GetPanels().ToList();
            foreach (var panel in panels)
            {
                if (panel is ScottPlot.Panels.ColorBar)
                    plot.Axes.Remove(panel);
            }

            if (_vm.Mode == BackgroundViewMode.Image && _vm.ImageMatrix != null)
            {
                var matrix = _vm.ImageMatrix;
                var heatmap = plot.Add.Heatmap(matrix);
                heatmap.Colormap = ColormapResolver.Resolve(_vm.Colormap);
                heatmap.ManualRange = new ScottPlot.Range((float)_vm.ZMinValue, (float)_vm.ZMaxValue);
                var cbar = plot.Add.ColorBar(heatmap);
                cbar.Label = "Value";
                plot.Axes.AutoScale();
            }
            else if (_vm.Mode == BackgroundViewMode.Pump && _vm.PumpData != null)
            {
                var config = new PlotConfig();
                _polarRenderer.Render(plot, _vm.PumpData, config);

                // 叠加拟合曲线(把 θ 当 Polarizer 代入)
                if (_vm.FitCurveTheta != null && _vm.FitCurveY != null)
                {
                    DrawFitCurveOnPolar(plot, _vm.FitCurveTheta, _vm.FitCurveY);
                }
            }

            PlotControl.Refresh();
        }
        private static void DrawFitCurveOnPolar(ScottPlot.Plot plot, double[] thetaDeg, double[] y)
        {
            // 把 (θ, r) 转成笛卡尔 (x, y),角度为度
            int n = thetaDeg.Length;
            var xs = new double[n];
            var ys = new double[n];
            for (int i = 0; i < n; i++)
            {
                double rad = thetaDeg[i] * System.Math.PI / 180.0;
                double r = System.Math.Max(0, y[i]);   // 负值裁 0(跟 PolarPlotRenderer 一致)
                xs[i] = r * System.Math.Cos(rad);
                ys[i] = r * System.Math.Sin(rad);
            }

            var scatter = plot.Add.Scatter(xs, ys);
            scatter.Color = ScottPlot.Colors.Red;
            scatter.LineWidth = 2;
            scatter.MarkerSize = 0;
            scatter.LinePattern = ScottPlot.LinePattern.Solid;
            scatter.LegendText = "Fit";
        }
    }
}