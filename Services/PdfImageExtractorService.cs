using System;
using System.IO;
using System.Threading.Tasks;
using SautinSoft;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Service for extracting images from PDF files using SautinSoft PdfFocus.
    /// Reuses existing SautinSoft dependency to avoid redundancy.
    /// </summary>
    public class PdfImageExtractorService
    {
        public async Task<int> ExtractImagesAsync(string pdfPath, string outputFolder, IProgress<string> progress = null)
        {
            return await Task.Run(() =>
            {
                progress?.Report("Initializing PDF extraction...");
                
                if (!Directory.Exists(outputFolder))
                {
                    Directory.CreateDirectory(outputFolder);
                }

                var focus = new PdfFocus();
                focus.OpenPdf(pdfPath);

                if (focus.PageCount == 0)
                {
                    throw new Exception("PDF file is empty or cannot be read.");
                }

                progress?.Report($"Extracting images from {focus.PageCount} pages...");

                // Extract all images
                var imageList = focus.ExtractImages();

                if (imageList == null || imageList.Count == 0)
                {
                    progress?.Report("No images found in the PDF.");
                    return 0;
                }

                // Save images to output folder with proper naming
                string baseName = Path.GetFileNameWithoutExtension(pdfPath);
                int count = 0;

                foreach (var pdfImage in imageList)
                {
                    count++;
                    string newFileName = $"{baseName}_Image_{count}.png";
                    string destinationPath = Path.Combine(outputFolder, newFileName);

                    // Save the image using SkiaSharp encoding
                    using (var stream = File.Create(destinationPath))
                    {
                        pdfImage.Picture.Encode(stream, SkiaSharp.SKEncodedImageFormat.Png, 95);
                    }
                    progress?.Report($"Extracted: {newFileName}");
                }

                progress?.Report($"Extraction complete! {count} images saved.");
                return count;
            });
        }
    }
}
