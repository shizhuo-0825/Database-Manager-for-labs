using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 按 Source 分类的数据处理器接口。
    /// 每种 Source 一个实现:PshgProcessor / RaShgProcessor / ShgImagingProcessor / WhitelightProcessor.
    /// </summary>
    public interface IDataProcessor
    {
        /// <summary>支持的 Source 类型标识,跟 ExperimentType.Name 一致</summary>
        string SourceType { get; }

        /// <summary>异步执行处理,返回可渲染的数据</summary>
        Task<ProcessedData> ProcessAsync(
            ProcessingContext context,
            CancellationToken ct = default);
    }
}