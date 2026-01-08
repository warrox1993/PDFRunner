using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConvertPDF.Models
{
    public class TranscriptionSegment
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = string.Empty;
        public float Probability { get; set; }
    }

    public class TranscriptionResult
    {
        public string FullText { get; set; } = string.Empty;
        public List<TranscriptionSegment> Segments { get; set; } = new List<TranscriptionSegment>();
        public TimeSpan Duration { get; set; }
        public string Language { get; set; } = "Auto";
    }

    public enum WhisperModelType
    {
        Tiny,
        Base,
        Small,
        Medium,
        LargeV3
    }

    public class WhisperModelInfo : INotifyPropertyChanged
    {
        private bool _isDownloaded;
        private bool _isDownloading;
        private double _downloadProgress;

        public WhisperModelType Type { get; set; }
        public string Name => Type.ToString();
        public string SizeDisplay { get; set; }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { _isDownloaded = value; OnPropertyChanged(); }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
