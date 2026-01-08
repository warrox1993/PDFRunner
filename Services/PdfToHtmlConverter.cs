using System;
using System.IO;
using System.Threading.Tasks;
using SautinSoft;

namespace ConvertPDF.Services
{
    public interface IPdfToHtmlConverter
    {
        Task<string> ConvertAsync(string pdfPath, string outputPath, bool flowingMode = false, bool embedImages = false, IProgress<(int percent, string message)>? progress = null);
    }

    public class PdfToHtmlConverter : IPdfToHtmlConverter
    {
        public async Task<string> ConvertAsync(string pdfPath, string outputPath, bool flowingMode = false, bool embedImages = false, IProgress<(int percent, string message)>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report((10, "Initializing PDF Focus..."));

                    var focus = new PdfFocus();
                    focus.OpenPdf(pdfPath);

                    if (focus.PageCount == 0)
                    {
                        throw new Exception("PDF contains no pages or is corrupted.");
                    }

                    progress?.Report((30, $"Processing {focus.PageCount} pages..."));

                    // Configure HTML options
                    string docTitle = Path.GetFileNameWithoutExtension(pdfPath);
                    focus.HtmlOptions.Title = docTitle;
                    focus.HtmlOptions.IncludeImageInHtml = embedImages;

                    if (!embedImages)
                    {
                        // Create images subfolder
                        string baseFolder = Path.GetDirectoryName(outputPath) ?? "";
                        string imagesFolder = Path.Combine(baseFolder, $"{docTitle}_images");
                        Directory.CreateDirectory(imagesFolder);
                        focus.HtmlOptions.ImageSubFolder = imagesFolder;
                    }

                    // Set conversion mode
                    if (flowingMode)
                    {
                        // Flowing mode - text reflows like paragraphs
                        progress?.Report((50, "Converting with flowing layout..."));
                    }
                    else
                    {
                        // Fixed mode - preserves exact PDF layout
                        progress?.Report((50, "Converting with fixed layout..."));
                    }

                    progress?.Report((70, "Generating HTML..."));

                    if (focus.ToHtml(outputPath) == 0)
                    {
                        progress?.Report((100, "Conversion complete!"));
                        string mode = flowingMode ? "Flowing" : "Fixed";
                        return $"SautinSoft.PdfFocus (HTML5 {mode})";
                    }

                    throw new Exception("HTML conversion failed.");
                }
                catch (Exception ex)
                {
                    progress?.Report((0, $"Error: {ex.Message}"));
                    throw new Exception($"PDF to HTML conversion failed: {ex.Message}", ex);
                }
            });
        }
    }
}
