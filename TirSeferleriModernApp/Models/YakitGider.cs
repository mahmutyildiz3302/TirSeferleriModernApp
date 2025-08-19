using System;

namespace TirSeferleriModernApp.Models
{
    public class YakitGider
    {
        public int YakitId { get; set; }
        public int? CekiciId { get; set; }
        public string? Plaka { get; set; }
        public DateTime Tarih { get; set; }
        public string? Istasyon { get; set; }
        public decimal Litre { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal Tutar { get; set; }
        public int? Km { get; set; }
        public string? Aciklama { get; set; }
    }
}