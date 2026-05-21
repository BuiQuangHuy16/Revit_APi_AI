using System;
using System.IO;
using System.Text.Json;
using Aplication.Commands.AutoDimColumns.Models;

namespace Aplication.Commands.AutoDimColumns.Services
{
    public static class SettingsStore
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitAPP_AI");

        private static readonly string SettingsPath =
            Path.Combine(SettingsFolder, "column-dim-settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static AutoDimOptions Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new AutoDimOptions();
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AutoDimOptions>(json) ?? new AutoDimOptions();
            }
            catch
            {
                return new AutoDimOptions();
            }
        }

        public static void Save(AutoDimOptions options)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder)) Directory.CreateDirectory(SettingsFolder);
                var json = JsonSerializer.Serialize(options, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // swallow — setting persistence is best-effort
            }
        }
    }
}
