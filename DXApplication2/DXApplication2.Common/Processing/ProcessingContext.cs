using DXApplication2.Common.Models;
using System;
using System.Collections.Generic;

namespace DXApplication2.Common.Processing
{
    /// <summary>
    /// 传给 IDataProcessor 的输入上下文。
    /// 简版(阶段 A)——Preprocessing/Roi/Background 先用 object,阶段 D-E 会替换成具体类型。
    /// </summary>
    public class ProcessingContext
    {
        /// <summary>要处理的 Records(至少 1 条)</summary>
        public List<DataRecord> Records { get; set; } = new();

        /// <summary>Records 所属的 Group(用于访问 GroupExperimentTypes 判断 Source)</summary>
        public DataGroup? Group { get; set; }

        /// <summary>预处理参数(阶段 D 替换为 PreprocessingConfig)</summary>
        public object? Preprocessing { get; set; }

        /// <summary>ROI 配置(阶段 E 替换为 RoiConfig)</summary>
        public object? Roi { get; set; }

        /// <summary>背景配置(阶段 E 替换为 BackgroundConfig)</summary>
        public object? Background { get; set; }

        /// <summary>用户指定的 X 轴字段;null 则自动选(比如多 Record 场景选方差最大的数值字段)</summary>
        public string? XAxisFieldName { get; set; }
        public string? YAxisFieldName { get; set; }
        public string? ZAxisFieldName { get; set; }   // Heatmap 的 Z 轴(颜色维度)
        /// <summary>进度反馈(0.0 ~ 1.0)</summary>
        public IProgress<(int current, int total, string status)>? Progress { get; set; }
        public class AvailableFieldsResult
        {
            public List<FieldOption> Fields { get; set; } = new();
            public string DefaultX { get; set; } = "";
            public string DefaultY { get; set; } = "";
        }

        public class FieldOption
        {
            public string FieldName { get; set; } = "";
            public string DisplayName { get; set; } = "";  // "DisplayName (Unit)"
        }
    }
}