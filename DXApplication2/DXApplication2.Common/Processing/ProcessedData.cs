using System.Collections.Generic;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 数据处理后的结果,可喂给任意 IPlotRenderer 渲染。
    /// </summary>
    public class ProcessedData
    {
        /// <summary>数据源类型(PSHG / RaSHG / SHGImaging / Whitelight)</summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>单条曲线的 X 轴数据(简单场景)</summary>
        public double[]? X { get; set; }

        /// <summary>单条曲线的 Y 轴数据(简单场景)</summary>
        public double[]? Y { get; set; }
        /// <summary>Y 轴误差棒(可选)</summary>
        public double[]? YError { get; set; }

        /// <summary>二维矩阵数据(Heatmap 用)</summary>
        public double[,]? Matrix { get; set; }
        // 用于 Heatmap:X 轴值(cell 的中心位置)
        public double[]? HeatmapXValues { get; set; }

        // 用于 Heatmap:Y 轴值(cell 的中心位置)
        public double[]? HeatmapYValues { get; set; }

        // Z 轴 label(colorbar 标题)
        public string ZLabel { get; set; } = string.Empty;

        // Z 轴数据的自然范围(colormap 默认范围)
        public double? ZMin { get; set; }
        public double? ZMax { get; set; }
        /// <summary>多条曲线(叠加图用,若非空则忽略 X/Y)</summary>
        public List<Curve> Curves { get; set; } = new();

        // 显示相关
        public string XLabel { get; set; } = string.Empty;
        public string YLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>透传的元数据(比如数据来源、处理参数),用于导出记录</summary>
        public Dictionary<string, object> Metadata { get; } = new();
        // 用户在 UI 里选中的标签(用于多选片段)。null = 显示所有
        public HashSet<string>? SelectedLabels { get; set; }
    }

    /// <summary>一条曲线(多曲线场景)</summary>
    public class Curve
    {
        public double[] X { get; set; } = System.Array.Empty<double>();
        public double[] Y { get; set; } = System.Array.Empty<double>();
        public string Label { get; set; } = string.Empty;
    }
}