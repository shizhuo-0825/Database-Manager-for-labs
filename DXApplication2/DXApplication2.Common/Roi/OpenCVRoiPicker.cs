using DXApplication2.Common.IO;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace DXApplication2.Common.Roi
{
    public static class OpenCvRoiPicker
    {
        /// <summary>
        /// 弹出 OpenCV 窗口让用户拖选 ROI。
        /// 内部会自动放大显示,选完后坐标缩回原始尺寸。
        /// </summary>
        public static RoiRect? PickRoi(double[,] matrix, string windowTitle = "Select ROI (Enter=confirm, ESC=cancel)")
        {
            if (matrix == null) return null;
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            if (rows == 0 || cols == 0) return null;
            matrix = MatrixFlipHelper.FlipY(matrix);
            // 1. 归一化到 0-255
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            if (min == max) { min = 0; max = 1; }

            var bytes = new byte[rows * cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var v = matrix[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) v = min;
                    var normalized = (v - min) / (max - min);
                    if (normalized < 0) normalized = 0;
                    if (normalized > 1) normalized = 1;
                    bytes[i * cols + j] = (byte)(normalized * 255);
                }
            }

            // 2. 转 Mat
            using var gray = new Mat(rows, cols, MatType.CV_8UC1);
            Marshal.Copy(bytes, 0, gray.Data, bytes.Length);

            // 3. 应用 Magma colormap
            using var colored = new Mat();
            Cv2.ApplyColorMap(gray, colored, ColormapTypes.Magma);

            // 4. 放大显示(目标最长边约 800 像素)
            int scale = Math.Max(1, 1024 / Math.Max(rows, cols));
            using var enlarged = new Mat();
            Cv2.Resize(colored, enlarged,
                new Size(cols * scale, rows * scale),
                0, 0,
                InterpolationFlags.Nearest);   // 最邻近:保持像素方块感

            // 5. 弹窗选 ROI
            try
            {
                var rect = Cv2.SelectROI(windowTitle, enlarged, showCrosshair: false, fromCenter: false);
                Cv2.DestroyWindow(windowTitle);

                if (rect.Width <= 0 || rect.Height <= 0)
                    return null;

                // rect 是在翻转视图上画的,需要把 Y 翻回原图坐标
                int flippedRowStart = rect.Y / scale;
                int flippedRowEnd = (rect.Y + rect.Height) / scale;
                var (origRowStart, origRowEnd) = MatrixFlipHelper.UnflipRowRange(flippedRowStart, flippedRowEnd, rows);

                return new RoiRect
                {
                    RowStart = origRowStart,
                    RowEnd = origRowEnd,
                    ColStart = rect.X / scale,
                    ColEnd = (rect.X + rect.Width) / scale
                };
            }
            catch (Exception)
            {
                try { Cv2.DestroyWindow(windowTitle); } catch { }
                return null;
            }
        }
    }
}