using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.IO;
using System.Linq;

namespace ConvertPDF.Services
{
    public class PdfPageManipulator
    {
        public int GetPageCount(string pdfPath)
        {
             using (var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import))
             {
                 return doc.PageCount;
             }
        }

        public void SplitPdf(string inputPath, string outputFolder)
        {
            using (var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
            {
                string baseName = Path.GetFileNameWithoutExtension(inputPath);
                for (int i = 0; i < inputDoc.PageCount; i++)
                {
                    using (var outputDoc = new PdfDocument())
                    {
                        outputDoc.AddPage(inputDoc.Pages[i]);
                        string pageName = $"{baseName}_Page_{i + 1}.pdf";
                        outputDoc.Save(Path.Combine(outputFolder, pageName));
                    }
                }
            }
        }

        public void ExtractPages(string inputPath, string outputPath, int[] pages)
        {
            using (var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
            {
                using (var outputDoc = new PdfDocument())
                {
                    foreach (var pageNum in pages)
                    {
                        // Pages are 1-based in UI, 0-based in array index
                        int index = pageNum - 1;
                        if (index >= 0 && index < inputDoc.PageCount)
                        {
                            outputDoc.AddPage(inputDoc.Pages[index]);
                        }
                    }
                    outputDoc.Save(outputPath);
                }
            }
        }

        public void RotatePage(string inputPath, string outputPath, int pageNumber, int degrees)
        {
            // Open for modify
            // PdfSharpCore requires copying to new doc for safe modification usually, 
            // but we can try Modify mode if we save to new file ?? 
            // Better strategy: Import -> Copy -> Rotate -> Save
            
            using (var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
            {
                using (var outputDoc = new PdfDocument())
                {
                    for (int i = 0; i < inputDoc.PageCount; i++)
                    {
                        var page = outputDoc.AddPage(inputDoc.Pages[i]);
                        
                        if (i == (pageNumber - 1))
                        {
                            // Rotate
                            int currentRotation = page.Rotate;
                            page.Rotate = (currentRotation + degrees) % 360;
                        }
                    }
                    outputDoc.Save(outputPath);
                }
            }
        }

        public void DeletePages(string inputPath, string outputPath, int[] pagesToDelete)
        {
             using (var inputDoc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
            {
                using (var outputDoc = new PdfDocument())
                {
                    for (int i = 0; i < inputDoc.PageCount; i++)
                    {
                        // Check if current page (1-based) is in the delete list
                        if (!pagesToDelete.Contains(i + 1))
                        {
                            outputDoc.AddPage(inputDoc.Pages[i]);
                        }
                    }
                    outputDoc.Save(outputPath);
                }
            }
        }
    }
}
