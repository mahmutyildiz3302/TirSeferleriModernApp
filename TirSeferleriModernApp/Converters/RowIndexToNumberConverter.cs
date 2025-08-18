using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // DataGridRow.AlternationIndex 0 tabanlýdýr; kullanýcýya 1 tabanlý sýra numarasý göstermek için +1 çevirir.
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
