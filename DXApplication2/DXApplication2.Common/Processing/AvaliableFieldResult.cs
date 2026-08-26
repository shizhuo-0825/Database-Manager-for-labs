using System.Collections.Generic;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 一次"获取可用字段"的结果:字段列表 + 默认 X/Y
    /// </summary>
    public class AvailableFieldsResult
    {
        public List<FieldOption> Fields { get; set; } = new();
        public string DefaultXFieldName { get; set; } = string.Empty;
        public string DefaultYFieldName { get; set; } = string.Empty;
    }
}