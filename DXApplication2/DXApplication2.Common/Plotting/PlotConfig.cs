namespace DXApplication2.Common.Plotting
{
    public enum MarginStyle
    {
        Tight,      // 数据紧贴轴
        Common,     // 5-10% 边距(默认)
        Loose       // 20% 边距
    }

    public enum LegendPos
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft,
        Hidden
    }
    public enum MarkerStyleKind
    {
        OpenCircle,      // 空心圆
        FilledCircle,    // 实心圆
        FilledOutlined   // 实心圆+边界
    }
    /// <summary>
    /// Plot 全局配置(持久化到磁盘 JSON)。
    /// 用户在右侧面板改动会自动保存,下次打开自动加载。
    /// </summary>
    public class PlotConfig
    {
        public string FontName { get; set; } = "Arial";
        public float BaseFontSize { get; set; } = 12f;   // AxisLabel/TickLabel/Legend 一起变

        public bool ShowFrame { get; set; } = false;      // 上和右的边框

        public MarginStyle Margin { get; set; } = MarginStyle.Common;

        public LegendPos LegendPosition { get; set; } = LegendPos.TopRight;

        public float LineWidth { get; set; } = 1.5f;

        // 图上标签(用户可覆盖 ProcessedData 里的默认值)
        public string XLabelOverride { get; set; } = "";
        public string YLabelOverride { get; set; } = "";
        public MarkerStyleKind MarkerStyle { get; set; } = MarkerStyleKind.FilledCircle;
        public float MarkerSize { get; set; } = 0f;  // 默认 0=不显示点
                                                     // Heatmap 相关
        public ColormapKind Colormap { get; set; } = ColormapKind.Magma;

        // Z 范围(用户可拖 RangeSlider 调整,null 表示自动用数据范围)
        public double? ZMinOverride { get; set; }
        public double? ZMaxOverride { get; set; }
    }
}