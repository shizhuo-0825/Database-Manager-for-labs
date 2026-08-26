using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 阶段 A 占位处理器:把 Records 的 Id 和 ResultQuantity 当作 X/Y。
    /// 阶段 B 起会被真实的 PshgProcessor 等替换。
    /// </summary>
    public class DummyProcessor : IDataProcessor
    {
        public string SourceType => "Dummy";

        public Task<ProcessedData> ProcessAsync(ProcessingContext context, CancellationToken ct = default)
        {
            var recs = context.Records;

            // 用 Id 序列 + 简单函数,一定能看到线
            var x = System.Linq.Enumerable.Range(0, recs.Count).Select(i => (double)i).ToArray();
            var y = recs.Select((r, i) => r.ResultQuantity ?? (double)(i)).ToArray();  // 没值就用 i^2

            var result = new ProcessedData
            {
                SourceType = "Dummy",
                X = x,
                Y = y,
                XLabel = "RowIndex",
                YLabel = "Value",
                Title = $"Dummy Plot ({recs.Count} records)"
            };
            return Task.FromResult(result);
        }
    }
}