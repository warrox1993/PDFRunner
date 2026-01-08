using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;

namespace ConvertPDF.ViewModels
{
    using ConvertPDF.Models;

    public enum ConversionState
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    public partial class FileItemViewModel : ObservableObject
    {
        public string FullPath { get; }
        public string FileName => Path.GetFileName(FullPath);

        [ObservableProperty]
        private ConversionFormat _targetFormat = ConversionFormat.PDF;

        [ObservableProperty]
        private string _status;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private ConversionState _state;

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _outputPath;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string? _actionRequired;

        public FileItemViewModel(string path)
        {
            FullPath = path;
            Status = "Pending";
            State = ConversionState.Pending;
            ProgressValue = 0;
        }

        public string StatusIcon
        {
            get
            {
                return State switch
                {
                    ConversionState.Pending => "ClockOutline",
                    ConversionState.Processing => "Cog", // Or ProgressClock
                    ConversionState.Completed => "CheckCircle",
                    ConversionState.Failed => "AlertCircle",
                    ConversionState.Cancelled => "Cancel",
                    _ => "HelpCircle"
                };
            }
        }

        public string StatusColor
        {
            get
            {
                return State switch
                {
                    ConversionState.Pending => "#757575", // Gray
                    ConversionState.Processing => "#2196F3", // Blue
                    ConversionState.Completed => "#4CAF50", // Green
                    ConversionState.Failed => "#F44336", // Red
                    ConversionState.Cancelled => "#FF9800", // Orange
                    _ => "#000000"
                };
            }
        }

        [RelayCommand]
        private void OpenFile()
        {
            if (!string.IsNullOrEmpty(OutputPath) && File.Exists(OutputPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true });
                }
                catch { }
            }
        }

        [RelayCommand]
        private void SaveCopy()
        {
            if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath)) return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = Path.GetFileName(OutputPath),
                Filter = "All Files|*.*"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(OutputPath, saveDialog.FileName, true);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void PreviewFile()
        {
            if (!string.IsNullOrEmpty(FullPath) && File.Exists(FullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(FullPath) { UseShellExecute = true });
                }
                catch { }
            }
        }
    }
}
