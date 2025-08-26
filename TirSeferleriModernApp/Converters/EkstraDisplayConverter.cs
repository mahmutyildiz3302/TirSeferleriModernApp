using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    public class EkstraDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString()?.Trim();
            if (string.Equals(s, "EKSTRA YOK", StringComparison.OrdinalIgnoreCase))
                return " ";
            return value ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                return " "; // bo� g�sterim -> veritaban�nda tek bo�luk olarak tutulacak
            return s!;
        }
    }
}
