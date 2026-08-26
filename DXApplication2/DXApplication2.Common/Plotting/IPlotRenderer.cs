using DXApplication2.Common.Processing;
using ScottPlot;

namespace DXApplication2.Common.Plotting
{
    public interface IPlotRenderer
    {
        string PlotType { get; }
        void Render(Plot plot, ProcessedData data, PlotConfig config);
    }
}