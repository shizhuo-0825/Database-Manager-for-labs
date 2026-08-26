using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DXApplication2.Common.Models;


namespace DXApplication2.Common.Messages
{
    // 让当前 Module 保存数据
    public class SaveCurrentModuleMessage
    {
    }

    // 让当前 Module 刷新数据(可能带一个可选的 folder path)
    public class RefreshCurrentModuleMessage
    {
        public string? FolderPath { get; set; }
    }

    // 用户在 Group Module 里双击了某个 Group,请求跳转到 Records
    public class OpenRecordsForGroupMessage
    {
        public int GroupId { get; set; }
    }

    // 用户 Open Folder 后广播:所有 Module 都要根据这个 folder 过滤
    public class FolderChangedMessage
    {
        public string? FolderPath { get; set; }   // null 表示"清除过滤,显示全部"
    }

    public class LoadRecordsForGroupMessage
    {
        public int GroupId { get; set; }
    }
    /// <summary>用户请求画 Line Plot(可能是 Ribbon 触发)</summary>
    public class RequestLinePlotMessage
    {
        // 空:PlotModule 会从缓存里拿最新的 SelectedRecords
        public List<DataRecord> Records { get; set; } = new();
    }

    /// <summary>Records 选中项变化(从 GroupModule 或 RecordsModule 广播)</summary>
    public class SelectedRecordsChangedMessage
    {
        public List<DataRecord> Records { get; set; } = new();
        public string Source { get; set; } = "";   // "Group" or "Records"
    }
    public class RequestScatterPlotMessage
    {
        public List<DataRecord> Records { get; set; } = new();
    }
    public class RequestHeatmapMessage
    {
        public List<DataRecord> Records { get; set; } = new();
    }
    public class RequestPreviewMessage
    {
        public DataRecord? Record { get; set; }
        public PreviewMode InitialMode { get; set; } = PreviewMode.Raw;
    }
    public class RequestPolarPlotMessage
    {
        public List<DataRecord> Records { get; set; } = new();
        public bool WithFill { get; set; } = false;
    }
    public class RequestBackgroundMessage
    {
        // 打开 Background Module,不带数据 - Module 自己 Load
    }

    public class SetImageBackgroundMessage
    {
        public DataRecord? Record { get; set; }
    }
    public class PumpScaleChangedMessage
    {
        public double Scale { get; set; }
    }
    public class SetPumpBackgroundMessage
    {
        public List<DataRecord> Records { get; set; } = new();
        public int GroupId { get; set; }
        public string GroupName { get; set; } = "";
    }
}