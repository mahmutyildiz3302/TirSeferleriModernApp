using System;
using System.Globalization;
using System.Windows;
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
        {
            // Sadece ilk hedefi g�ncelle, di�erlerine dokunma.
            if (targetTypes == null || targetTypes.Length == 0)
                return Array.Empty<object>();

            var results = new object[targetTypes.Length];

            // [0] ger�ek alan: parse et, ba�ar�s�zsa orijinali korumak i�in Binding.DoNothing g�nder
            results[0] = TryConvert(value, targetTypes[0], culture);

            // [1..] (Aciklama vb.) g�ncellenmesin
            for (int i = 1; i < targetTypes.Length; i++)
                results[i] = Binding.DoNothing;

            return results;
        }

        private static object TryConvert(object value, Type targetType, CultureInfo culture)
        {
            try
            {
                if (targetType == typeof(object))
                    return value; // hi�bir d�n��t�rme gerekmiyor

                var nnType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (value == null)
                    return nnType == typeof(string) ? null : Binding.DoNothing;

                if (nnType.IsInstanceOfType(value))
                    return value;

                var s = value as string ?? value.ToString() ?? string.Empty;

                // Bo� de�erler: string i�in bo�, di�er tipler i�in g�ncelleme yok
                if (string.IsNullOrWhiteSpace(s))
                    return nnType == typeof(string) ? string.Empty : Binding.DoNothing;

                if (nnType == typeof(string))
                    return s;

                if (nnType == typeof(DateTime))
                {
                    return DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt)
                        ? dt
                        : Binding.DoNothing;
                }

                if (nnType == typeof(TimeSpan))
                {
                    return TimeSpan.TryParse(s, culture, out var ts)
                        ? ts
                        : Binding.DoNothing;
                }

                if (nnType.IsEnum)
                {
                    return Enum.TryParse(nnType, s, true, out var ev)
                        ? ev!
                        : Binding.DoNothing;
                }

                // Say�sal vb.
                var converted = System.Convert.ChangeType(s, nnType, culture);
                return converted ?? Binding.DoNothing;
            }
            catch
            {
                // Hata durumunda geri yaz�m� iptal et (orijinal de�er korunur)
                return Binding.DoNothing;
            }
        }
    }
}
