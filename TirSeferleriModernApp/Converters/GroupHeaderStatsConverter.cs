using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // Multivalue: [0]=CollectionViewGroup, [1]=int totalCount
    public class GroupHeaderStatsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return string.Empty;
            var grp = values[0] as System.Windows.Data.CollectionViewGroup;
            int total = 0;
            if (values[1] is int t) total = t;
            if (grp == null) return string.Empty;

            try
            {
                var items = grp.Items.Cast<object>();
                int count = items.Count();
                // Yalnýzca rota sayýsý ve toplamý göster; min/maks/ort kaldýrýldý
                return $"— {count} rota / toplam={total}";
            }
            catch
            {
                return string.Empty;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => Array.Empty<object>();
    }
}
