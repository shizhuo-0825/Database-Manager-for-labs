namespace DXApplication2.Common.Preprocessing
{
    /// <summary>
    /// Preprocessing 参数(所有 Source 共享一套)。
    /// 处理顺序:Hotspot → Hard Threshold → Gaussian Blur
    /// </summary>
    public class PreprocessingConfig
    {
        /// <summary>
        /// 硬性阈值:超过此值的 pixel 会被替换为周围小于此值的中位数。
        /// null / 极大值 = 不处理。
        /// </summary>
        public double? HardThreshold { get; set; }

        /// <summary>
        /// 高斯模糊标准差(sigma)。0 = 不模糊。
        /// </summary>
        public double GaussianSigma { get; set; } = 0;

        /// <summary>
        /// Hotspot 检测方块大小(奇数,推荐 3/5/7)。0 = 不处理 hotspot。
        /// </summary>
        public int HotspotSize { get; set; } = 0;

        /// <summary>
        /// Hotspot 检测比例:中心值 > 边缘中位数 × 此值时,判为 hotspot 并替换为中位数。
        /// </summary>
        public double HotspotRatio { get; set; } = 3.0;
    }
}