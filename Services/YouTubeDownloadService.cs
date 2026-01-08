using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace ConvertPDF.Services
{
    public class YouTubeDownloadService
    {
        private readonly YoutubeClient _youtube;

        public YouTubeDownloadService()
        {
            _youtube = new YoutubeClient();
        }

        public async Task<(string filePath, string title, TimeSpan duration)> DownloadAudioAsync(
            string videoUrl, 
            string outputFolder, 
            IProgress<(int percent, string message)> progress = null,
            CancellationToken cancellationToken = default)
        {
            string outputPath = null;
            try
            {
                progress?.Report((10, "Fetching video metadata..."));
                cancellationToken.ThrowIfCancellationRequested();
                
                var video = await _youtube.Videos.GetAsync(videoUrl, cancellationToken);
                string cleanTitle = CleanFileName(video.Title);

                progress?.Report((20, "Getting stream manifest..."));
                cancellationToken.ThrowIfCancellationRequested();
                
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id, cancellationToken);

                // Optimisation : Prendre une qualité raisonnable au lieu de la maximale
                var audioStreams = streamManifest.GetAudioOnlyStreams().OrderBy(s => s.Bitrate).ToList();
                
                var streamInfo = audioStreams
                    .FirstOrDefault(s => s.Bitrate.KiloBitsPerSecond >= 96 && s.Bitrate.KiloBitsPerSecond <= 160)
                    ?? audioStreams.FirstOrDefault();

                if (streamInfo == null)
                {
                    throw new Exception("No audio stream found for this video.");
                }

                string extension = streamInfo.Container.Name;
                outputPath = Path.Combine(outputFolder, $"{cleanTitle}.{extension}");

                progress?.Report((30, $"Downloading audio ({streamInfo.Bitrate.KiloBitsPerSecond:F0}kbps)..."));
                
                var progressHandler = new Progress<double>(p => 
                {
                    int percent = 30 + (int)(p * 70);
                    progress?.Report((percent, $"Downloading: {p:P0}"));
                });
                
                await _youtube.Videos.Streams.DownloadAsync(streamInfo, outputPath, progressHandler, cancellationToken);

                progress?.Report((100, "Download complete!"));

                return (outputPath, video.Title, video.Duration ?? TimeSpan.Zero);
            }
            catch (OperationCanceledException)
            {
                // Clean up partial file on cancellation
                if (outputPath != null && File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"YouTube download failed: {ex.Message}", ex);
            }
        }

        private string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), "_"));
        }
    }
}
