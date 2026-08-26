namespace DXApplication2.Common.ViewModels
{
    public class InspectorItem
    {
        public string Name { get; }
        public string Value { get; }

        /// <summary>缩略图路径(null = 普通键值项)</summary>
        public string? ThumbnailPath { get; }

        public bool IsThumbnail => !string.IsNullOrEmpty(ThumbnailPath);

        // 普通键值项构造
        public InspectorItem(string name, string value)
        {
            Name = name;
            Value = value ?? string.Empty;
            ThumbnailPath = null;
        }

        // 缩略图项构造
        public InspectorItem(string name, string thumbnailPath, bool isThumbnail)
        {
            Name = name;
            Value = string.Empty;
            ThumbnailPath = isThumbnail ? thumbnailPath : null;
        }
    }
}