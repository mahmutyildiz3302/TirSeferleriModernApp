using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // DataGridRow.AlternationIndex 0 tabanl�d�r; kullan�c�ya 1 tabanl� s�ra numaras� g�stermek i�in +1 �evirir.
    public class RowIndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int idx)
                return (idx + 1).ToString(culture);
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
