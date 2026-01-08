using System;
using System.Globalization;
using System.Windows.Data;
using ConvertPDF.Models;

namespace ConvertPDF.Converters
{
    public class ConversionFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConversionFormat format)
            {
                return format switch
                {
                    ConversionFormat.PDF => "PDF",
                    ConversionFormat.Word => "Word",
                    ConversionFormat.Excel => "Excel (.xlsx)",
                    ConversionFormat.ExcelCSV => "CSV",
                    ConversionFormat.HTML => "HTML",
                    ConversionFormat.Image => "Image",
                    ConversionFormat.Text => "Text",
                    _ => value.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
