using System;

namespace DXApplication2.Common.Plotting
{
    /// <summary>
    /// 把非均匀 (xs, ys, matrix) 重采样成均匀矩阵:每个目标像素取"包含该点的源 cell"的值。
    /// 结果是 Origin 风格的锐利阶梯,没有插值糊化。
    /// NaN cell 用最近的非 NaN 源 cell 填充(索引空间最近邻,极快)。
    /// </summary>
    public static class HeatmapInterpolator
    {
        public static double[,] ResampleToUniform(
            double[,] sparse, double[] xs, double[] ys,
            int outWidth, int outHeight)
        {
            int ny = ys.Length, nx = xs.Length;
            double xMin = xs[0], xMax = xs[^1], yMin = ys[0], yMax = ys[^1];
            double xR = xMax - xMin, yR = yMax - yMin;

            var result = new double[outHeight, outWidth];
            if (xR < 1e-15 || yR < 1e-15)
            {
                for (int i = 0; i < outHeight; i++)
                    for (int j = 0; j < outWidth; j++) result[i, j] = double.NaN;
                return result;
            }

            // 预填 NaN cell 用最近非 NaN 源(在源网格索引空间内 BFS 或暴力)
            var filled = FillSourceNaNs(sparse, xs, ys);

            for (int py = 0; py < outHeight; py++)
            {
                // py=0 → yMin(bottom),与 MatrixFlipHelper.FlipY 约定一致
                double wy = yMin + (double)py / (outHeight - 1) * yR;
                int si = NearestIndex(ys, wy);
                for (int px = 0; px < outWidth; px++)
                {
                    double wx = xMin + (double)px / (outWidth - 1) * xR;
                    int sj = NearestIndex(xs, wx);
                    result[py, px] = filled[si, sj];
                }
            }
            return result;
        }

        private static double[,] FillSourceNaNs(double[,] src, double[] xs, double[] ys)
        {
            int ny = ys.Length, nx = xs.Length;
            var m = (double[,])src.Clone();

            // 收集已知点(索引 + 归一化坐标)
            double xMin = xs[0], xR = xs[^1] - xs[0];
            double yMin = ys[0], yR = ys[^1] - ys[0];
            var known = new System.Collections.Generic.List<(int i, int j, double nx, double ny, double z)>();
            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                    if (!double.IsNaN(m[i, j]))
                        known.Add((i, j, (xs[j] - xMin) / xR, (ys[i] - yMin) / yR, m[i, j]));

            if (known.Count == 0) return m;

            for (int i = 0; i < ny; i++)
                for (int j = 0; j < nx; j++)
                {
                    if (!double.IsNaN(m[i, j])) continue;
                    double tx = (xs[j] - xMin) / xR, ty = (ys[i] - yMin) / yR;
                    double best = double.MaxValue, bz = 0;
                    foreach (var k in known)
                    {
                        double dx = k.nx - tx, dy = k.ny - ty;
                        double d2 = dx * dx + dy * dy;
                        if (d2 < best) { best = d2; bz = k.z; }
                    }
                    m[i, j] = bz;
                }
            return m;
        }

        private static int NearestIndex(double[] arr, double v)
        {
            // 找 v 最接近的 arr 索引(二分)
            if (v <= arr[0]) return 0;
            if (v >= arr[^1]) return arr.Length - 1;
            int lo = 0, hi = arr.Length - 1;
            while (hi - lo > 1)
            {
                int m = (lo + hi) >> 1;
                if (arr[m] <= v) lo = m; else hi = m;
            }
            return (v - arr[lo] < arr[hi] - v) ? lo : hi;
        }
    }
}