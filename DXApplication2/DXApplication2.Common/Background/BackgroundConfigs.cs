using System.Collections.Generic;

namespace DXApplication2.Common.Background
{
    /// <summary>
    /// 全局唯一的 background 配置(所有类型共享一份存储,但内部分 Image / Pump 两个字段)。
    /// </summary>
    public class BackgroundConfig
    {
        /// <summary>是否应用到 PSHG raw processing(减去 pump artifact)</summary>
        public bool ApplyToPshgRaw { get; set; } = false;

        /// <summary>Image background(用于 SHG Image 处理)</summary>
        public ImageBackgroundInfo? Image { get; set; }

        /// <summary>Pump background(用于 PSHG artifact 去除)</summary>
        public PumpBackgroundInfo? Pump { get; set; }
        public double PumpScale { get; set; } = 1.0;
    }

    /// <summary>Image background:引用一个 DataRecord</summary>
    public class ImageBackgroundInfo
    {
        public int RecordId { get; set; }
        public string RecordName { get; set; } = "";     // 便于显示,不影响逻辑
        public string ImageFilePath { get; set; } = "";  // 冗余,方便快速查看
        public string SetTimestamp { get; set; } = "";
    }

    /// <summary>Pump background:一组 records + 拟合参数</summary>
    public class PumpBackgroundInfo
    {
        public List<int> RecordIds { get; set; } = new();
        public int GroupId { get; set; }
        public string GroupName { get; set; } = "";
        public string SetTimestamp { get; set; } = "";

        // 傅里叶分量拟合结果:
        //   y(θ) = FitA + Σ_{k=1..MaxK} FitBk[k-1] * cos(k × π/180 × θ_deg + FitPhik[k-1])
        // 其中 θ_deg = Probe_P_Analyzer 或(减背底时) Probe_P_Polarizer(测量时两者相等)
        public bool IsFitted { get; set; } = false;
        public double FitA { get; set; }
        public double[] FitBk { get; set; } = System.Array.Empty<double>();
        public double[] FitPhik { get; set; } = System.Array.Empty<double>();

        public const int MaxK = 4;
        

        /// <summary>用拟合结果算给定角度(度)的 y 值</summary>
        public double Evaluate(double thetaDeg)
        {
            if (!IsFitted) return 0;
            double y = FitA;
            double thetaRad = thetaDeg * System.Math.PI / 180.0;
            int len = System.Math.Min(FitBk.Length, FitPhik.Length);
            for (int i = 0; i < len; i++)
            {
                int k = i + 1;
                y += FitBk[i] * System.Math.Cos(k * thetaRad + FitPhik[i]);
            }
            return y;
        }
    }
}