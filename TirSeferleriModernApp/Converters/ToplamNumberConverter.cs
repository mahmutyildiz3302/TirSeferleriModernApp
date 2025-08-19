using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // values[0] => sayi (decimal/double/int), values[1] => Aciklama
    // Aciklama == "Toplam" ise nokta binlik, virgul ondalik kullanarak formatlar.
    public class ToplamNumberConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return string.Empty;

            var sayiObj = values[0];
            var aciklama = values[1]?.ToString();
            if (sayiObj == null)
                return string.Empty;

            // Kaç basamak gösterileceði (parametre ile gelebilir)
            int decimals = 2;
            if (parameter != null && int.TryParse(parameter.ToString(), out var d))
                decimals = d;

            if (string.Equals(aciklama, "Toplam", StringComparison.OrdinalIgnoreCase))
            {
                var nfi = new NumberFormatInfo
                {
                    NumberGroupSeparator = ".",
                    NumberDecimalSeparator = ","
                };
                var fmt = "N" + decimals; // örn: N2
                try
                {
                    var dec = System.Convert.ToDecimal(sayiObj, CultureInfo.InvariantCulture);
                    return dec.ToString(fmt, nfi);
                }
                catch
                {
                    return sayiObj.ToString() ?? string.Empty;
                }
            }

            // Toplam deðilse normal metin
            return sayiObj.ToString() ?? string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
