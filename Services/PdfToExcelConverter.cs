using System;
using System.IO;
using System.Threading.Tasks;
using SautinSoft;

namespace ConvertPDF.Services
{
    public interface IPdfToExcelConverter
    {
        Task<string> ConvertAsync(string pdfPath, string outputPath, bool convertAllData = true, IProgress<(int percent, string message)>? progress = null);
    }

    public class PdfToExcelConverter : IPdfToExcelConverter
    {
        public async Task<string> ConvertAsync(string pdfPath, string outputPath, bool convertAllData = true, IProgress<(int percent, string message)>? progress = null)
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

                    // Configure Excel options
                    focus.ExcelOptions.ConvertNonTabularDataToSpreadsheet = convertAllData;
                    focus.ExcelOptions.PreservePageLayout = true;

                    progress?.Report((60, "Converting to Excel..."));

                    // Determine output format based on extension
                    string extension = Path.GetExtension(outputPath).ToLower();
                    
                    if (extension == ".csv")
                    {
                        // CSV Export
                        if (focus.ToExcel(outputPath) == 0)
                        {
                            progress?.Report((100, "Conversion complete!"));
                            return "SautinSoft.PdfFocus (CSV)";
                        }
                    }
                    else
                    {
                        // Excel Export (.xlsx, .xls)
                        if (focus.ToExcel(outputPath) == 0)
                        {
                            progress?.Report((100, "Conversion complete!"));
                            return "SautinSoft.PdfFocus (Excel)";
                        }
                    }

                    throw new Exception("Excel conversion failed - check PDF content for tables.");
                }
                catch (Exception ex)
                {
                    progress?.Report((0, $"Error: {ex.Message}"));
                    throw new Exception($"PDF to Excel conversion failed: {ex.Message}", ex);
                }
            });
        }
    }
}
