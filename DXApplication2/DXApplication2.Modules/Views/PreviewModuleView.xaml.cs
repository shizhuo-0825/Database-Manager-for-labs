using DXApplication2.Common.IO;
using DXApplication2.Common.Plotting;
using DXApplication2.Modules.ViewModels;
using ScottPlot;
using System;
using System.Linq;
using System.Windows.Controls;

namespace DXApplication2.Modules.Views
{
    public partial class PreviewModuleView : UserControl
    {
        private PreviewModuleViewModel? _vm;

        public PreviewModuleView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null) _vm.RenderRequested -= OnRenderRequested;
            _vm = DataContext as PreviewModuleViewModel;
            if (_vm != null) _vm.RenderRequested += OnRenderRequested;
        }

        private void OnRenderRequested(object? sender, EventArgs e) => RenderCurrent();

        private void RenderCurrent()
        {
            if (_vm?.Matrix == null)
            {
                PlotControl.Plot.Clear();
                PlotControl.Refresh();
                return;
            }

            var matrix = _vm.Matrix;
            var plot = PlotControl.Plot;
            plot.Clear();

            // 清理 colorbar
            var panels = plot.Axes.GetPanels().ToList();
            foreach (var panel in panels)
            {
                if (panel is ScottPlot.Panels.ColorBar)
                    plot.Axes.Remove(panel);
            }
            var displayMatrix = MatrixFlipHelper.FlipY(matrix);
            var heatmap = plot.Add.Heatmap(displayMatrix);
            heatmap.Colormap = ColormapResolver.Resolve(_vm.Colormap);
            heatmap.ManualRange = new ScottPlot.Range((float)_vm.ZMinValue, (float)_vm.ZMaxValue);

            var cbar = plot.Add.ColorBar(heatmap);
            cbar.Label = "Value";

            // 叠加 ROI 黄框(如果有)
            var roi = _vm.CurrentRoi;
            if (roi != null)
            {
                DrawRoiRectangle(plot, roi, matrix.GetLength(0), matrix.GetLength(1));
            }

            plot.Axes.AutoScale();
            PlotControl.Refresh();
        }

        /// <summary>
        /// 在 heatmap 上画一个黄色的 ROI 边框。
        /// 注意坐标转换:heatmap 里
        ///   - X 方向 = column
        ///   - Y 方向 = row(但 ScottPlot 默认 Y 朝上,而矩阵 row 从上往下,所以 Y 需要翻转)
        /// </summary>
        private static void DrawRoiRectangle(Plot plot, DXApplication2.Common.Roi.RoiRect roi, int totalRows, int totalCols)
        {
            // ScottPlot Heatmap 默认坐标:X 从 0 到 cols,Y 从 0 到 rows
            // 但 Y 轴是"从底部往上",而矩阵行是"从顶部往下"
            // 所以 rowStart(顶部)对应 Y = totalRows - rowStart,rowEnd 对应 Y = totalRows - rowEnd
            double x1 = roi.ColStart;
            double x2 = roi.ColEnd;
            double y1 = roi.RowStart;
            double y2 = roi.RowEnd;

            // 画 4 条线组成矩形
            var yellow = Colors.Yellow;

            var line1 = plot.Add.Line(x1, y1, x2, y1);   // 底
            var line2 = plot.Add.Line(x2, y1, x2, y2);   // 右
            var line3 = plot.Add.Line(x2, y2, x1, y2);   // 顶
            var line4 = plot.Add.Line(x1, y2, x1, y1);   // 左

            foreach (var line in new[] { line1, line2, line3, line4 })
            {
                line.Color = yellow;
                line.LineWidth = 2;
            }
        }
    }
}