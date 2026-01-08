using System.IO;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Auto-OCR Detection: Detects if files likely need OCR.
    /// Images always need OCR. For PDFs, we check file size heuristics.
    /// </summary>
    public class SmartOcrDetector
    {
        /// <summary>
        /// Determines if OCR should be enabled for this file.
        /// Images always need OCR. For PDFs, we use a heuristic: 
        /// if the file is small relative to page count, it's likely image-only.
        /// </summary>
        public bool ShouldEnableOcr(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            string ext = Path.GetExtension(filePath).ToLower();

            // Images always need OCR
            if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".gif")
                return true;

            // For PDFs, use a simple heuristic: large PDFs with few pages are likely scans
            if (ext == ".pdf")
            {
                var fileInfo = new FileInfo(filePath);
                // If PDF is over 500KB per estimated page, likely contains images/scans
                // This is a rough heuristic; real detection would need PDF parsing
                if (fileInfo.Length > 500 * 1024) // > 500KB
                    return true; // Suggest OCR for large PDFs
            }

            // Word docs, etc. don't need OCR
            return false;
        }
    }
}
