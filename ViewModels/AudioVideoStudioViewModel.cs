using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConvertPDF.Models;
using ConvertPDF.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ConvertPDF.ViewModels
{
    public partial class AudioVideoStudioViewModel : ObservableObject
    {
        private readonly WhisperTranscriptionService _whisperService;
        private readonly YouTubeDownloadService _youtubeService;
        private readonly MediaConversionService _mediaService;
        
        private readonly TranslationService _translationService;
        
        private TranscriptionResult _lastResult;
        private CancellationTokenSource _cts;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private string _youtubeUrl;

        [ObservableProperty]
        private string _transcriptionText;

        [ObservableProperty]
        private WhisperModelInfo _selectedModel;

        // New Features
        [ObservableProperty]
        private bool _enableTranslation = false;

        [ObservableProperty]
        private string _selectedTargetLanguage = "en";

        [ObservableProperty]
        private bool _showTimestamps = true;

        [ObservableProperty]
        private string _selectedLanguage = "auto";

        [ObservableProperty]
        private string _backendInfo = "Detecting...";

        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string>
        {
            "auto", "fr", "en", "es", "de", "it", "ja", "zh", "pt", "ru"
        };

        public ObservableCollection<string> TargetLanguages { get; } = new ObservableCollection<string>
        {
            "en", "fr", "es", "de", "it", "ja", "zh", "pt", "ru"
        };

        public ObservableCollection<WhisperModelInfo> AvailableModels { get; } = new ObservableCollection<WhisperModelInfo>();

        public AudioVideoStudioViewModel(
            WhisperTranscriptionService whisperService,
            YouTubeDownloadService youtubeService,
            MediaConversionService mediaService,
            TranslationService translationService)
        {
            _whisperService = whisperService;
            _youtubeService = youtubeService;
            _mediaService = mediaService;
            _translationService = translationService;
            
            LoadModels();
            
            // Get GPU status
            BackendInfo = $"Engine: Whisper.net | Backend: {_whisperService.DetectedBackend}";
        }

        private void LoadModels()
        {
            var status = _whisperService.GetAllModelsStatus();
            AvailableModels.Clear();
            foreach (var s in status) AvailableModels.Add(s);
            
            // Select first downloaded model, or first in list if none downloaded
            SelectedModel = AvailableModels.FirstOrDefault(m => m.IsDownloaded) 
                ?? AvailableModels.FirstOrDefault();
        }

        [RelayCommand]
        private async Task DownloadModel(WhisperModelInfo model)
        {
            if (model == null || model.IsDownloading || model.IsDownloaded) return;

            model.IsDownloading = true;
            StatusMessage = $"Downloading {model.Name}...";
            
            try
            {
                var progress = new Progress<(int percent, string message)>(report => 
                {
                    model.DownloadProgress = report.percent;
                    ProgressValue = report.percent;
                    StatusMessage = report.message;
                });

                await _whisperService.DownloadModelAsync(model.Type, progress);
                
                // Verify model actually exists on disk
                bool verified = _whisperService.IsModelDownloaded(model.Type);
                model.IsDownloaded = verified;
                model.DownloadProgress = verified ? 100 : 0;
                StatusMessage = verified 
                    ? $"✅ {model.Name} downloaded successfully." 
                    : $"⚠️ {model.Name} download may have failed.";
            }
            catch (Exception ex)
            {
                model.IsDownloaded = false;
                StatusMessage = $"Download failed. {ex.Message}";
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                model.IsDownloading = false;
                ProgressValue = 0;
            }
        }

        partial void OnShowTimestampsChanged(bool value)
        {
            // Re-format the displayed text when toggle changes
            if (_lastResult != null)
            {
                TranscriptionText = value 
                    ? CaptionExporter.FormatWithTimestamps(_lastResult) 
                    : _lastResult.FullText;
            }
        }

        [RelayCommand]
        private void Stop()
        {
            _cts?.Cancel();
            StatusMessage = "Stopping...";
        }

        [RelayCommand]
        private async Task DownloadYoutube()
        {
            if (string.IsNullOrWhiteSpace(YoutubeUrl)) return;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            IsBusy = true;
            StatusMessage = "Analyzing YouTube video...";
            ProgressValue = 0;

            try
            {
                var progress = new Progress<(int percent, string message)>(report => 
                {
                    ProgressValue = report.percent;
                    StatusMessage = report.message;
                });

                string tempFolder = Path.Combine(Path.GetTempPath(), "ConvertPDF_Audio");
                Directory.CreateDirectory(tempFolder);

                var result = await _youtubeService.DownloadAudioAsync(YoutubeUrl, tempFolder, progress, _cts.Token);
                
                StatusMessage = $"Downloaded: {result.title}";
                
                await TranscribeFile(result.filePath);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Operation cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"YouTube Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PickFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media Files|*.mp3;*.wav;*.m4a;*.mp4;*.mov;*.mkv|All Files|*.*",
                Title = "Select Audio or Video File"
            };

            if (dialog.ShowDialog() == true)
            {
                await TranscribeFile(dialog.FileName);
            }
        }

        private async Task TranscribeFile(string filePath)
        {
            // Create new CTS if not already set (called directly via PickFile)
            if (_cts == null || _cts.IsCancellationRequested)
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
            }

            IsBusy = true;
            StatusMessage = "Preparing audio for transcription...";
            ProgressValue = 0;

            try
            {
                var modelProgress = new Progress<(int percent, string message)>(report =>
                {
                    ProgressValue = report.percent;
                    StatusMessage = report.message;
                });

                // Download model if needed
                if (!_whisperService.IsModelDownloaded(SelectedModel.Type))
                {
                    StatusMessage = $"Downloading Whisper Model ({SelectedModel.Name})...";
                    await _whisperService.DownloadModelAsync(SelectedModel.Type, modelProgress, _cts.Token);
                }

                _cts.Token.ThrowIfCancellationRequested();

                // Convert to compatible WAV
                StatusMessage = "Converting media to 16kHz WAV...";
                string wavPath = await _mediaService.ConvertToWav16KhzAsync(filePath, Path.GetDirectoryName(filePath));

                _cts.Token.ThrowIfCancellationRequested();

                // Transcribe
                StatusMessage = "Transcribing with Whisper AI...";
                string modelPath = await _whisperService.DownloadModelAsync(SelectedModel.Type, modelProgress, _cts.Token);

                var transProgress = new Progress<(int percent, string message)>(report => 
                {
                    ProgressValue = report.percent;
                    StatusMessage = report.message;
                });

                // Clear previous text for real-time update
                TranscriptionText = "";

                // Whisper native translation is only to English.
                bool whisperTranslate = EnableTranslation && SelectedTargetLanguage == "en";

                _lastResult = await _whisperService.TranscribeAsync(
                    wavPath, 
                    modelPath, 
                    SelectedLanguage, 
                    whisperTranslate, 
                    transProgress,
                    onSegmentReady: segment => 
                    {
                        // Update UI progressively
                        System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        {
                            string newText = ShowTimestamps 
                                ? $"[{segment.Start:mm\\:ss}] {segment.Text}\n" 
                                : segment.Text + " ";
                                
                            TranscriptionText += newText;
                        });
                    },
                    cancellationToken: _cts.Token);

                // Multi-language Translation if enabled and NOT English (Whisper handled English)
                if (EnableTranslation && SelectedTargetLanguage != "en")
                {
                    StatusMessage = $"Translating to {SelectedTargetLanguage}...";
                    ProgressValue = 90;
                    
                    // Translate full text
                    string translatedText = await _translationService.TranslateAsync(_lastResult.FullText, SelectedTargetLanguage, SelectedLanguage);
                    _lastResult.FullText = translatedText;

                    // Translate segments for timestamps in parallel (with throttling)
                    using (var semaphore = new System.Threading.SemaphoreSlim(5))
                    {
                        var translationTasks = _lastResult.Segments.Select(async segment =>
                        {
                            await semaphore.WaitAsync(_cts.Token);
                            try
                            {
                                segment.Text = await _translationService.TranslateAsync(segment.Text, SelectedTargetLanguage, SelectedLanguage);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });
                        await Task.WhenAll(translationTasks);
                    }
                }

                // Display based on timestamp toggle
                TranscriptionText = ShowTimestamps 
                    ? CaptionExporter.FormatWithTimestamps(_lastResult) 
                    : _lastResult.FullText;
                    
                StatusMessage = "Transcription Complete!";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Transcription cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed";
                MessageBox.Show($"Transcription Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CopyText()
        {
            if (!string.IsNullOrEmpty(TranscriptionText))
            {
                System.Windows.Clipboard.SetText(TranscriptionText);
                StatusMessage = "Copied to clipboard!";
            }
        }

        [RelayCommand]
        private void ExportSrt()
        {
            if (_lastResult == null || !_lastResult.Segments.Any())
            {
                MessageBox.Show("No transcription to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "SubRip Subtitle (*.srt)|*.srt",
                Title = "Export as SRT",
                FileName = "transcription.srt"
            };

            if (dialog.ShowDialog() == true)
            {
                var srtContent = CaptionExporter.ExportToSrt(_lastResult);
                CaptionExporter.SaveToFile(srtContent, dialog.FileName);
                StatusMessage = $"Exported to {Path.GetFileName(dialog.FileName)}";
            }
        }

        [RelayCommand]
        private void ExportVtt()
        {
            if (_lastResult == null || !_lastResult.Segments.Any())
            {
                MessageBox.Show("No transcription to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "WebVTT (*.vtt)|*.vtt",
                Title = "Export as VTT",
                FileName = "transcription.vtt"
            };

            if (dialog.ShowDialog() == true)
            {
                var vttContent = CaptionExporter.ExportToVtt(_lastResult);
                CaptionExporter.SaveToFile(vttContent, dialog.FileName);
                StatusMessage = $"Exported to {Path.GetFileName(dialog.FileName)}";
            }
        }
    }
}

