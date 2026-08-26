using ScottPlot;

namespace DXApplication2.Common.Plotting
{
    /// <summary>
    /// 把 ColormapKind 转成 ScottPlot 的 IColormap 实例。
    /// ScottPlot 5.x 的所有 colormap 在 ScottPlot.Colormaps 命名空间。
    /// </summary>
    public static class ColormapResolver
    {
        public static IColormap Resolve(ColormapKind kind)
        {
            return kind switch
            {
                ColormapKind.Magma => new ScottPlot.Colormaps.Magma(),
                ColormapKind.Matter => new ScottPlot.Colormaps.Matter(),
                ColormapKind.Phase => new ScottPlot.Colormaps.Phase(),
                ColormapKind.Balance => new ScottPlot.Colormaps.Balance(),
                ColormapKind.Curl => new ScottPlot.Colormaps.Curl(),
                ColormapKind.Dense => new ScottPlot.Colormaps.Dense(),
                ColormapKind.Grayscale => new ScottPlot.Colormaps.Grayscale(),
                _ => new ScottPlot.Colormaps.Magma()
            };
        }
    }
}