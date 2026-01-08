using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Model for a single history entry.
    /// </summary>
    public class ConversionHistoryEntry
    {
        public string SourcePath { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public string Format { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Manages persistent conversion history using a JSON file.
    /// Allows users to review past conversions.
    /// </summary>
    public class ConversionHistoryService
    {
        private readonly string _historyPath;
        private List<ConversionHistoryEntry> _entries = new();

        public IReadOnlyList<ConversionHistoryEntry> Entries => _entries.AsReadOnly();

        public ConversionHistoryService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "ConvertPDF");
            Directory.CreateDirectory(folder);
            _historyPath = Path.Combine(folder, "history.json");

            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    string json = File.ReadAllText(_historyPath);
                    _entries = JsonSerializer.Deserialize<List<ConversionHistoryEntry>>(json) ?? new();
                }
            }
            catch { _entries = new(); }
        }

        private void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyPath, json);
            }
            catch { }
        }

        public void AddEntry(string sourcePath, string outputPath, string format, bool success)
        {
            _entries.Insert(0, new ConversionHistoryEntry
            {
                SourcePath = sourcePath,
                OutputPath = outputPath,
                Format = format,
                Timestamp = DateTime.Now,
                Success = success
            });

            // Keep only the last 100 entries
            if (_entries.Count > 100)
                _entries.RemoveRange(100, _entries.Count - 100);

            Save();
        }

        public void ClearHistory()
        {
            _entries.Clear();
            Save();
        }
    }
}
