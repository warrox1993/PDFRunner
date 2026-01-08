using System;
using System.IO;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace ConvertPDF.Services
{
    public class MediaConversionService
    {
        private bool _isInitialized = false;

        public async Task InitializeAsync(string ffmpegPath = "FFmpeg")
        {
            if (_isInitialized) return;

            // Check if FFmpeg is available, if not download it
            string exePath = Path.Combine(ffmpegPath, "ffmpeg.exe");
            
            if (!File.Exists(exePath))
            {
               // Auto-download FFmpeg
               Directory.CreateDirectory(ffmpegPath);
               await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
            }

            FFmpeg.SetExecutablesPath(ffmpegPath);
            _isInitialized = true;
        }

        public async Task<string> ConvertToWav16KhzAsync(string inputPath, string outputFolder)
        {
            if (!_isInitialized) await InitializeAsync();

            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string outputPath = Path.Combine(outputFolder, $"{fileName}_16khz.wav");

            try
            {
                // Conversion parameters for Whisper: 16kHz, Mono, PCM S16LE
                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(inputPath);
                
                IConversion conversion = FFmpeg.Conversions.New()
                    .AddStream(mediaInfo.AudioStreams)
                    .SetOutput(outputPath)
                    .AddParameter("-ar 16000 -ac 1") // Set Sample Rate to 16kHz and Channels to Mono
                    .SetOutputFormat(Format.wav);

                await conversion.Start();

                if (!File.Exists(outputPath))
                {
                    throw new Exception("FFmpeg conversion completed but output file was not created.");
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Audio conversion failed: {ex.Message}. Ensure the media file is valid and not corrupted.", ex);
            }
        }
    }
}
