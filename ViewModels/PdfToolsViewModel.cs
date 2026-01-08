using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConvertPDF.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;

namespace ConvertPDF.ViewModels
{
    public partial class PdfToolsViewModel : ObservableObject
    {
        private readonly PdfMergeService _mergeService;
        private readonly PdfPageManipulator _pdfTools;

        [ObservableProperty]
        private string _selectedFilePath = "";

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<string> _mergeFiles = new();

        [ObservableProperty]
        private int _pageCount = 0;

        [ObservableProperty]
        private int _selectedOperation = 0; // 0=Split, 1=Extract, 2=Rotate, 3=Delete, 4=Merge

        [ObservableProperty]
        private string _pageRange = "1-3";

        [ObservableProperty]
        private int _selectedPage = 1;

        [ObservableProperty]
        private int _rotationDegrees = 90;

        [ObservableProperty]
        private string _statusMessage = "Sélectionnez un fichier PDF";

        [ObservableProperty]
        private bool _isBusy = false;

        public PdfToolsViewModel(PdfMergeService mergeService, PdfPageManipulator pdfTools)
        {
            _mergeService = mergeService;
            _pdfTools = pdfTools;
        }

        [RelayCommand]
        private void MoveMergeFileUp(string file)
        {
            int index = MergeFiles.IndexOf(file);
            if (index > 0)
            {
                MergeFiles.Move(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveMergeFileDown(string file)
        {
            int index = MergeFiles.IndexOf(file);
            if (index < MergeFiles.Count - 1)
            {
                MergeFiles.Move(index, index + 1);
            }
        }

        [RelayCommand]
        private void AddMergeFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                    MergeFiles.Add(file);
            }
        }

        [RelayCommand]
        private void RemoveMergeFile(string file) => MergeFiles.Remove(file);

        [RelayCommand]
        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                Title = "Sélectionner un fichier PDF"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFilePath = dialog.FileName;
                PageCount = _pdfTools.GetPageCount(SelectedFilePath);
                StatusMessage = $"{Path.GetFileName(SelectedFilePath)} - {PageCount} page(s)";
            }
        }

        [RelayCommand]
        private async Task Execute()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                MessageBox.Show("Veuillez sélectionner un fichier PDF valide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Traitement en cours...";

            try
            {
                switch (SelectedOperation)
                {
                    case 0: // Split
                        await System.Threading.Tasks.Task.Run(() => ExecuteSplit());
                        break;
                    case 1: // Extract
                        await System.Threading.Tasks.Task.Run(() => ExecuteExtract());
                        break;
                    case 2: // Rotate
                        await System.Threading.Tasks.Task.Run(() => ExecuteRotate());
                        break;
                    case 3: // Delete
                        await System.Threading.Tasks.Task.Run(() => ExecuteDelete());
                        break;
                    case 4: // Merge
                        await System.Threading.Tasks.Task.Run(() => ExecuteMerge());
                        break;
                }

                StatusMessage = "Opération terminée avec succès !";
                MessageBox.Show("Opération terminée avec succès !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteSplit()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Directory|*.none",
                FileName = "SelectFolder",
                CheckFileExists = false,
                CheckPathExists = true,
                Title = "Choisir le dossier de sortie"
            };

            if (dialog.ShowDialog() == true)
            {
                string folder = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folder))
                {
                    _pdfTools.SplitPdf(SelectedFilePath, folder);
                }
            }
        }

        private void ExecuteExtract()
        {
            var pages = ParsePageRange(PageRange);
            if (pages.Length == 0)
            {
                throw new Exception("Format de plage invalide. Exemple: 1-3, 5, 7-9");
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "extracted.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _pdfTools.ExtractPages(SelectedFilePath, dialog.FileName, pages);
            }
        }

        private void ExecuteRotate()
        {
            if (SelectedPage < 1 || SelectedPage > PageCount)
            {
                throw new Exception("Numéro de page invalide");
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "rotated.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _pdfTools.RotatePage(SelectedFilePath, dialog.FileName, SelectedPage, RotationDegrees);
            }
        }

        private void ExecuteDelete()
        {
            var pages = ParsePageRange(PageRange);
            if (pages.Length == 0)
            {
                throw new Exception("Format de plage invalide");
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "modified.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                _pdfTools.DeletePages(SelectedFilePath, dialog.FileName, pages);
            }
        }

        private async Task ExecuteMerge()
        {
            if (MergeFiles.Count < 2)
            {
                throw new Exception("Veuillez sélectionner au moins 2 fichiers à fusionner.");
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "merged.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                await _mergeService.MergePdfFiles(MergeFiles.ToList(), dialog.FileName);
            }
        }

        private int[] ParsePageRange(string range)
        {
            try
            {
                var parts = range.Split(',');
                var pages = new System.Collections.Generic.List<int>();

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.Contains('-'))
                    {
                        var limits = trimmed.Split('-');
                        int start = int.Parse(limits[0].Trim());
                        int end = int.Parse(limits[1].Trim());
                        for (int i = start; i <= end; i++)
                            pages.Add(i);
                    }
                    else
                    {
                        pages.Add(int.Parse(trimmed));
                    }
                }

                return pages.Distinct().OrderBy(p => p).ToArray();
            }
            catch
            {
                return Array.Empty<int>();
            }
        }
    }
}
