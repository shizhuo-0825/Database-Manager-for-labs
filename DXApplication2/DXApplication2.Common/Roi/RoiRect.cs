namespace DXApplication2.Common.Roi
{
    /// <summary>
    /// 一个 ROI 矩形:整数像素坐标(row/col based)。
    /// </summary>
    public class RoiRect
    {
        public int RowStart { get; set; }
        public int RowEnd { get; set; }
        public int ColStart { get; set; }
        public int ColEnd { get; set; }

        public string Tag { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;

        public int Rows => RowEnd - RowStart;
        public int Cols => ColEnd - ColStart;

        public override string ToString() => $"{Tag} @ {Timestamp} [{RowStart}-{RowEnd}, {ColStart}-{ColEnd}]";
    }
}