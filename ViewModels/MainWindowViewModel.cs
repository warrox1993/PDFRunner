using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConvertPDF.Services;
using ConvertPDF.Views;
using ConvertPDF.Models;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using System.Diagnostics;
using System.IO.Compression;
using CompressionLevel = ConvertPDF.Services.CompressionLevel;
using Microsoft.Extensions.DependencyInjection;

namespace ConvertPDF.ViewModels
{
    public enum AppMode
    {
        Conversion,
        MergePdf,
        PdfTools,
        Transcription
    }

    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IConverterService _wordConverter;
        private readonly IConverterService _imageConverter;
        private readonly PdfMergeService _mergeService;
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        private readonly LanguagePackManager _languagePackManager;
        private readonly PdfCompressionService _compressionService;
        private readonly ProfileManager _profileManager;
        private readonly PdfPageManipulator _pdfTools;
        private FolderWatcherService _watcherService;
        private readonly PdfToWordConverter _pdfToWordConverter;
        private readonly PdfToExcelConverter _pdfToExcelConverter;
        private readonly PdfToHtmlConverter _pdfToHtmlConverter;
        private readonly ThemeService _themeService;
        private readonly SmartOcrDetector _smartOcrDetector;
        private readonly ConflictResolver _conflictResolver;
        private readonly ConversionHistoryService _historyService;
        private readonly IServiceProvider _serviceProvider; // Added

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private double _progressValue; // Added

        [ObservableProperty]
        private string _statusMessage = "Ready - Drop PDF files here"; // Modified

        [ObservableProperty]
        private bool _enableOcr = true;

        [ObservableProperty]
        private bool _turboMode = true;

        [ObservableProperty]
        private bool _mergePdfs = false;

        public ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new(); // Modified

        [ObservableProperty]
        private LanguageInfo _selectedLanguage;

        [ObservableProperty]
        private CompressionLevel _selectedCompression = CompressionLevel.None;

        public ObservableCollection<ConversionProfile> Profiles { get; } = new(); // Modified

        [ObservableProperty]
        private ConversionProfile _selectedProfile;

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _files = new(); // Modified

        [ObservableProperty]
        private bool _hasFiles;

        [ObservableProperty]
        private string _watchInputFolder = "";

        [ObservableProperty]
        private string _watchOutputFolder = "";

        [ObservableProperty]
        private bool _isWatching = false;

        [ObservableProperty]
        private string _watchStatus = "Inactif";

        [ObservableProperty]
        private bool _isSettingsOpen = false;

        // Mode Selection for Centralized Drop Zone
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsConversionMode))]
        [NotifyPropertyChangedFor(nameof(IsMergePdfMode))]
        [NotifyPropertyChangedFor(nameof(IsPdfToolsMode))]
        [NotifyPropertyChangedFor(nameof(IsTranscriptionMode))]
        private AppMode _selectedMode = AppMode.Conversion;

        [ObservableProperty]
        private string _dropZoneTitle = "Glissez vos fichiers ici";

        [ObservableProperty]
        private string _dropZoneSubtitle = "ou cliquez pour parcourir";

        [ObservableProperty]
        private string _actionButtonText = "CONVERTIR TOUT";

        // Mode computed properties for UI bindings
        public bool IsConversionMode => SelectedMode == AppMode.Conversion;
        public bool IsMergePdfMode => SelectedMode == AppMode.MergePdf;
        public bool IsPdfToolsMode => SelectedMode == AppMode.PdfTools;
        public bool IsTranscriptionMode => SelectedMode == AppMode.Transcription;

        // Snackbar message queue
        public SnackbarMessageQueue MessageQueue { get; } = new();


        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private readonly ShellRegistrationService _shellService;
        private readonly SystemHealthService _healthService;

        [ObservableProperty]
        private bool _isShellRegistered = false;

        [ObservableProperty]
        private bool _isFFmpegHealthy;

        [ObservableProperty]
        private bool _alwaysAskForOutputFolder = true;

        [ObservableProperty]
        private string _defaultOutputFolder = "";

        [ObservableProperty]
        private bool _isWhisperHealthy;

        [ObservableProperty]
        private bool _isGpuAvailable;

        partial void OnIsShellRegisteredChanged(bool value)
        {
            if (value)
                _shellService?.Register();
            else
                _shellService?.Unregister();
        }

        [RelayCommand]
        private async Task RunRepair()
        {
            StatusMessage = "Réparation en cours...";
            IsBusy = true;
            try
            {
                await _healthService.RunDiagnosticsAsync();
                if (!_healthService.IsFFmpegHealthy)
                {
                    await _healthService.RepairFFmpegAsync(new Progress<string>(s => StatusMessage = s));
                }
                StatusMessage = "Réparation terminée. " + (_healthService.IsFFmpegHealthy ? "FFmpeg OK" : "FFmpeg manquant");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Constructor RESTORED
        public MainWindowViewModel(
            ProfileManager profileManager,
            LanguagePackManager languagePackManager,
            PdfToWordConverter pdfToWordConverter,
            PdfToExcelConverter pdfToExcelConverter,
            PdfToHtmlConverter pdfToHtmlConverter,
            ThemeService themeService,
            SmartOcrDetector smartOcrDetector,
            ConflictResolver conflictResolver,
            ConversionHistoryService historyService,
            ShellRegistrationService shellRegistrationService,
            SystemHealthService systemHealthService,
            IServiceProvider serviceProvider)
        {
            _profileManager = profileManager;
            _languagePackManager = languagePackManager;
            _pdfToWordConverter = pdfToWordConverter;
            _pdfToExcelConverter = pdfToExcelConverter;
            _pdfToHtmlConverter = pdfToHtmlConverter;
            _themeService = themeService;
            _smartOcrDetector = smartOcrDetector;
            _conflictResolver = conflictResolver;
            _historyService = historyService;
            _shellService = shellRegistrationService;
            _healthService = systemHealthService;
            _serviceProvider = serviceProvider;

            // Initialize other services manually if they are not injected
            _settingsService = new SettingsService();
            _pdfTools = new PdfPageManipulator();
            _mergeService = new PdfMergeService();
            _compressionService = new PdfCompressionService();
            _wordConverter = new WordToPdfConverter(); 
            _imageConverter = new ImageToPdfConverter();

            // Initialize Shell Registration state
            IsShellRegistered = _shellService.IsRegistered;

            LoadSettings();
            LoadLanguages();
            LoadProfiles();
            _ = StartHealthMonitoring();
        }

        private async Task StartHealthMonitoring()
        {
            while (true)
            {
                await _healthService.RunDiagnosticsAsync();
                IsFFmpegHealthy = _healthService.IsFFmpegHealthy;
                IsWhisperHealthy = _healthService.IsWhisperHealthy;
                IsGpuAvailable = true; // Placeholder, assuming true for now as per dashboard intent
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
        }

        [RelayCommand]
        private void OpenStudio()
        {
            try
            {
                var studioWindow = _serviceProvider.GetService(typeof(ConvertPDF.Views.AudioVideoStudioWindow)) as Window;
                studioWindow?.Show();
            }
            catch (Exception ex)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Error opening Studio: {ex.Message}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"Inner: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                        sb.AppendLine($"Root: {ex.InnerException.InnerException.Message}");
                }
                
                // LOG TO FILE
                try {
                    var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ConvertPDF_startup.log");
                    System.IO.File.AppendAllText(logPath, $"\n[{DateTime.Now}] STUDIO OPEN ERROR:\n{sb}");
                } catch { /* ignore log error */ }

                MessageBox.Show(sb.ToString(), "Studio Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenSettings() => IsSettingsOpen = true;

        [RelayCommand]
        private void CloseSettings() => IsSettingsOpen = false;

        [ObservableProperty]
        private bool _isDarkMode = false;

        partial void OnIsDarkModeChanged(bool value)
        {
            _themeService.ApplyTheme(value);
            // Also persist to settings
            _currentSettings.IsDarkMode = value;
            _settingsService.SaveSettings(_currentSettings);
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
        }

        // Set Mode Command (for RadioButtons)
        [RelayCommand]
        private void SetMode(string modeIndex)
        {
            if (int.TryParse(modeIndex, out int index))
            {
                SelectedMode = (AppMode)index;
            }
        }


        // Mode Change Handler
        partial void OnSelectedModeChanged(AppMode value)
        {
            // Update drop zone text based on mode
            switch (value)
            {
                case AppMode.Conversion:
                    DropZoneTitle = "Glissez vos fichiers ici";
                    DropZoneSubtitle = "ou cliquez pour parcourir";
                    ActionButtonText = "OUVRIR LE CHEMIN D'ACCÈS";
                    break;
                case AppMode.MergePdf:
                    DropZoneTitle = "Glissez vos PDF à fusionner";
                    DropZoneSubtitle = "L'ordre des fichiers sera respecté";
                    ActionButtonText = "OUVRIR LE CHEMIN D'ACCÈS";
                    break;
                case AppMode.PdfTools:
                    DropZoneTitle = "Outils PDF Avancés";
                    DropZoneSubtitle = "Diviser, Extraire, Pivoter, Supprimer";
                    ActionButtonText = "OUVRIR LES OUTILS";
                    break;
                case AppMode.Transcription:
                    DropZoneTitle = "Transcription Audio/Vidéo";
                    DropZoneSubtitle = "Glissez un fichier média";
                    ActionButtonText = "OUVRIR LE STUDIO";
                    break;
            }
        }

        // Unified Action Dispatcher
        [RelayCommand]
        private async Task ExecuteAction()
        {
            switch (SelectedMode)
            {
                case AppMode.Conversion:
                    await ConvertAll();
                    break;
                case AppMode.MergePdf:
                    await MergePdfsNow();
                    break;
                case AppMode.PdfTools:
                    OpenPdfTools();
                    break;
                case AppMode.Transcription:
                    OpenStudio();
                    break;
            }
        }

        // New method for instant PDF merge
        private async Task MergePdfsNow()
        {
            if (!Files.Any())
            {
                StatusMessage = "Aucun fichier à fusionner";
                return;
            }

            var pdfFiles = Files.Where(f => Path.GetExtension(f.FullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
            if (pdfFiles.Count < 2)
            {
                StatusMessage = "Veuillez ajouter au moins 2 fichiers PDF";
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "merged.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = $"Fusion de {pdfFiles.Count} PDF...";

                    var filePaths = pdfFiles.Select(f => f.FullPath).ToList();
                    await _mergeService.MergePdfFiles(filePaths, saveDialog.FileName);

                    StatusMessage = "Fusion terminée !";
                    MessageQueue.Enqueue($"PDF fusionné : {Path.GetFileName(saveDialog.FileName)}");
                }
                catch (Exception ex)
                {
                    StatusMessage = "Erreur lors de la fusion";
                    MessageBox.Show($"Erreur : {ex.Message}", "Fusion PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }


        // Modified
        public static ObservableCollection<ConversionFormat> ConversionFormats { get; } = new()
        {
            ConversionFormat.PDF,
            ConversionFormat.Word,
            ConversionFormat.Excel,
            ConversionFormat.ExcelCSV,
            ConversionFormat.HTML,
            ConversionFormat.ImageJPEG,
            ConversionFormat.ImagePNG
        };
        public static IEnumerable<CompressionLevel> CompressionLevels => Enum.GetValues(typeof(CompressionLevel)).Cast<CompressionLevel>();

        [RelayCommand]
        private async Task ConvertAll()
        {
            if (IsBusy || !Files.Any()) return;

            IsBusy = true;
            StatusMessage = "Conversion en cours...";
            ProgressValue = 0;

            string outputDir = "";
            if (AlwaysAskForOutputFolder || string.IsNullOrEmpty(DefaultOutputFolder))
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Sélectionnez le dossier de destination pour les fichiers convertis",
                    UseDescriptionForTitle = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    outputDir = dialog.SelectedPath;
                    if (string.IsNullOrEmpty(DefaultOutputFolder))
                    {
                        DefaultOutputFolder = outputDir;
                        SaveSettings();
                    }
                }
                else
                {
                    IsBusy = false;
                    StatusMessage = "Opération annulée.";
                    return;
                }
            }
            else
            {
                outputDir = DefaultOutputFolder;
            }

            // Ensure output directory exists
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var filesToProcess = Files.Where(f => !f.IsCompleted).ToList();
            if (!filesToProcess.Any())
            {
                IsBusy = false;
                return;
            }

            int completedCount = 0;
            object lockObj = new object();

            // ... (Rest of the logic using outputDir)
            using var semaphore = new System.Threading.SemaphoreSlim(4);
            var tasks = filesToProcess.Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    file.IsConverting = true;
                    file.Status = "En attente...";
                    file.ProgressValue = 0;

                    var progress = new Progress<(int percent, string message)>(p => {
                        file.ProgressValue = p.percent;
                    });

                    string langCode = SelectedLanguage?.Code ?? "fra";
                    bool success = false;
                    string targetPath = Path.Combine(outputDir, Path.GetFileName(Path.ChangeExtension(file.FullPath, GetExtensionForFormat(file.TargetFormat))));

                    try
                    {
                        file.Status = "Conversion...";
                        switch (file.TargetFormat)
                        {
                            case ConversionFormat.PDF:
                                if (Path.GetExtension(file.FullPath).ToLower() == ".docx" || Path.GetExtension(file.FullPath).ToLower() == ".doc")
                                {
                                    await _wordConverter.ConvertAsync(file.FullPath, targetPath, EnableOcr, langCode, progress);
                                    success = true;
                                }
                                else if (new[] { ".jpg", ".png", ".jpeg" }.Contains(Path.GetExtension(file.FullPath).ToLower()))
                                {
                                    await _imageConverter.ConvertAsync(file.FullPath, targetPath, EnableOcr, langCode, progress);
                                    success = true;
                                }
                                break;
                            case ConversionFormat.Word:
                                if (Path.GetExtension(file.FullPath).ToLower() == ".pdf")
                                {
                                    var result = await _pdfToWordConverter.ConvertAsync(file.FullPath, targetPath, langCode, TurboMode, progress);
                                    success = File.Exists(result);
                                }
                                break;
                            default:
                                file.ErrorMessage = "Format non supporté";
                                break;
                        }


                        if (success)
                        {
                            file.Status = "Terminé";
                            file.IsCompleted = true;
                            file.ProgressValue = 100;
                        }
                        else
                        {
                            file.Status = "Erreur";
                            file.HasError = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        file.ErrorMessage = ex.Message;
                        file.HasError = true;
                        file.Status = "Échec";
                    }
                }
                finally
                {
                    file.IsConverting = false;
                    lock (lockObj)
                    {
                        completedCount++;
                        ProgressValue = (double)completedCount / filesToProcess.Count * 100;
                    }
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Handle PDF Merging if enabled
            if (MergePdfs)
            {
                var generatedPdfs = filesToProcess
                    .Where(f => f.IsCompleted && Path.GetExtension(f.OutputPath).ToLower() == ".pdf")
                    .Select(f => f.OutputPath)
                    .ToList();

                if (generatedPdfs.Count > 1)
                {
                    StatusMessage = "Fusion des PDF en cours...";
                    try
                    {
                        string firstFileDir = Path.GetDirectoryName(generatedPdfs[0]);
                        string mergedPath = Path.Combine(firstFileDir, $"Merged_Result_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                        await _mergeService.MergePdfFiles(generatedPdfs, mergedPath);
                        StatusMessage = $"Fusion terminée : {Path.GetFileName(mergedPath)}";
                        
                        // Notify user via Snackbar or MessageBox
                        Application.Current.Dispatcher.Invoke(() => 
                            MessageBox.Show($"Fusion terminée !\nFichier : {mergedPath}", "Succès", MessageBoxButton.OK, MessageBoxImage.Information));
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Erreur fusion : {ex.Message}";
                    }
                }
            }
            else
            {
                StatusMessage = "Toutes les tâches sont terminées.";
            }

            IsBusy = false;
        }




        [RelayCommand]
        private async Task PickFiles()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "All Supported Files|*.pdf;*.docx;*.doc;*.png;*.jpg;*.jpeg;*.bmp|PDF Files|*.pdf|Word Documents|*.docx;*.doc|Images|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await OnFilesDropped(openFileDialog.FileNames);
            }
        }





        [RelayCommand]
        private void SaveCopy(FileItemViewModel file)
        {
             // Stub
        }

        [RelayCommand]
        private void OpenFile(FileItemViewModel file)
        {
             if (File.Exists(file.OutputPath))
             {
                 Process.Start(new ProcessStartInfo(file.OutputPath) { UseShellExecute = true });
             }
        }

        private void LoadSettings()
        {
            _currentSettings = _settingsService.LoadSettings();
            
            // Apply Settings
            EnableOcr = _currentSettings.EnableOcr;
            TurboMode = _currentSettings.TurboMode;
            
            // Apply Compression
            if (Enum.TryParse<CompressionLevel>(_currentSettings.DefaultCompressionLevel, out var compressionLevel))
                SelectedCompression = compressionLevel;
            else
                SelectedCompression = CompressionLevel.None;
            
            // Load Watch Folder settings
            WatchInputFolder = _currentSettings.WatchInputPath ?? "";
            WatchOutputFolder = _currentSettings.WatchOutputPath ?? "";

            // Load Output Folder settings
            DefaultOutputFolder = _currentSettings.DefaultOutputFolder ?? "";
            AlwaysAskForOutputFolder = _currentSettings.AlwaysAskForOutputFolder;
            
            // Apply Theme via unified ThemeService
            IsDarkMode = _currentSettings.IsDarkMode;
            _themeService.ApplyTheme(IsDarkMode);
        }

        private void LoadLanguages()
        {
            var langs = _languagePackManager?.GetAllLanguages() ?? new List<LanguageInfo>
            {
                new LanguageInfo { Code = "eng", DisplayName = "English" },
                new LanguageInfo { Code = "fra", DisplayName = "Français" },
                new LanguageInfo { Code = "deu", DisplayName = "Deutsch" },
                new LanguageInfo { Code = "spa", DisplayName = "Español" },
                new LanguageInfo { Code = "ita", DisplayName = "Italiano" }
            };

            AvailableLanguages.Clear();
            foreach (var lang in langs)
            {
                AvailableLanguages.Add(lang);
            }

            // Default to French if not set or if set to English (legacy)
            string savedLang = _currentSettings?.PreferredOcrLanguage;
            if (string.IsNullOrEmpty(savedLang) || savedLang == "eng")
            {
                savedLang = "fra"; // Default to French
            }
            
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == savedLang) 
                            ?? AvailableLanguages.FirstOrDefault(l => l.Code == "fra")
                            ?? AvailableLanguages.FirstOrDefault();
        }

        private void LoadProfiles()
        {
            var loadedProfiles = _profileManager?.LoadProfiles() ?? new List<ConversionProfile>();
            Profiles.Clear();
            foreach (var p in loadedProfiles)
            {
                Profiles.Add(p);
            }
            SelectedProfile = Profiles.FirstOrDefault();
        }



        partial void OnSelectedProfileChanged(ConversionProfile value)
        {
            if (value != null && value.Name != "Default")
            {
                ApplyProfile(value);
            }
        }

        private void ApplyProfile(ConversionProfile profile)
        {
            EnableOcr = profile.EnableOcr;
            
            var language = AvailableLanguages.FirstOrDefault(l => l.Code == profile.OcrLanguage);
            if (language != null)
                SelectedLanguage = language;
            
            if (Enum.TryParse<CompressionLevel>(profile.CompressionLevel, out var compression))
                SelectedCompression = compression;
            
            if (!string.IsNullOrEmpty(profile.DefaultOutputFolder) && Directory.Exists(profile.DefaultOutputFolder))
                _currentSettings.LastOutputFolder = profile.DefaultOutputFolder;
        }

        [RelayCommand]
        private void SaveCurrentAsProfile()
        {
            // This will be called from UI dialog with profile name input
            var newProfile = new ConversionProfile
            {
                Name = "Custom_" + DateTime.Now.Ticks, // Placeholder, should be user-provided
                EnableOcr = EnableOcr,
                OcrLanguage = SelectedLanguage?.Code ?? "eng",
                CompressionLevel = SelectedCompression.ToString(),
                DefaultOutputFolder = _currentSettings?.LastOutputFolder ?? ""
            };

            _profileManager.SaveProfile(newProfile);
            LoadProfiles();
        }

        [RelayCommand]
        private void DeleteProfile(ConversionProfile profile)
        {
            if (profile == null || profile.Name == "Default") return;

            _profileManager.DeleteProfile(profile.Name);
            LoadProfiles();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Name == "Default");
        }

        partial void OnSelectedLanguageChanged(LanguageInfo value)
        {
            if (value != null && !value.IsInstalled)
            {
                // Trigger download
                _ = DownloadLanguagePack(value.Code);
            }
            else
            {
                SaveSettings();
            }
        }

        private async Task DownloadLanguagePack(string langCode)
        {
            var language = AvailableLanguages.FirstOrDefault(l => l.Code == langCode);
            if (language == null) return;

            string oldStatus = StatusMessage;
            IsBusy = true;
            StatusMessage = $"Downloading {language.DisplayName}...";

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    StatusMessage = $"Downloading {language.DisplayName}... {percent}%";
                });

                bool success = await _languagePackManager.DownloadLanguagePack(langCode, progress);
                
                if (success)
                {
                    language.IsInstalled = true;
                    StatusMessage = $"{language.DisplayName} downloaded successfully!";
                    SaveSettings();
                }
                else
                {
                    StatusMessage = $"Failed to download {language.DisplayName}";
                    // Revert selection
                    SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.IsInstalled);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error downloading language: {ex.Message}";
                SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.IsInstalled);
            }
            finally
            {
                IsBusy = false;
                await Task.Delay(2000);
                StatusMessage = oldStatus;
            }
        }

        private async void Initialize()
        {
            await CheckAndDownloadTessData();
        }

        private async Task CheckAndDownloadTessData()
        {
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            string engData = Path.Combine(tessDataPath, "eng.traineddata");
            
            if (!Directory.Exists(tessDataPath)) Directory.CreateDirectory(tessDataPath);

            if (!File.Exists(engData))
            {
                StatusMessage = "Downloading OCR data...";
                IsBusy = true;
                try
                {
                    using (var client = new HttpClient())
                    {
                        var bytes = await client.GetByteArrayAsync("https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata");
                        await File.WriteAllBytesAsync(engData, bytes);
                    }
                    StatusMessage = "Ready. Drop Word files.";
                }
                catch (Exception ex)
                {
                    StatusMessage = "Failed to download OCR data: " + ex.Message;
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        partial void OnEnableOcrChanged(bool value)
        {
            SaveSettings();
        }

        [RelayCommand]
        private void ClearList()
        {
            Files.Clear();
            HasFiles = false;
            StatusMessage = "Drop Word files or Images here";
        }

        public async Task OnFilesDropped(string[] filePaths)
        {
            if (IsBusy) return;

            // Filter for Word files, Images, and PDFs
            var validFiles = new System.Collections.Generic.List<string>();
            foreach (var file in filePaths)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".docx" || ext == ".doc" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".pdf") 
                    validFiles.Add(file);
            }

            if (validFiles.Count == 0)
            {
                StatusMessage = "No supported files found (Word/Images/PDFs).";
                return;
            }

            // Just add to list
            foreach (var file in validFiles)
            {
                Files.Add(new FileItemViewModel(file));
            }
            HasFiles = true;
            StatusMessage = $"{Files.Count} files pending. Drag to reorder, then convert.";
        }

        [RelayCommand]
        private async Task ConvertToPdf()
        {
            if (Files.Count == 0 || IsBusy) return;

            string initialDir = !string.IsNullOrEmpty(_currentSettings?.LastOutputFolder) ? _currentSettings.LastOutputFolder : "";

            // Ask for Output Folder
            var folderDialog = new Microsoft.Win32.OpenFolderDialog();
            folderDialog.Title = "Select Output Folder for PDFs";
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)) folderDialog.InitialDirectory = initialDir;

            if (folderDialog.ShowDialog() != true) return;

            string outputFolder = folderDialog.FolderName;

            // Save preference
            _currentSettings.LastOutputFolder = outputFolder;
            SaveSettings();
            
            await ProcessBatchConversion(outputFolder, isMerge: false);
        }

        [RelayCommand]
        private async Task ConvertToWord()
        {
            // Filter PDF files only
            var pdfFiles = Files.Where(f => f.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (pdfFiles.Count == 0)
            {
                MessageBox.Show("No PDF files to convert. Please add PDF files to the list.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (IsBusy) return;

            string initialDir = !string.IsNullOrEmpty(_currentSettings?.LastOutputFolder) ? _currentSettings.LastOutputFolder : "";

            // Ask for Output Folder
            var folderDialog = new Microsoft.Win32.OpenFolderDialog();
            folderDialog.Title = "Select Output Folder for Word Documents";
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)) folderDialog.InitialDirectory = initialDir;

            if (folderDialog.ShowDialog() != true) return;

            string outputFolder = folderDialog.FolderName;

            // Save preference
            _currentSettings.LastOutputFolder = outputFolder;
            SaveSettings();

            IsBusy = true;
            StatusMessage = "Converting PDF to Word...";
            
            try
            {
                string langCode = SelectedLanguage?.Code ?? "eng";

                foreach (var fileVm in pdfFiles)
                {
                    try 
                    {
                        fileVm.State = ConversionState.Processing;
                        fileVm.Status = "Converting PDF...";
                        fileVm.IsConverting = true;
                        fileVm.HasError = false;

                        string fileName = Path.GetFileNameWithoutExtension(fileVm.FullPath);
                        string outputPath = Path.Combine(outputFolder, fileName + ".docx");

                        var progress = new Progress<(int percent, string message)>(report => 
                        {
                            fileVm.ProgressValue = report.percent;
                            fileVm.Status = report.message;
                        });

                        string method = await _pdfToWordConverter.ConvertAsync(fileVm.FullPath, outputPath, langCode, TurboMode, progress);

                        fileVm.Status = $"Done";
                        fileVm.ProgressValue = 100;
                        fileVm.State = ConversionState.Completed;
                        fileVm.IsCompleted = true;
                        fileVm.IsConverting = false;
                        fileVm.OutputPath = outputPath;
                    }
                    catch (Exception ex)
                    {
                        fileVm.State = ConversionState.Failed;
                        fileVm.HasError = true;
                        fileVm.IsConverting = false;

                        if (ex is System.IO.FileNotFoundException) {
                            fileVm.Status = "Fichier introuvable";
                            fileVm.ErrorMessage = "Le système ne trouve pas le fichier spécifié.";
                            fileVm.ActionRequired = "Vérifiez que le fichier existe et n'a pas été déplacé.";
                        }
                        else if (ex is System.UnauthorizedAccessException) {
                            fileVm.Status = "Accès refusé";
                            fileVm.ErrorMessage = "Impossible d'accéder au fichier.";
                            fileVm.ActionRequired = "Fermez le fichier s'il est ouvert dans un autre programme.";
                        }
                        else if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("encrypted", StringComparison.OrdinalIgnoreCase) >= 0) {
                            fileVm.Status = "Protégé par mot de passe";
                            fileVm.ErrorMessage = "Le fichier PDF est chiffré.";
                            fileVm.ActionRequired = "Veuillez retirer le mot de passe avant la conversion.";
                        }
                        else if (ex is OutOfMemoryException) {
                            fileVm.Status = "Fichier trop lourd";
                            fileVm.ErrorMessage = "Mémoire insuffisante pour traiter ce fichier.";
                            fileVm.ActionRequired = "Essayez de réduire la taille du fichier ou de convertir page par page.";
                        }
                        else {
                            fileVm.Status = $"Erreur: {ex.Message}";
                            fileVm.ErrorMessage = ex.Message;
                            fileVm.ActionRequired = "Réessayez ou contactez le support.";
                        }

                        System.Diagnostics.Debug.WriteLine($"File error: {ex.Message}");
                    }
                }

                StatusMessage = $"Completed! Processed {pdfFiles.Count} PDF(s).";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ConvertToExcel()
        {
            var pdfFiles = Files.Where(f => f.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
            if (pdfFiles.Count == 0)
            {
                MessageBox.Show("No PDF files to convert. Please add PDF files to the list.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (IsBusy) return;

            string initialDir = !string.IsNullOrEmpty(_currentSettings?.LastOutputFolder) ? _currentSettings.LastOutputFolder : "";
            var folderDialog = new Microsoft.Win32.OpenFolderDialog();
            folderDialog.Title = "Select Output Folder for Excel Files";
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)) folderDialog.InitialDirectory = initialDir;
            if (folderDialog.ShowDialog() != true) return;

            string outputFolder = folderDialog.FolderName;
            _currentSettings.LastOutputFolder = outputFolder;
            SaveSettings();

            IsBusy = true;
            StatusMessage = "Converting PDF to Excel...";
            
            try
            {
                foreach (var fileVm in pdfFiles)
                {
                    try 
                    {
                        fileVm.State = ConversionState.Processing;
                        fileVm.Status = "Converting to Excel...";
                        fileVm.IsConverting = true;
                        fileVm.HasError = false;

                        string fileName = Path.GetFileNameWithoutExtension(fileVm.FullPath);
                        string extension = fileVm.TargetFormat == ConversionFormat.ExcelCSV ? ".csv" : ".xlsx";
                        string outputPath = Path.Combine(outputFolder, fileName + extension);

                        var progress = new Progress<(int percent, string message)>(report => 
                        {
                            fileVm.ProgressValue = report.percent;
                            fileVm.Status = report.message;
                        });

                        await _pdfToExcelConverter.ConvertAsync(fileVm.FullPath, outputPath, true, progress);

                        fileVm.Status = "Done";
                        fileVm.ProgressValue = 100;
                        fileVm.State = ConversionState.Completed;
                        fileVm.IsCompleted = true;
                        fileVm.IsConverting = false;
                        fileVm.OutputPath = outputPath;
                    }
                    catch (Exception ex)
                    {
                        fileVm.State = ConversionState.Failed;
                        fileVm.HasError = true;
                        fileVm.IsConverting = false;
                        fileVm.Status = $"Error: {ex.Message}";
                        fileVm.ErrorMessage = ex.Message;
                        fileVm.ActionRequired = "Vérifiez que le PDF contient des tableaux.";
                    }
                }
                StatusMessage = $"Completed! Processed {pdfFiles.Count} PDF(s) to Excel.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ConvertToHtml()
        {
            var pdfFiles = Files.Where(f => f.FullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
            if (pdfFiles.Count == 0)
            {
                MessageBox.Show("No PDF files to convert. Please add PDF files to the list.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (IsBusy) return;

            string initialDir = !string.IsNullOrEmpty(_currentSettings?.LastOutputFolder) ? _currentSettings.LastOutputFolder : "";
            var folderDialog = new Microsoft.Win32.OpenFolderDialog();
            folderDialog.Title = "Select Output Folder for HTML Files";
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)) folderDialog.InitialDirectory = initialDir;
            if (folderDialog.ShowDialog() != true) return;

            string outputFolder = folderDialog.FolderName;
            _currentSettings.LastOutputFolder = outputFolder;
            SaveSettings();

            IsBusy = true;
            StatusMessage = "Converting PDF to HTML...";
            
            try
            {
                foreach (var fileVm in pdfFiles)
                {
                    try 
                    {
                        fileVm.State = ConversionState.Processing;
                        fileVm.Status = "Converting to HTML...";
                        fileVm.IsConverting = true;
                        fileVm.HasError = false;

                        string fileName = Path.GetFileNameWithoutExtension(fileVm.FullPath);
                        string outputPath = Path.Combine(outputFolder, fileName + ".html");

                        var progress = new Progress<(int percent, string message)>(report => 
                        {
                            fileVm.ProgressValue = report.percent;
                            fileVm.Status = report.message;
                        });

                        await _pdfToHtmlConverter.ConvertAsync(fileVm.FullPath, outputPath, flowingMode: false, embedImages: false, progress);

                        fileVm.Status = "Done";
                        fileVm.ProgressValue = 100;
                        fileVm.State = ConversionState.Completed;
                        fileVm.IsCompleted = true;
                        fileVm.IsConverting = false;
                        fileVm.OutputPath = outputPath;
                    }
                    catch (Exception ex)
                    {
                        fileVm.State = ConversionState.Failed;
                        fileVm.HasError = true;
                        fileVm.IsConverting = false;
                        fileVm.Status = $"Error: {ex.Message}";
                        fileVm.ErrorMessage = ex.Message;
                        fileVm.ActionRequired = "Réessayez ou contactez le support.";
                    }
                }
                StatusMessage = $"Completed! Processed {pdfFiles.Count} PDF(s) to HTML.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task MergeAll()
        {
            if (Files.Count == 0 || IsBusy) return;

            string initialDir = !string.IsNullOrEmpty(_currentSettings?.LastOutputFolder) ? _currentSettings.LastOutputFolder : "";

            // Ask for Output File
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "PDF File (*.pdf)|*.pdf";
            
            // Generate smart default name
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string firstFileName = Files.Count > 0 ? Path.GetFileNameWithoutExtension(Files[0].FileName) : "Document";
            saveDialog.FileName = $"Merged_{firstFileName}_{timestamp}.pdf";
            
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)) saveDialog.InitialDirectory = initialDir;

            if (saveDialog.ShowDialog() != true) return;

            string finalOutputPath = saveDialog.FileName;

            // Save preference (folder of the file)
            _currentSettings.LastOutputFolder = Path.GetDirectoryName(finalOutputPath);
            SaveSettings();

            await ProcessBatchConversion(null, isMerge: true, mergeOutputPath: finalOutputPath);
        }

        [RelayCommand]
        private async Task ExportToZip()
        {
            var completedFiles = Files.Where(f => f.IsCompleted && !string.IsNullOrEmpty(f.OutputPath) && File.Exists(f.OutputPath)).ToList();
            if (completedFiles.Count == 0)
            {
                MessageBox.Show("Aucun fichier converti à exporter.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ZIP Archive (*.zip)|*.zip",
                FileName = $"Conversion_Result_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Création du fichier ZIP...";
                    await Task.Run(() =>
                    {
                        using (var archive = System.IO.Compression.ZipFile.Open(saveDialog.FileName, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            foreach (var file in completedFiles)
                            {
                                archive.CreateEntryFromFile(file.OutputPath, Path.GetFileName(file.OutputPath));
                            }
                        }
                    });
                    StatusMessage = "Export ZIP terminé !";
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{saveDialog.FileName}\"") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de la création du ZIP: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ProcessBatchConversion(string outputFolder, bool isMerge, string mergeOutputPath = null)
        {
            IsBusy = true;
            StatusMessage = isMerge ? "Converting for Merge..." : "Converting...";

            // Track successful paths for merging
            var tempPdfFiles = new System.Collections.Generic.List<string>();

            try
            {
                _wordConverter.InitializeBatch();
                
                foreach (var fileItem in Files)
                {
                    fileItem.Status = "Converting...";
                    fileItem.State = ConversionState.Processing;
                    fileItem.IsConverting = true;
                    fileItem.HasError = false;

                    try
                    {
                        string targetPath;
                        bool isTemp = false;

                        if (isMerge)
                        {
                            // Create a temp file for this item
                            targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
                            isTemp = true;
                        }
                        else
                        {
                            targetPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(fileItem.FullPath) + ".pdf");
                        }

                        string ext = Path.GetExtension(fileItem.FullPath).ToLower();
                        IConverterService converter = null;
                         
                        if (ext == ".docx" || ext == ".doc") converter = _wordConverter;
                        else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp") converter = _imageConverter;

                        if (converter != null)
                        {
                             string langCode = SelectedLanguage?.Code ?? "eng";
                             await converter.ConvertAsync(fileItem.FullPath, targetPath, EnableOcr, langCode);
                             
                             // Apply compression if needed
                             if (SelectedCompression != CompressionLevel.None)
                             {
                                 string compressedPath = targetPath + ".tmp.pdf";
                                 _compressionService.CompressPdf(targetPath, compressedPath, SelectedCompression);
                                 File.Delete(targetPath);
                                 File.Move(compressedPath, targetPath);
                             }
                             
                             fileItem.Status = "Done";
                             fileItem.State = ConversionState.Completed;
                             fileItem.IsCompleted = true;
                             fileItem.OutputPath = targetPath; // Store output path
                             if (isTemp) tempPdfFiles.Add(targetPath);
                        }
                        else
                        {
                             fileItem.Status = "Skipped";
                             fileItem.State = ConversionState.Failed;
                             fileItem.HasError = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        fileItem.State = ConversionState.Failed;
                        fileItem.HasError = true;
                        
                        if (ex is System.IO.FileNotFoundException) {
                            fileItem.Status = "Fichier introuvable";
                            fileItem.ErrorMessage = "Le système ne trouve pas le fichier spécifié.";
                            fileItem.ActionRequired = "Vérifiez que le fichier existe et n'a pas été déplacé.";
                        }
                        else if (ex is System.UnauthorizedAccessException) {
                            fileItem.Status = "Accès refusé";
                            fileItem.ErrorMessage = "Impossible d'accéder au fichier.";
                            fileItem.ActionRequired = "Fermez le fichier s'il est ouvert dans un autre programme.";
                        }
                        else if (ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("encrypted", StringComparison.OrdinalIgnoreCase) >= 0) {
                            fileItem.Status = "Protégé par mot de passe";
                            fileItem.ErrorMessage = "Le fichier PDF est chiffré.";
                            fileItem.ActionRequired = "Veuillez retirer le mot de passe avant la conversion.";
                        }
                        else if (ex is OutOfMemoryException) {
                            fileItem.Status = "Fichier trop lourd";
                            fileItem.ErrorMessage = "Mémoire insuffisante pour traiter ce fichier.";
                            fileItem.ActionRequired = "Essayez de réduire la taille du fichier ou de convertir page par page.";
                        }
                        else {
                            fileItem.Status = $"Erreur: {ex.Message}"; // Show specific error
                            fileItem.ErrorMessage = ex.Message;
                            fileItem.ActionRequired = "Réessayez ou contactez le support.";
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Error converting {fileItem.FileName}: {ex}");
                    }
                    finally
                    {
                        fileItem.IsConverting = false;
                    }
                }

                // If Merge Mode, perform the merge
                if (isMerge && tempPdfFiles.Count > 0)
                {
                    StatusMessage = "Merging...";
                    await Task.Run(() => _mergeService.MergePdfFiles(tempPdfFiles, mergeOutputPath));
                    
                    // Cleanup temps
                    foreach (var temp in tempPdfFiles)
                    {
                        try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                    }
                    StatusMessage = "Merge complete!";
                }
                else
                {
                     StatusMessage = "Conversion complete.";
                }
            }
            finally
            {
                _wordConverter.DisposeBatch();
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void RemoveFile(FileItemViewModel file)
        {
            if (file != null && Files.Contains(file) && !IsBusy)
            {
                Files.Remove(file);
                if (Files.Count == 0) HasFiles = false;
                StatusMessage = $"{Files.Count} files pending.";
            }
        }

        [RelayCommand]
        private void BrowseWatchInputFolder()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder",
                Title = "Select Input Folder to Watch"
            };

            if (dialog.ShowDialog() == true)
            {
                WatchInputFolder = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        [RelayCommand]
        private void BrowseWatchOutputFolder()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                CheckFileExists = false,
                FileName = "Select Folder",
                Title = "Select Output Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                WatchOutputFolder = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        [RelayCommand]
        private void ToggleWatcher()
        {
            if (IsWatching)
            {
                StopWatcher();
            }
            else
            {
                StartWatcher();
            }
        }

        private void StartWatcher()
        {
            if (string.IsNullOrEmpty(WatchInputFolder) || string.IsNullOrEmpty(WatchOutputFolder))
            {
                MessageBox.Show("Please select both input and output folders", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _watcherService = new FolderWatcherService();
                _watcherService.OnStatusChanged += (msg) => Application.Current.Dispatcher.Invoke(() => WatchStatus = msg);
                _watcherService.OnFileProcessed += (msg) => Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
                _watcherService.OnError += (msg) => Application.Current.Dispatcher.Invoke(() => StatusMessage = "❌ " + msg);

                string langCode = SelectedLanguage?.Code ?? "eng";
                _watcherService.Start(WatchInputFolder, WatchOutputFolder, EnableOcr, langCode);
                
                IsWatching = true;
                SaveSettings(); // Updated call
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start watcher: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopWatcher()
        {
            _watcherService?.Stop();
            _watcherService?.Dispose();
            _watcherService = null;
            IsWatching = false;
            WatchStatus = "Stopped";
            SaveSettings(); // Updated call
        }

        private void SaveSettings()
        {
            if (_currentSettings == null) _currentSettings = new AppSettings();
            _currentSettings.IsDarkMode = IsDarkMode;
            _currentSettings.EnableOcr = EnableOcr;
            _currentSettings.TurboMode = TurboMode;
            _currentSettings.DefaultCompressionLevel = SelectedCompression.ToString();
            _currentSettings.WatchFolderEnabled = IsWatching;
            _currentSettings.WatchInputPath = WatchInputFolder;
            _currentSettings.WatchOutputPath = WatchOutputFolder;
            _currentSettings.DefaultOutputFolder = DefaultOutputFolder;
            _currentSettings.AlwaysAskForOutputFolder = AlwaysAskForOutputFolder;
            _currentSettings.PreferredOcrLanguage = SelectedLanguage?.Code ?? "fra";

            _settingsService.SaveSettings(_currentSettings);
        }


        [RelayCommand]
        private void BrowseDefaultOutputFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Sélectionnez le dossier de sortie par défaut",
                SelectedPath = DefaultOutputFolder,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DefaultOutputFolder = dialog.SelectedPath;
                SaveSettings();
            }
        }

        private string GetExtensionForFormat(ConversionFormat format)
        {
            return format switch
            {
                ConversionFormat.PDF => ".pdf",
                ConversionFormat.Word => ".docx",
                ConversionFormat.Excel => ".xlsx",
                ConversionFormat.HTML => ".html",
                ConversionFormat.ImagePNG => ".png",
                ConversionFormat.ImageJPEG => ".jpg",
                ConversionFormat.ExcelCSV => ".csv",
                _ => ".pdf"
            };
        }


        [RelayCommand]
        private void OpenPdfTools()
        {
            var vm = _serviceProvider.GetRequiredService<PdfToolsViewModel>();
            var toolsWindow = new PdfToolsWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = vm
            };
            toolsWindow.ShowDialog();
        }
    }
}
