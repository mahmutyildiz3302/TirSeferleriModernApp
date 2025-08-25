using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Controls;

namespace TirSeferleriModernApp.Converters
{
    // Converts a CollectionViewGroup to a stats string: "— X rota | min=... maks=... ort=..."
    public class GroupStatsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CollectionViewGroup grp)
            {
                // Try to project Fiyat from anonymous/POCO items via dynamic access
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
                    return $"— {count} rota | min={min:0} maks={max:0} ort={avg:0}";
                }
                catch { }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
