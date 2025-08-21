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

        private string? _yuklemeYeri;
        public string? YuklemeYeri
        {
            get => _yuklemeYeri;
            set => SetProperty(ref _yuklemeYeri, value);
        }

        private string? _bosaltmaYeri;
        public string? BosaltmaYeri
        {
            get => _bosaltmaYeri;
            set => SetProperty(ref _bosaltmaYeri, value);
        }

        private string? _ekstra;
        public string? Ekstra
        {
            get => _ekstra;
            set => SetProperty(ref _ekstra, value);
        }

        private string? _bosDolu;
        public string? BosDolu
        {
            get => _bosDolu;
            set => SetProperty(ref _bosDolu, value);
        }

        public DateTime Tarih { get; set; }
        public string? Saat { get; set; }

        private decimal _fiyat;
        public decimal Fiyat
        {
            get => _fiyat;
            set => SetProperty(ref _fiyat, value);
        }

        public string? Aciklama { get; set; }
        public int? CekiciId { get; set; }
        public string? CekiciPlaka { get; set; }
        public int? DorseId { get; set; }
        public int? SoforId { get; set; }
        public string? SoforAdi { get; set; }
    }
}
