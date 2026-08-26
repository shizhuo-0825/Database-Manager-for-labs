using System;
using System.IO;
using System.Text.Json;

namespace DXApplication2.Common.Preprocessing
{
    /// <summary>
    /// 磁盘 JSON 存储 PreprocessingConfig。
    /// 全局唯一配置,所有 Source 共享。
    /// </summary>
    public static class PreprocessingConfigStore
    {
        private static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SHGManager");

        private static string ConfigPath => Path.Combine(ConfigDir, "preprocessing.json");

        public static PreprocessingConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new PreprocessingConfig();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<PreprocessingConfig>(json) ?? new PreprocessingConfig();
            }
            catch
            {
                return new PreprocessingConfig();
            }
        }

        public static void Save(PreprocessingConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}