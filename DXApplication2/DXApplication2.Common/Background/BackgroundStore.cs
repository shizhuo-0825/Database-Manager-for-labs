using System;
using System.IO;
using System.Text.Json;

namespace DXApplication2.Common.Background
{
    /// <summary>
    /// Background 磁盘 JSON 存储。全局唯一,路径固定。
    /// </summary>
    public static class BackgroundStore
    {
        private static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SHGManager");

        private static string ConfigPath => Path.Combine(ConfigDir, "background.json");

        public static BackgroundConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new BackgroundConfig();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<BackgroundConfig>(json) ?? new BackgroundConfig();
            }
            catch
            {
                return new BackgroundConfig();
            }
        }

        public static void Save(BackgroundConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        public static string GenerateTimestamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }
}