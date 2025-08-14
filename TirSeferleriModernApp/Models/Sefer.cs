using System;
using CommunityToolkit.Mvvm.ComponentModel;

// bu dosya Sefer verisini temsil eden model sınıfıdır.
// Bir seferin tüm alanlarını (KonteynerNo, Tarih, Fiyat, vs.) içerir. 

namespace TirSeferleriModernApp.Models
{
    public class Sefer : ObservableObject
    {
        public int SeferId { get; set; } // Id -> SeferId olarak değiştirildi
        public string? KonteynerNo { get; set; }
        public string? KonteynerBoyutu { get; set; } // "20" ya da "40"
        public string? YuklemeYeri { get; set; }
        public string? BosaltmaYeri { get; set; }
        public DateTime Tarih { get; set; }
        public string? Saat { get; set; }
        public decimal Fiyat { get; set; }
        public string? Aciklama { get; set; }
        public int? CekiciId { get; set; }
        public string? CekiciPlaka { get; set; }
        public int? DorseId { get; set; }
        public int? SoforId { get; set; }
        public string? SoforAdi { get; set; }
    }
}
