using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace ConvertPDF.Services
{
    public class PdfMergeService
    {
        public async Task MergePdfFiles(List<string> inputFiles, string outputPath)
        {
            await Task.Run(() =>
            {
                using (var outputDocument = new PdfDocument())
                {
                    foreach (var file in inputFiles)
                    {
                        using (var inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                        {
                            int count = inputDocument.PageCount;
                            for (int idx = 0; idx < count; idx++)
                            {
                                var page = inputDocument.Pages[idx];
                                outputDocument.AddPage(page);
                            }
                        }
                    }
                    outputDocument.Save(outputPath);
                }
            });
        }
    }
}
