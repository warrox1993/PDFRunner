using ConvertPDF.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConvertPDF.Services
{
    public class FolderWatcherService : IDisposable
    {
        private FileSystemWatcher _watcher;
        private readonly ConcurrentQueue<string> _fileQueue = new ConcurrentQueue<string>();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;
        
        private string _inputFolder;
        private string _outputFolder;
        private bool _enableOcr;
        private string _languageCode;
        
        private readonly IConverterService _wordConverter;
        private readonly IConverterService _imageConverter;
        
        public event Action<string>? OnFileProcessed;
        public event Action<string>? OnError;
        public event Action<string>? OnStatusChanged;
        
        public bool IsWatching { get; private set; }

        public FolderWatcherService()
        {
            _wordConverter = new WordToPdfConverter();
            _imageConverter = new ImageToPdfConverter();
        }

        public void Start(string inputFolder, string outputFolder, bool enableOcr, string languageCode = "eng")
        {
            if (IsWatching) return;
            
            if (!Directory.Exists(inputFolder))
            {
                throw new ArgumentException("Input folder does not exist");
            }
            
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            _inputFolder = inputFolder;
            _outputFolder = outputFolder;
            _enableOcr = enableOcr;
            _languageCode = languageCode;

            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize FileSystemWatcher
            _watcher = new FileSystemWatcher(_inputFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileCreated;
            _watcher.Error += OnWatcherError;

            // Start background processing
            _processingTask = Task.Run(() => ProcessQueue(_cancellationTokenSource.Token));

            IsWatching = true;
            OnStatusChanged?.Invoke($"Watching: {_inputFolder}");
        }

        public void Stop()
        {
            if (!IsWatching) return;

            IsWatching = false;
            
            _watcher?.Dispose();
            _watcher = null;

            _cancellationTokenSource?.Cancel();
            _processingTask?.Wait(5000);
            
            OnStatusChanged?.Invoke("Stopped");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Filter for supported files
            string ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext == ".docx" || ext == ".doc" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
            {
                // Wait a bit to ensure file is fully written
                Thread.Sleep(500);
                _fileQueue.Enqueue(e.FullPath);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            OnError?.Invoke($"Watcher error: {e.GetException()?.Message}");
        }

        private async Task ProcessQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_fileQueue.TryDequeue(out string filePath))
                {
                    await ProcessFile(filePath);
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task ProcessFile(string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                    return;

                string fileName = Path.GetFileNameWithoutExtension(inputPath);
                string outputPath = Path.Combine(_outputFolder, fileName + ".pdf");

                OnStatusChanged?.Invoke($"Processing: {Path.GetFileName(inputPath)}");

                IConverterService converter = GetConverter(inputPath);
                if (converter != null)
                {
                    await converter.ConvertAsync(inputPath, outputPath, _enableOcr, _languageCode);
                    OnFileProcessed?.Invoke($"✓ {Path.GetFileName(inputPath)} → {Path.GetFileName(outputPath)}");
                    
                    // Delete source file after successful conversion (optional)
                    // File.Delete(inputPath);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error processing {Path.GetFileName(inputPath)}: {ex.Message}");
            }
        }

        private IConverterService GetConverter(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            
            if (ext == ".docx" || ext == ".doc")
                return _wordConverter;
            
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                return _imageConverter;
            
            return null;
        }

        public void Dispose()
        {
            Stop();
            _wordConverter?.Dispose();
            _imageConverter?.Dispose();
        }
    }
}
