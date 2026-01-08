using System;
using System.IO;
using System.Threading.Tasks;
using SautinSoft;

namespace ConvertPDF.Services
{
    public class PdfToWordConverter : IDisposable
    {
        public PdfToWordConverter()
        {
            // Constructor kept for compatibility
        }

        public async Task<string> ConvertAsync(string pdfPath, string wordPath, string languageCode = "eng", bool turboMode = false, IProgress<(int percent, string message)> progress = null)
        {
            progress?.Report((10, "Initializing SautinSoft PDF Engine..."));

            return await Task.Run(() =>
            {
                string resultMessage = "Done";

                // Configure SautinSoft.PdfFocus
                PdfFocus f = new PdfFocus();
                
                // IMPORTANT: This version is free for personal use but may have limitations or watermarks in trial mode.
                // For GitHub "Ultra Solid" request, this is the most powerful engine available via NuGet.
                
                // Configure extraction options
                f.WordOptions.Format = PdfFocus.CWordOptions.eWordDocument.Docx;
                f.WordOptions.KeepCharScaleAndSpacing = true; // Preserve layout fidelity
                
                // Handle Images
                f.WordOptions.RenderMode = PdfFocus.CWordOptions.eRenderMode.Flowing; // Best for editing
                
                progress?.Report((30, "Opening PDF..."));
                f.OpenPdf(pdfPath);

                if (f.PageCount > 0)
                {
                    progress?.Report((50, "Converting pages..."));
                    
                    // Conversion
                    int ret = f.ToWord(wordPath);
                    
                    if (ret == 0) // 0 = Success in SautinSoft
                    {
                        progress?.Report((100, "Done!"));
                        resultMessage = "SautinSoft Success";
                    }
                    else
                    {
                        throw new Exception("SautinSoft Conversion returned error code: " + ret);
                    }
                }
                else
                {
                    throw new Exception("Could not open PDF or it is empty.");
                }

                return resultMessage;
            });
        }

        public void Dispose()
        {
            // No unmanaged resources to dispose in this simplified version
        }
    }
}
