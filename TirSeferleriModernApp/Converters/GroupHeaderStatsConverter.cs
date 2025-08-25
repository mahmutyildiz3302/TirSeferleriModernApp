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
                var fiyatlar = items.Select(it =>
                {
                    var prop = it.GetType().GetProperty("Fiyat");
                    if (prop != null)
                    {
                        var v = prop.GetValue(it);
                        if (v is decimal dec) return dec;
                        if (v is double dbl) return (decimal)dbl;
                        if (v is float fl) return (decimal)fl;
                    }
                    return 0m;
                }).ToList();
                int count = fiyatlar.Count;
                decimal min = count > 0 ? fiyatlar.Min() : 0m;
                decimal max = count > 0 ? fiyatlar.Max() : 0m;
                decimal avg = count > 0 ? (decimal)fiyatlar.Average(x => (double)x) : 0m;
                return $"— {count} rota / toplam={total} | min={min:0} maks={max:0} ort={avg:0}";
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
