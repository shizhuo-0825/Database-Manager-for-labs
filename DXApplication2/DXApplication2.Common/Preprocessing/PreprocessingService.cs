using System;
using System.Collections.Generic;
using System.Linq;

namespace DXApplication2.Common.Preprocessing
{
    /// <summary>
    /// Preprocessing 算法集合。
    /// 处理顺序:Hotspot → HardThreshold → GaussianBlur
    /// </summary>
    public static class PreprocessingService
    {
        /// <summary>
        /// 主入口:按序应用所有配置的处理。
        /// 返回新矩阵(不修改输入)。
        /// </summary>
        public static double[,] Apply(double[,] input, PreprocessingConfig config)
        {
            var result = (double[,])input.Clone();

            // 1. Hotspot removal
            if (config.HotspotSize > 0 && config.HotspotRatio > 0)
                result = RemoveHotspot(result, config.HotspotSize, config.HotspotRatio);

            // 2. Hard threshold
            if (config.HardThreshold.HasValue)
                result = RemoveHardThreshold(result, config.HardThreshold.Value);

            // 3. Gaussian blur
            if (config.GaussianSigma > 0)
                result = GaussianBlur(result, config.GaussianSigma);

            return result;
        }

        // ============================================================
        // 算法 1: Hotspot Removal
        // 用 sxs 方块检查每个像素,若中心 > 边缘中位数 * ratio,替换为中位数
        // ============================================================
        public static double[,] RemoveHotspot(double[,] input, int size, double ratio)
        {
            if (size <= 0) return input;
            if (size % 2 == 0) size++;

            int rows = input.GetLength(0);
            int cols = input.GetLength(1);
            var result = new double[rows, cols];
            int halfSize = size / 2;

            // Fast-path 阈值:大部分 pixel 会被这个滤掉
            double sum = 0;
            int count = 0;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    var v = input[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    sum += v;
                    count++;
                }
            double mean = count > 0 ? sum / count : 0;
            double fastThreshold = mean * ratio;   // 低于这个不可能是 hotspot

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var center = input[i, j];

                    // Fast-path:值不高的 pixel 直接复制
                    if (center <= fastThreshold || double.IsNaN(center))
                    {
                        result[i, j] = center;
                        continue;
                    }

                    // Slow-path:可能是 hotspot,做完整判断
                    var edgePixels = new List<double>();
                    for (int di = -halfSize; di <= halfSize; di++)
                    {
                        for (int dj = -halfSize; dj <= halfSize; dj++)
                        {
                            if (Math.Abs(di) != halfSize && Math.Abs(dj) != halfSize) continue;
                            int ni = i + di;
                            int nj = j + dj;
                            if (ni < 0 || ni >= rows || nj < 0 || nj >= cols) continue;
                            var val = input[ni, nj];
                            if (double.IsNaN(val) || double.IsInfinity(val)) continue;
                            edgePixels.Add(val);
                        }
                    }

                    if (edgePixels.Count == 0)
                    {
                        result[i, j] = center;
                        continue;
                    }

                    var median = Median(edgePixels);
                    result[i, j] = center > median * ratio ? median : center;
                }
            }

            return result;
        }

        // ============================================================
        // 算法 2: Hard Threshold Removal
        // pixel > threshold → 替换为周围<= threshold 的中位数
        // ============================================================
        public static double[,] RemoveHardThreshold(double[,] input, double threshold)
        {
            int rows = input.GetLength(0);
            int cols = input.GetLength(1);
            var result = (double[,])input.Clone();

            // 用一个 3x3 窗口找中位数(默认窗口)
            const int windowSize = 3;
            int half = windowSize / 2;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (input[i, j] <= threshold) continue;

                    // 收集周围小于 threshold 的值
                    var neighbors = new List<double>();
                    for (int di = -half; di <= half; di++)
                    {
                        for (int dj = -half; dj <= half; dj++)
                        {
                            if (di == 0 && dj == 0) continue;
                            int ni = i + di;
                            int nj = j + dj;
                            if (ni < 0 || ni >= rows || nj < 0 || nj >= cols) continue;
                            var v = input[ni, nj];
                            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                            if (v <= threshold) neighbors.Add(v);
                        }
                    }

                    if (neighbors.Count > 0)
                        result[i, j] = Median(neighbors);
                    else
                        result[i, j] = threshold;   // 兜底
                }
            }

            return result;
        }

        // ============================================================
        // 算法 3: Gaussian Blur
        // 分离式卷积:先横向再纵向(等价于 2D 卷积,但 O(n) 而非 O(n^2))
        // ============================================================
        public static double[,] GaussianBlur(double[,] input, double sigma)
        {
            if (sigma <= 0) return input;

            int rows = input.GetLength(0);
            int cols = input.GetLength(1);

            // 生成 1D Gaussian 核
            int radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
            int kernelSize = 2 * radius + 1;
            var kernel = new double[kernelSize];
            double sum = 0;
            for (int k = 0; k < kernelSize; k++)
            {
                double x = k - radius;
                kernel[k] = Math.Exp(-(x * x) / (2 * sigma * sigma));
                sum += kernel[k];
            }
            for (int k = 0; k < kernelSize; k++) kernel[k] /= sum;

            // 横向卷积
            var horizontal = new double[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double acc = 0;
                    double weight = 0;
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int nj = j + k - radius;
                        if (nj < 0 || nj >= cols) continue;
                        var v = input[i, nj];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        acc += v * kernel[k];
                        weight += kernel[k];
                    }
                    horizontal[i, j] = weight > 0 ? acc / weight : input[i, j];
                }
            }

            // 纵向卷积
            var result = new double[rows, cols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double acc = 0;
                    double weight = 0;
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int ni = i + k - radius;
                        if (ni < 0 || ni >= rows) continue;
                        var v = horizontal[ni, j];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        acc += v * kernel[k];
                        weight += kernel[k];
                    }
                    result[i, j] = weight > 0 ? acc / weight : horizontal[i, j];
                }
            }

            return result;
        }

        // ============================================================
        // 辅助:中位数
        // ============================================================
        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            if (sorted.Count % 2 == 0)
                return (sorted[mid - 1] + sorted[mid]) / 2.0;
            return sorted[mid];
        }
    }
}