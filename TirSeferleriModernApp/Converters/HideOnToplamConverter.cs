using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // �oklu ba�lama: [0] alan de�eri, [1] Aciklama
    // Aciklama == "Toplam" ise ilk de�eri bo� d�nd�r�r; aksi halde de�eri uygun bi�imde string d�nd�r�r.
    public class HideOnToplamConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2)
                return string.Empty;

            var value = values[0];
            var aciklama = values[1]?.ToString();
            if (string.Equals(aciklama, "Toplam", StringComparison.OrdinalIgnoreCase))
            {
                // Litre/Tutar hari� kolonlar bu converter ile ba�lanaca��ndan toplam sat�rda bo� g�sterilecekler
                return string.Empty;
            }

            if (value is null)
                return string.Empty;

            return value switch
            {
                DateTime dt => dt == default ? string.Empty : dt.ToString("yyyy-MM-dd"),
                IFormattable f => f.ToString(null, culture),
                _ => value.ToString() ?? string.Empty
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
