using System;

namespace ConvertPDF.Services
{
    public class PdfCompressionService
    {
        public void CompressPdf(string inputPath, string outputPath, CompressionLevel level)
        {
            // TODO: Implement PDF compression using PdfSharpCore
            //  For now, just copy the file
            System.IO.File.Copy(inputPath, outputPath, true);
        }
    }
}
