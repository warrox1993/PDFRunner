using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ConvertPDF.Services
{
    public class WordToPdfConverter : IConverterService
    {
        private dynamic _wordApp;

        public WordToPdfConverter()
        {
            // OcrProcessor removed
        }

        public void InitializeBatch()
        {
            if (_wordApp == null)
            {
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                    throw new Exception("Microsoft Word is not installed on this computer.");

                _wordApp = Activator.CreateInstance(wordType);
                try { _wordApp.Visible = false; } catch { }
            }
        }

        public void DisposeBatch()
        {
            if (_wordApp != null)
            {
                try { _wordApp.Quit(0); } catch { }
                _wordApp = null;
            }
        }

        public void Dispose()
        {
            DisposeBatch();
        }

        public async Task ConvertAsync(string inputPath, string outputPath, bool enableOcr, string languageCode = "eng", IProgress<(int percent, string message)> progress = null)
        {
            await Task.Run(async () =>
            {
                progress?.Report((10, "Starting Word to PDF..."));
                bool localInstance = false;
                dynamic appToUse = _wordApp;

                try
                {
                    if (appToUse == null)
                    {
                        Type wordType = Type.GetTypeFromProgID("Word.Application");
                        if (wordType == null) throw new Exception("Microsoft Word is not installed.");
                        appToUse = Activator.CreateInstance(wordType);
                        try { appToUse.Visible = false; } catch { }
                        localInstance = true;
                    }

                    dynamic doc = null;
                    string tempPdfPath = enableOcr ? Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf") : outputPath;

                    try
                    {
                        doc = appToUse.Documents.Open(inputPath, ReadOnly: true, Visible: false);
                        
                        progress?.Report((30, "Conversion format..."));

                        // PREMIUM QUALITY EXPORT
                        // ExportAsFixedFormat parameters for MAXIMUM quality
                        doc.ExportAsFixedFormat(
                            OutputFileName: tempPdfPath,
                            ExportFormat: 17,                     // wdExportFormatPDF
                            OpenAfterExport: false,
                            OptimizeFor: 0,                       // wdExportOptimizeForPrint (0 = Quality, 1 = Size)
                            Range: 0,                             // wdExportAllDocument
                            From: 1,
                            To: 1,
                            Item: 0,                              // wdExportDocumentContent
                            IncludeDocProps: true,                // Preserve document properties
                            KeepIRM: false,
                            CreateBookmarks: 1,                   // wdExportCreateHeadingBookmarks
                            DocStructureTags: true,               // PDF structure tags for accessibility
                            BitmapMissingFonts: true,             // Embed missing fonts as bitmaps
                            UseISO19005_1: false,                 // PDF/A compliance (disable for smaller size)
                            FixedFormatExtClassPtr: Type.Missing
                        );
                        
                        progress?.Report((80, "Finalisation..."));
                        
                        doc.Close(0); // DoNotSaveChanges
                        doc = null;

                        // If OCR is enabled, move temp file to final destination
                        if (enableOcr && File.Exists(tempPdfPath))
                        {
                            progress?.Report((90, "Saving final PDF..."));
                            // Ensure output directory exists
                            string outputDir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }
                            // Copy temp PDF to final location
                            File.Copy(tempPdfPath, outputPath, overwrite: true);
                            // Delete temp file
                            try { File.Delete(tempPdfPath); } catch { }
                        }

                        // Final verification
                        if (!File.Exists(outputPath))
                        {
                            throw new Exception($"Conversion failed: output file not created at {outputPath}");
                        }

                        progress?.Report((100, "Conversion complete!"));
                    }
                    catch
                    {
                        try { doc?.Close(0); } catch { }
                        throw;
                    }
                    finally
                    {
                        if (localInstance)
                        {
                            try { appToUse.Quit(0); } catch { }
                        }
                    }
                }
                catch (Exception)
                {
                   // If shared instance crashed, null it
                   if (!localInstance && _wordApp == appToUse) _wordApp = null;
                   throw;
                }
            });
        }
    }
}
