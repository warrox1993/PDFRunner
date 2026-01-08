using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace ConvertPDF.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;

        public TranslationService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // Prevent indefinite blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = "auto")
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            if (targetLanguage == sourceLanguage) return text;

            try
            {
                // Using a public endpoint (example: MyMemory or similar free API for demo purposes)
                // For a production app, this would typically use a paid API like Google Cloud or DeepL.
                string url = $"https://api.mymemory.translated.net/get?q={HttpUtility.UrlEncode(text)}&langpair={sourceLanguage}|{targetLanguage}";
                
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var translatedText = doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();
                    return HttpUtility.HtmlDecode(translatedText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
            }

            return text; // Fallback to original text if translation fails
        }
    }
}
