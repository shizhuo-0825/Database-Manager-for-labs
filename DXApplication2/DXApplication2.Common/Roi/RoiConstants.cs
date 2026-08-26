using System.Collections.Generic;

namespace DXApplication2.Common.Roi
{
    /// <summary>
    /// ROI 标签常量。四种固定标签,存文件时用它们作为文件名前缀。
    /// </summary>
    public static class RoiTags
    {
        public const string PSHG = "PSHG";
        public const string RaSHG = "RaSHG";
        public const string SHGImage = "SHGImage";
        public const string Background = "Background";

        public static readonly IReadOnlyList<string> All = new[]
        {
            PSHG, RaSHG, SHGImage, Background
        };
    }
}