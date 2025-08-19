using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // Çoklu baðlama: [0] alan deðeri, [1] Aciklama
    // Aciklama == "Toplam" ise ilk deðeri boþ döndürür; aksi halde deðeri uygun biçimde string döndürür.
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
                // Litre/Tutar hariç kolonlar bu converter ile baðlanacaðýndan toplam satýrda boþ gösterilecekler
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
