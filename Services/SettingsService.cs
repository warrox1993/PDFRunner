using System;
using System.IO;
using System.Text.Json;

namespace ConvertPDF.Services
{
    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = false;
        public bool EnableOcr { get; set; } = false;
        public string LastOutputFolder { get; set; } = "";
        public string DefaultOutputFolder { get; set; } = "";
        public bool AlwaysAskForOutputFolder { get; set; } = true;
        public string PreferredOcrLanguage { get; set; } = "eng";
        public string DefaultCompressionLevel { get; set; } = "None";
        public bool WatchFolderEnabled { get; set; } = false;
        public string WatchInputPath { get; set; } = "";
        public string WatchOutputPath { get; set; } = "";
        public bool TurboMode { get; set; } = true; // "Boost" mode (Default)
    }

    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ConvertPDF");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
    }
}
