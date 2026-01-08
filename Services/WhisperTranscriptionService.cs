using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using ConvertPDF.Models;
using System.Collections.Generic;
using System.Linq;

namespace ConvertPDF.Services
{
    public class WhisperTranscriptionService
    {
        private static readonly string ModelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConvertPDF", "WhisperModels");
        
        public string DetectedBackend { get; private set; } = "CPU";
        public bool IsGpuAvailable { get; private set; } = false;

        public WhisperTranscriptionService()
        {
            if (!Directory.Exists(ModelsDirectory))
            {
                Directory.CreateDirectory(ModelsDirectory);
            }
            
            DetectGpuCapabilities();
        }

        private void DetectGpuCapabilities()
        {
            try
            {
                // Check for NVIDIA GPU by looking for CUDA DLLs or environment
                var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
                bool hasCuda = !string.IsNullOrEmpty(cudaPath) || File.Exists(@"C:\Windows\System32\nvcuda.dll");
                
                if (hasCuda)
                {
                    IsGpuAvailable = true;
                    DetectedBackend = "CUDA (NVIDIA GPU)";
                }
                else
                {
                    // Check for AVX2 support (faster CPU)
                    DetectedBackend = "CPU (AVX2)";
                }
            }
            catch
            {
                DetectedBackend = "CPU (Standard)";
            }
        }

        public async Task<string> DownloadModelAsync(WhisperModelType modelType, IProgress<(int percent, string message)> progress = null, CancellationToken cancellationToken = default)
        {
            var ggmlType = modelType switch
            {
                WhisperModelType.Tiny => GgmlType.Tiny,
                WhisperModelType.Base => GgmlType.Base,
                WhisperModelType.Small => GgmlType.Small,
                WhisperModelType.Medium => GgmlType.Medium,
                WhisperModelType.LargeV3 => GgmlType.LargeV3,
                _ => GgmlType.Base
            };

            string modelFileName = $"ggml-{ggmlType.ToString().ToLower()}.bin";
            string modelPath = Path.Combine(ModelsDirectory, modelFileName);
            string tempPath = modelPath + ".tmp";

            // Si le modèle existe ET est valide
            if (File.Exists(modelPath) && IsModelValid(modelPath, modelType))
            {
                progress?.Report((100, "Model ready."));
                return modelPath;
            }

            // Nettoyer les fichiers temporaires/corrompus
            if (File.Exists(modelPath)) File.Delete(modelPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);

            long expectedSize = GetEstimatedModelSize(modelType);
            progress?.Report((0, $"Downloading {modelType} model (~{expectedSize / (1024 * 1024)} MB)..."));

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromHours(2); // Plus généreux pour connexions lentes
                
                var downloader = new WhisperGgmlDownloader(httpClient);
                
                // Download with progress tracking
                using var modelStream = await downloader.GetGgmlModelAsync(ggmlType);
                
                // Télécharger dans un fichier temporaire d'abord
                using var fileStream = File.Create(tempPath);
                
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                long totalSize = modelStream.CanSeek ? modelStream.Length : expectedSize;
                
                while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;
                    
                    int percent = totalSize > 0 ? (int)((totalRead * 100) / totalSize) : 50;
                    string sizeMb = (totalRead / 1024.0 / 1024.0).ToString("F1");
                    string totalMb = (totalSize / 1024.0 / 1024.0).ToString("F1");
                    progress?.Report((percent, $"Downloading: {sizeMb} MB / {totalMb} MB"));
                }
                
                fileStream.Close(); // Fermer avant de renommer
                
                // Valider la taille du fichier téléchargé
                var fileInfo = new FileInfo(tempPath);
                
                if (fileInfo.Length < expectedSize * 0.95) // Tolérance de 5%
                {
                    File.Delete(tempPath);
                    throw new Exception($"Download incomplete: {fileInfo.Length / (1024*1024)} MB / {expectedSize / (1024*1024)} MB expected.");
                }
                
                // Déplacer le fichier validé
                File.Move(tempPath, modelPath);
                
                progress?.Report((100, "Download complete & verified!"));
                return modelPath;
            }
            catch (Exception ex)
            {
                // Nettoyer en cas d'erreur
                if (File.Exists(tempPath)) 
                {
                    try { File.Delete(tempPath); } catch { }
                }
                throw new Exception($"Model download failed: {ex.Message}. Please check your internet connection and try again.", ex);
            }
        }

        private bool IsModelValid(string modelPath, WhisperModelType modelType)
        {
            if (!File.Exists(modelPath)) return false;
            
            var fileInfo = new FileInfo(modelPath);
            long expectedSize = GetEstimatedModelSize(modelType);
            
            // Vérifier que le fichier est au moins 95% de la taille attendue
            return fileInfo.Length >= expectedSize * 0.95;
        }

        public List<WhisperModelInfo> GetAllModelsStatus()
        {
            var models = new List<WhisperModelInfo>();
            foreach (WhisperModelType type in Enum.GetValues(typeof(WhisperModelType)))
            {
                long size = GetEstimatedModelSize(type);
                models.Add(new WhisperModelInfo
                {
                    Type = type,
                    IsDownloaded = IsModelDownloaded(type),
                    SizeDisplay = $"{(size / 1024.0 / 1024.0):F0} MB"
                });
            }
            return models;
        }

        public long GetEstimatedModelSize(WhisperModelType modelType) => modelType switch
        {
            WhisperModelType.Tiny => 75 * 1024 * 1024,
            WhisperModelType.Base => 142 * 1024 * 1024,
            WhisperModelType.Small => 466 * 1024 * 1024,
            WhisperModelType.Medium => 1500 * 1024 * 1024,
            WhisperModelType.LargeV3 => 3100L * 1024 * 1024,
            _ => 150 * 1024 * 1024
        };

        public async Task<TranscriptionResult> TranscribeAsync(
            string audioFilePath, 
            string modelPath, 
            string language = "auto", 
            bool translateToEnglish = false,
            IProgress<(int percent, string message)> progress = null,
            Action<TranscriptionSegment> onSegmentReady = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(async () =>
            {
                progress?.Report((0, $"Loading Whisper model ({DetectedBackend})..."));
                
                using var factory = WhisperFactory.FromPath(modelPath);
                
                var builder = factory.CreateBuilder()
                    .WithLanguage(language)
                    .WithProbabilities();                   // Enable probabilities for filtering

                if (translateToEnglish)
                {
                    builder = builder.WithTranslate();
                }

                using var processor = builder.Build();

                using var fileStream = File.OpenRead(audioFilePath);

                var segments = new List<TranscriptionSegment>();
                string fullText = "";

                progress?.Report((10, "Starting transcription..."));

                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    // Filter low-probability segments (hallucinations)
                    if (segment.Probability < 0.5f)
                    {
                        continue; // Skip uncertain segments
                    }

                    var transSegment = new TranscriptionSegment
                    {
                        Start = segment.Start,
                        End = segment.End,
                        Text = segment.Text.Trim(),
                        Probability = segment.Probability
                    };
                    
                    segments.Add(transSegment);
                    fullText += " " + segment.Text.Trim(); // Add space between segments
                    
                    // Notify progress in real-time
                    onSegmentReady?.Invoke(transSegment);
                    
                    progress?.Report((50, $"[{segment.Start:mm\\:ss}] {segment.Text.Substring(0, Math.Min(30, segment.Text.Length))}..."));
                }

                progress?.Report((100, "Transcription complete!"));

                return new TranscriptionResult
                {
                    FullText = fullText.Trim(),
                    Segments = segments,
                    Language = language
                };
            });
        }
        
        public bool IsModelDownloaded(WhisperModelType modelType)
        {
            var ggmlType = modelType switch
            {
                WhisperModelType.Tiny => GgmlType.Tiny,
                WhisperModelType.Base => GgmlType.Base,
                WhisperModelType.Small => GgmlType.Small,
                WhisperModelType.Medium => GgmlType.Medium,
                WhisperModelType.LargeV3 => GgmlType.LargeV3,
                _ => GgmlType.Base
            };
            string modelFileName = $"ggml-{ggmlType.ToString().ToLower()}.bin";
            return File.Exists(Path.Combine(ModelsDirectory, modelFileName));
        }
    }
}

