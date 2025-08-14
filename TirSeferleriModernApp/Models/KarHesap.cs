using System;

namespace TirSeferleriModernApp.Models
{
    public class KarHesap
    {
        public int KarHesapId { get; set; }
        public int CekiciId { get; set; }
        public decimal Gelir { get; set; }
        public decimal Gider { get; set; }
        public decimal NetKar { get; set; }
        public DateTime Tarih { get; set; }

        // Nullability uyarılarını düzeltmek için varsayılan değerler eklendi
        public string Plaka { get; set; } = string.Empty;
        public string SoforAdi { get; set; } = string.Empty;

        // Örnek bir metot: Null döndürme ihtimali varsa kontrol eklenir
        public static KarHesap? GetKarHesapById(int id)
        {
            // Örnek: Veritabanından veri çekme simülasyonu
            if (id <= 0)
            {
                return null; // Null döndürme ihtimali kontrol edildi
            }

            return new KarHesap
            {
                KarHesapId = id,
                CekiciId = 1,
                Gelir = 1000m,
                Gider = 500m,
                NetKar = 500m,
                Tarih = DateTime.Now,
                Plaka = "34 ABC 123",
                SoforAdi = "Ahmet Yılmaz"
            };
        }
    }
}
