using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    /// <summary>
    /// SelectedPlaka (string, sadece plaka) ile ItemsControl item text ("PLAKA - Þoför")
    /// arasýndaki eþleþmeyi kontrol eder. Eþitse true döner.
    /// </summary>
    public class SelectedPlakaToItemMatchConverter : IMultiValueConverter
    {
        private static readonly string[] separator = new[] { " - " };

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var selectedPlaka = values[0] as string;
            var itemText = values[1] as string;

            if (string.IsNullOrWhiteSpace(selectedPlaka) || string.IsNullOrWhiteSpace(itemText))
                return false;

            var platePart = itemText.Split(separator, StringSplitOptions.None).FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(platePart))
                return false;

            return string.Equals(selectedPlaka, platePart, StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
