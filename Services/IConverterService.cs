using System;
using System.Threading.Tasks;

namespace ConvertPDF.Services
{
    public interface IConverterService : IDisposable
    {
        Task ConvertAsync(string inputPath, string outputPath, bool enableOcr, string languageCode = "eng", IProgress<(int percent, string message)> progress = null);
        void InitializeBatch();
        void DisposeBatch();
    }
}
