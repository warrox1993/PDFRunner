using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;

namespace ConvertPDF.Services
{
    public class ImageToPdfConverter : IConverterService
    {
        public ImageToPdfConverter() { }

        public void InitializeBatch() { }
        public void DisposeBatch() { }
        public void Dispose() { }

        public async Task ConvertAsync(string inputPath, string outputPath, bool enableOcr, string languageCode = "eng", IProgress<(int percent, string message)> progress = null)
        {
             await Task.Run(() =>
             {
                 try
                 {
                     progress?.Report((10, "Loading Image..."));
                     using (var document = new PdfDocument())
                     {
                         var page = document.AddPage();
                         using (var image = XImage.FromFile(inputPath))
                         {
                             // Resize page to image size or fit image to page
                             page.Width = image.PointWidth;
                             page.Height = image.PointHeight;

                             using (var gfx = XGraphics.FromPdfPage(page))
                             {
                                 progress?.Report((50, "Drawing to PDF..."));
                                 gfx.DrawImage(image, 0, 0, image.PointWidth, image.PointHeight);
                             }
                         }
                         
                         progress?.Report((90, "Saving..."));
                         document.Save(outputPath);
                     }
                     progress?.Report((100, "Done"));
                 }
                 catch (Exception ex)
                 {
                     throw new Exception($"Image conversion failed: {ex.Message}", ex);
                 }
             });
        }
    }
}
