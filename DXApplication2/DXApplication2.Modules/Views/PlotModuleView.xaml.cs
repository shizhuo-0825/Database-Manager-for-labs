using DXApplication2.Common.Plotting;
using DXApplication2.Modules.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;


namespace DXApplication2.Modules.Views
{

    public partial class PlotModuleView : UserControl
    {
        private readonly HeatmapRenderer _heatmapRenderer = new();
        private readonly PolarHeatmapRenderer _polarHeatmapRenderer = new();
        private readonly LinePlotRenderer _lineRenderer = new();
        private readonly PolarPlotRenderer _polarRenderer = new(withFill: false);
        private readonly PolarPlotRenderer _polarFilledRenderer = new(withFill: true);
        private readonly ContourRenderer _contourRenderer = new();
        private readonly PolarContourRenderer _polarContourRenderer = new();
        private PlotModuleViewModel? _vm;

        public PlotModuleView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            PlotControl.MouseMove += OnPlotMouseMove;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.RenderRequested -= OnRenderRequested;
            _vm = DataContext as PlotModuleViewModel;
            if (_vm != null) _vm.RenderRequested += OnRenderRequested;
        }

        private void OnRenderRequested(object? sender, System.EventArgs e) => RenderCurrent();

        private readonly ScatterPlotRenderer _scatterRenderer = new();

        private void RenderCurrent()
        {
            if (_vm?.CurrentData == null)
            {
                PlotControl.Plot.Clear();
                PlotControl.Refresh();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"RenderCurrent: PlotType={_vm.CurrentPlotType}, Curves={_vm.CurrentData?.Curves.Count}");
            IPlotRenderer renderer = _vm.CurrentPlotType switch
            {
                "Scatter" => _scatterRenderer,
                "Heatmap" => _heatmapRenderer,
                "PolarHeatmap" => _polarHeatmapRenderer,
                "Polar" => _polarRenderer,
                "PolarFilled" => _polarFilledRenderer,
                "Contour" => _contourRenderer,
                "PolarContour" => _polarContourRenderer,
                _ => _lineRenderer
            };
            System.Diagnostics.Debug.WriteLine($"Using renderer: {renderer.PlotType}");
            renderer.Render(PlotControl.Plot, _vm.CurrentData, _vm.Config);
            PlotControl.Refresh();
        }

        private void OnPlotMouseMove(object sender, MouseEventArgs e)
        {
            if (_vm == null) return;
            var pos = e.GetPosition(PlotControl);
            var pixel = new ScottPlot.Pixel((float)pos.X, (float)pos.Y);
            var coord = PlotControl.Plot.GetCoordinates(pixel);
            _vm.MouseCoordinate = $"X: {coord.X:F3}   Y: {coord.Y:F3}";
        }
    }
}