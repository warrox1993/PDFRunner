using System;
using System.Globalization;
using System.Windows.Data;
using ConvertPDF.Models;

namespace ConvertPDF.Converters
{
    public class FormatToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConversionFormat format)
            {
                return format switch
                {
                    ConversionFormat.PDF => "FilePdfBox",
                    ConversionFormat.Word => "FileWordBox",
                    ConversionFormat.Excel => "FileExcelBox",
                    ConversionFormat.ExcelCSV => "FileDelimited",
                    ConversionFormat.HTML => "FileCodeOutline",
                    ConversionFormat.Image => "FileImageOutline",
                    ConversionFormat.Text => "FileDocumentOutline",
                    _ => "FileDocument"
                };
            }
            return "FileDocument";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
