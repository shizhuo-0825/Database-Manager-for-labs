namespace DXApplication2.Common.Background
{
    public enum BackgroundMode
    {
        None,      // 未设置
        Image,     // 一个 record 的 preprocessed 图,用于减 SHG Image
        Pump       // 一组 records 的拟合结果,用于减 PSHG 的 pump artifact
    }
}