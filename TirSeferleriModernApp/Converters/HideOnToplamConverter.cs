using System;
using System.Globalization;
using System.Windows;
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
        {
            // Sadece ilk hedefi güncelle, diðerlerine dokunma.
            if (targetTypes == null || targetTypes.Length == 0)
                return Array.Empty<object>();

            var results = new object[targetTypes.Length];

            // [0] gerçek alan: parse et, baþarýsýzsa orijinali korumak için Binding.DoNothing gönder
            results[0] = TryConvert(value, targetTypes[0], culture);

            // [1..] (Aciklama vb.) güncellenmesin
            for (int i = 1; i < targetTypes.Length; i++)
                results[i] = Binding.DoNothing;

            return results;
        }

        private static object TryConvert(object value, Type targetType, CultureInfo culture)
        {
            try
            {
                if (targetType == typeof(object))
                    return value; // hiçbir dönüþtürme gerekmiyor

                var nnType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (value == null)
                    return nnType == typeof(string) ? null : Binding.DoNothing;

                if (nnType.IsInstanceOfType(value))
                    return value;

                var s = value as string ?? value.ToString() ?? string.Empty;

                // Boþ deðerler: string için boþ, diðer tipler için güncelleme yok
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

                // Sayýsal vb.
                var converted = System.Convert.ChangeType(s, nnType, culture);
                return converted ?? Binding.DoNothing;
            }
            catch
            {
                // Hata durumunda geri yazýmý iptal et (orijinal deðer korunur)
                return Binding.DoNothing;
            }
        }
    }
}
