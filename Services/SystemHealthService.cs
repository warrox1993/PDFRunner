using System;
using System.IO;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Whisper.net;

namespace ConvertPDF.Services
{
    /// <summary>
    /// System Health Service for diagnostics and auto-repair of dependencies.
    /// Checks FFmpeg availability and Whisper model integrity.
    /// </summary>
    public class SystemHealthService
    {
        private const string FFmpegFolder = "FFmpeg";
        private const string ModelsFolder = "Models";

        public bool IsFFmpegHealthy { get; private set; }
        public bool IsWhisperHealthy { get; private set; }
        public string DiagnosticsReport { get; private set; } = "";

        public async Task<bool> RunDiagnosticsAsync()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== System Health Report ===");
            report.AppendLine($"Time: {DateTime.Now}");
            report.AppendLine();

            // Check FFmpeg
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FFmpegFolder, "ffmpeg.exe");
            IsFFmpegHealthy = File.Exists(ffmpegPath);
            report.AppendLine($"FFmpeg: {(IsFFmpegHealthy ? "✓ OK" : "✗ MISSING")}");

            // Check Whisper Models
            string modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelsFolder);
            if (Directory.Exists(modelsPath))
            {
                var models = Directory.GetFiles(modelsPath, "*.bin");
                IsWhisperHealthy = models.Length > 0;
                report.AppendLine($"Whisper Models: {(IsWhisperHealthy ? $"✓ {models.Length} found" : "✗ NONE")}");
            }
            else
            {
                IsWhisperHealthy = false;
                report.AppendLine("Whisper Models: ✗ Folder missing");
            }

            DiagnosticsReport = report.ToString();
            return IsFFmpegHealthy && IsWhisperHealthy;
        }

        public async Task RepairFFmpegAsync(IProgress<string>? progress = null)
        {
            progress?.Report("Downloading FFmpeg...");
            string ffmpegFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FFmpegFolder);

            // Delete corrupted files
            if (Directory.Exists(ffmpegFolder))
                Directory.Delete(ffmpegFolder, true);

            Directory.CreateDirectory(ffmpegFolder);
            await Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(
                Xabe.FFmpeg.Downloader.FFmpegVersion.Official, ffmpegFolder);

            progress?.Report("FFmpeg installed successfully.");
            IsFFmpegHealthy = true;
        }

        public void ClearWhisperModels()
        {
            string modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelsFolder);
            if (Directory.Exists(modelsPath))
                Directory.Delete(modelsPath, true);

            Directory.CreateDirectory(modelsPath);
            IsWhisperHealthy = false;
        }
    }
}
