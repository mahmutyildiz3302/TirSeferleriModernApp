using System;

namespace TirSeferleriModernApp.Models
{
    public class PersonelGider
    {
        public int PersonelGiderId { get; set; }
        public int? CekiciId { get; set; }
        public string? Plaka { get; set; }
        public DateTime Tarih { get; set; }
        public string? PersonelAdi { get; set; }
        public string? OdemeTuru { get; set; } // Maas, Avans, SGK, DigerVergi
        public string? SgkDonem { get; set; }  // �rn: 2025/08
        public string? VergiTuru { get; set; } // �rn: Damga, Muhtasar
        public decimal Tutar { get; set; }
        public string? Aciklama { get; set; }
    }
}
