using System.Collections.Generic;

namespace DXApplication2.Common.Roi
{
    /// <summary>用户当前在 Preview 里选中的 ROI(每个 Tag 一个)</summary>
    public static class ActiveRoiState
    {
        // Tag → Timestamp(用户选的);null = 用最新
        private static readonly Dictionary<string, string?> _activeTimestamps = new();

        public static void Set(string tag, string? timestamp)
        {
            _activeTimestamps[tag] = timestamp;
        }

        public static string? Get(string tag)
        {
            return _activeTimestamps.TryGetValue(tag, out var ts) ? ts : null;
        }
    }
}