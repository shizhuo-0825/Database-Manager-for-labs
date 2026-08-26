using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 一个可选的字段(用于 UI 下拉列表)
    /// </summary>
    public class FieldOption
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;   // "DisplayName (Unit)"

        // 便于下拉框直接显示
        public override string ToString() => DisplayLabel;

        // 便于 Equals 判断
        public override bool Equals(object? obj) => obj is FieldOption other && FieldName == other.FieldName;
        public override int GetHashCode() => FieldName.GetHashCode();
    }
}
