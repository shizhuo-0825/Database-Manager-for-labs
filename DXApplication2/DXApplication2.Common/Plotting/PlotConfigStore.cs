using System;
using System.IO;
using System.Text.Json;

namespace DXApplication2.Common.Plotting
{
    public static class PlotConfigStore
    {
        private static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SHGManager");

        private static string ConfigPath => Path.Combine(ConfigDir, "plot_config.json");

        public static PlotConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new PlotConfig();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<PlotConfig>(json) ?? new PlotConfig();
            }
            catch
            {
                return new PlotConfig();
            }
        }

        public static void Save(PlotConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 保存失败不影响运行,静默忽略
            }
        }
    }
}