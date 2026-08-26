using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DXApplication2.Common.Roi
{
    /// <summary>
    /// ROI 磁盘存储(txt 文件)。
    /// 路径:%LocalAppData%\SHGManager\roi\{Tag}_{Timestamp}.txt
    /// 内容:row_start row_end col_start col_end(单行,空格分隔)
    /// </summary>
    public static class RoiStore
    {
        public static string RoiDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SHGManager", "roi");

        public static string GenerateTimestamp()
            => DateTime.Now.ToString("yyyyMMdd_HHmmss");

        public static string GetFilePath(string tag, string timestamp)
            => Path.Combine(RoiDir, $"{tag}_{timestamp}.txt");

        public static void Save(RoiRect roi)
        {
            Directory.CreateDirectory(RoiDir);
            var path = GetFilePath(roi.Tag, roi.Timestamp);
            var line = $"{roi.RowStart} {roi.RowEnd} {roi.ColStart} {roi.ColEnd}";
            File.WriteAllText(path, line);
        }

        public static RoiRect? Load(string tag, string timestamp)
        {
            var path = GetFilePath(tag, timestamp);
            if (!File.Exists(path)) return null;

            try
            {
                var content = File.ReadAllText(path).Trim();
                var parts = content.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return null;

                return new RoiRect
                {
                    Tag = tag,
                    Timestamp = timestamp,
                    RowStart = int.Parse(parts[0], CultureInfo.InvariantCulture),
                    RowEnd = int.Parse(parts[1], CultureInfo.InvariantCulture),
                    ColStart = int.Parse(parts[2], CultureInfo.InvariantCulture),
                    ColEnd = int.Parse(parts[3], CultureInfo.InvariantCulture),
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>列出某个 Tag 下所有已保存的 Timestamp,按时间倒序</summary>
        public static List<string> ListTimestamps(string tag)
        {
            if (!Directory.Exists(RoiDir)) return new List<string>();

            var prefix = $"{tag}_";
            var files = Directory.GetFiles(RoiDir, $"{prefix}*.txt");

            var timestamps = files
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(name => name.StartsWith(prefix))
                .Select(name => name.Substring(prefix.Length))
                .OrderByDescending(ts => ts)   // 倒序:最新在前
                .ToList();

            return timestamps;
        }

        /// <summary>删除某个 Tag+Timestamp 的 ROI 文件</summary>
        public static bool Delete(string tag, string timestamp)
        {
            var path = GetFilePath(tag, timestamp);
            if (!File.Exists(path)) return false;
            try
            {
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }
    }
}