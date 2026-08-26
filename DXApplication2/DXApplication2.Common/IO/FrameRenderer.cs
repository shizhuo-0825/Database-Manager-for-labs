using DXApplication2.Common.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;

//using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXApplication2.Common.IO
{
    /// <summary>
    /// 单帧渲染:矩阵 → magma → resize 到指定尺寸 → 叠加左上角文字。
    /// </summary>
    public static class FrameRenderer
    {
        public const int OutputSize = 512;
        public const int FontSize = 16;

        /// <summary>
        /// 生成一帧图像。
        /// </summary>
        /// <param name="matrix">数据矩阵</param>
        /// <param name="overlayLines">左上角显示的文字(每行一个 string)</param>
        /// <param name="globalMin">colormap 归一化的 min</param>
        /// <param name="globalMax">colormap 归一化的 max</param>
        public static Image<Rgba32> Render(double[,] matrix,
            IEnumerable<string> overlayLines,
            double globalMin, double globalMax)
        {
            matrix = DXApplication2.Common.IO.MatrixFlipHelper.FlipY(matrix);
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            // 1. 归一化 + magma → RGB
            var img = new Image<Rgba32>(cols, rows);
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) v = globalMin;
                    var t = (globalMax > globalMin)
                        ? (v - globalMin) / (globalMax - globalMin)
                        : 0.0;
                    if (t < 0) t = 0; if (t > 1) t = 1;
                    var (r, g, b) = MagmaColor(t);
                    img[j, i] = new Rgba32((byte)r, (byte)g, (byte)b, 255);
                }
            }

            // 2. resize 到目标尺寸(保持宽高比,不足处填黑)
            float scale = System.Math.Min((float)OutputSize / cols, (float)OutputSize / rows);
            int outW = System.Math.Max(1, (int)(cols * scale));
            int outH = System.Math.Max(1, (int)(rows * scale));

            img.Mutate(ctx => ctx.Resize(outW, outH, KnownResamplers.NearestNeighbor));

            // 中心 pad 到正方形
            var final = new Image<Rgba32>(OutputSize, OutputSize, new Rgba32(0, 0, 0, 255));
            int padX = (OutputSize - outW) / 2;
            int padY = (OutputSize - outH) / 2;
            final.Mutate(ctx => ctx.DrawImage(img, new Point(padX, padY), 1f));
            img.Dispose();

            // 3. 左上角叠加文字
            try
            {
                var fontFamily = SystemFonts.TryGet("Arial", out var family)
                    ? family
                    : SystemFonts.Families.First();
                var font = fontFamily.CreateFont(FontSize, FontStyle.Regular);

                float y = 8;
                foreach (var line in overlayLines)
                {
                    if (string.IsNullOrEmpty(line)) { y += FontSize + 2; continue; }
                    // 描边:黑色底,白色顶
                    final.Mutate(ctx => ctx.DrawText(line, font, Color.Black, new PointF(9, y + 1)));
                    final.Mutate(ctx => ctx.DrawText(line, font, Color.White, new PointF(8, y)));
                    y += FontSize + 4;
                }
            }
            catch (Exception)
            {
                // 字体加载失败,跳过文字叠加(不影响图像)
            }

            return final;
        }

        // Magma colormap 简版(跟 ThumbnailGenerator 一致)
        private static (byte r, byte g, byte b) MagmaColor(double t)
        {
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