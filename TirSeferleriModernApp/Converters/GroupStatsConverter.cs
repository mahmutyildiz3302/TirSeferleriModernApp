using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Controls;

namespace TirSeferleriModernApp.Converters
{
    // Converts a CollectionViewGroup to a stats string: sadece "— X rota" (min/maks/ort kaldýrýldý)
    public class GroupStatsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CollectionViewGroup grp)
            {
                try
                {
                    var count = grp.Items?.Count ?? 0;
                    return $"— {count} rota";
                }
                catch { }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
