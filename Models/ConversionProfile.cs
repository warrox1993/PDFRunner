using ConvertPDF.Services;

namespace ConvertPDF.Models
{
    public class ConversionProfile
    {
        public string Name { get; set; } = "Default";
        public bool EnableOcr { get; set; } = false;
        public string OcrLanguage { get; set; } = "fr";
        public string CompressionLevel { get; set; } = "None";
        public string DefaultOutputFolder { get; set; } = "";
    }
}
