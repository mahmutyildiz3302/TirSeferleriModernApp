using System;
using System.Globalization;
using System.Windows.Data;

namespace TirSeferleriModernApp.Converters
{
    // Tek de�erli d�n��t�r�c�: say�lar� nokta binlik, virg�l ondal�kla bi�imlendirir.
    // ConverterParameter: g�sterilecek ondal�k basamak say�s� (varsay�lan 2)
    public class NumericTrConverter : IValueConverter
    {
        private static readonly NumberFormatInfo TrLike = new()
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ","
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return string.Empty;

            int decimals = 2;
            if (parameter != null && int.TryParse(parameter.ToString(), out var d))
                decimals = d;

            var fmt = "N" + decimals; // �rn: N2
            try
            {
                var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return dec.ToString(fmt, TrLike);
            }
            catch
            {
                return value.ToString() ?? string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
