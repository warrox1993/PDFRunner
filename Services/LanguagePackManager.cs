using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ConvertPDF.Services
{
    public class LanguageInfo
    {
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public bool IsInstalled { get; set; }
    }

    public class LanguagePackManager
    {
        private readonly string _tessDataPath;
        private const string TessdataRepoUrl = "https://github.com/tesseract-ocr/tessdata_fast/raw/main/";

        private static readonly Dictionary<string, string> AvailableLanguages = new Dictionary<string, string>
        {
            { "eng", "English" },
            { "fra", "Français" },
            { "spa", "Español" },
            { "deu", "Deutsch" },
            { "ita", "Italiano" },
            { "por", "Português" },
            { "rus", "Русский" },
            { "ara", "العربية" },
            { "chi_sim", "中文(简体)" },
            { "jpn", "日本語" }
        };

        public LanguagePackManager()
        {
            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            Directory.CreateDirectory(_tessDataPath);
        }

        public List<LanguageInfo> GetAllLanguages()
        {
            return AvailableLanguages.Select(kvp => new LanguageInfo
            {
                Code = kvp.Key,
                DisplayName = kvp.Value,
                IsInstalled = IsLanguageInstalled(kvp.Key)
            }).ToList();
        }

        public List<LanguageInfo> GetInstalledLanguages()
        {
            return GetAllLanguages().Where(l => l.IsInstalled).ToList();
        }

        public bool IsLanguageInstalled(string langCode)
        {
            string filePath = Path.Combine(_tessDataPath, $"{langCode}.traineddata");
            return File.Exists(filePath);
        }

        public async Task<bool> DownloadLanguagePack(string langCode, IProgress<int> progress = null)
        {
            if (!AvailableLanguages.ContainsKey(langCode))
                return false;

            string fileName = $"{langCode}.traineddata";
            string targetPath = Path.Combine(_tessDataPath, fileName);

            if (File.Exists(targetPath))
                return true; // Already installed

            try
            {
                using (var client = new HttpClient())
                {
                    string url = TessdataRepoUrl + fileName;
                    
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1 && progress != null;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (canReportProgress)
                                {
                                    var progressPercentage = (int)((totalRead * 100) / totalBytes);
                                    progress.Report(progressPercentage);
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                // Cleanup partial download
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                return false;
            }
        }

        public string GetLanguageDisplayName(string langCode)
        {
            return AvailableLanguages.TryGetValue(langCode, out var displayName) ? displayName : langCode;
        }
    }
}
