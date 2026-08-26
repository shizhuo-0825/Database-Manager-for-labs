using SkiaSharp;
using System;
using System.IO;

namespace DXApplication2.Common.IO
{
    /// <summary>
    /// 用 SkiaSharp 把矩阵渲染成缩略图 PNG(应用 magma colormap)。
    /// </summary>
    public static class ThumbnailGenerator
    {
        public const int DefaultThumbSize = 256;

        public static void GenerateAndSave(double[,] matrix, string outputPngPath, int size = DefaultThumbSize)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            if (rows == 0 || cols == 0) return;

            // 1. 找 min/max(忽略 NaN)
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            if (min == max) { min = 0; max = 1; }

            // 2. 生成"内部图像"(cols × rows,原始尺寸)
            using var innerBmp = new SKBitmap(cols, rows, SKColorType.Rgba8888, SKAlphaType.Premul);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) v = min;
                    var t = (v - min) / (max - min);
                    if (t < 0) t = 0; if (t > 1) t = 1;
                    var (r, g, b) = MagmaColor(t);
                    innerBmp.SetPixel(j, i, new SKColor((byte)r, (byte)g, (byte)b));
                }
            }

            // 3. 缩放到 size × size(保持宽高比)
            float scale = System.Math.Min((float)size / cols, (float)size / rows);
            int outW = System.Math.Max(1, (int)(cols * scale));
            int outH = System.Math.Max(1, (int)(rows * scale));

            using var scaledBmp = innerBmp.Resize(new SKImageInfo(outW, outH), SKFilterQuality.Medium);

            // 4. 保存 PNG
            Directory.CreateDirectory(Path.GetDirectoryName(outputPngPath)!);
            using var img = SKImage.FromBitmap(scaledBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            using var fs = File.OpenWrite(outputPngPath);
            data.SaveTo(fs);
        }

        // Magma colormap(简化实现:6 关键色插值)
        private static (byte r, byte g, byte b) MagmaColor(double t)
        {
            // Wong-like 简版 magma:黑 → 紫 → 红 → 黄 → 白
            var stops = new (double t, int r, int g, int b)[]
            {
                (0.0, 0, 0, 0),
                (0.2, 55, 20, 90),
                (0.4, 135, 40, 110),
                (0.6, 210, 60, 90),
                (0.8, 250, 140, 60),
                (1.0, 253, 253, 190),
            };

            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t <= stops[i + 1].t)
                {
                    var a = stops[i];
                    var b = stops[i + 1];
                    var f = (t - a.t) / (b.t - a.t);
                    int r = (int)(a.r + f * (b.r - a.r));
                    int g = (int)(a.g + f * (b.g - a.g));
                    int bb = (int)(a.b + f * (b.b - a.b));
                    return ((byte)r, (byte)g, (byte)bb);
                }
            }
            return (253, 253, 190);
        }
    }
}