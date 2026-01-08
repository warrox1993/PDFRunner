using ConvertPDF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ConvertPDF.Services
{
    public class ProfileManager
    {
        private readonly string _profilesPath;

        public ProfileManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ConvertPDF", "profiles");
            Directory.CreateDirectory(folder);
            _profilesPath = folder;
        }

        public List<ConversionProfile> LoadProfiles()
        {
            var profiles = new List<ConversionProfile>();
            
            try
            {
                var files = Directory.GetFiles(_profilesPath, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var profile = JsonSerializer.Deserialize<ConversionProfile>(json);
                        if (profile != null)
                            profiles.Add(profile);
                    }
                    catch { }
                }
            }
            catch { }

            // Always ensure Default profile exists
            if (!profiles.Any(p => p.Name == "Default"))
            {
                profiles.Insert(0, new ConversionProfile { Name = "Default" });
            }

            return profiles;
        }

        public void SaveProfile(ConversionProfile profile)
        {
            try
            {
                string fileName = SanitizeFileName(profile.Name) + ".json";
                string filePath = Path.Combine(_profilesPath, fileName);
                string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public void DeleteProfile(string profileName)
        {
            if (profileName == "Default") return; // Can't delete default

            try
            {
                string fileName = SanitizeFileName(profileName) + ".json";
                string filePath = Path.Combine(_profilesPath, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch { }
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
