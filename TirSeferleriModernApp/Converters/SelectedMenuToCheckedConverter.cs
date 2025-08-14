using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // Aktif alt men� ba�l��� (string) ile item'in Baslik (string) de�eri e�itse true d�ner
    public class SelectedMenuToCheckedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var active = values[0] as string;
            var itemTitle = values[1] as string;

            if (string.IsNullOrWhiteSpace(active) || string.IsNullOrWhiteSpace(itemTitle))
                return false;

            return string.Equals(active, itemTitle, StringComparison.Ordinal);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
