using System;

namespace TirSeferleriModernApp.Models
{
    public class SanaiGider
    {
        public int SanaiId { get; set; }
        public int? CekiciId { get; set; }
        public string? Plaka { get; set; }
        public DateTime Tarih { get; set; }
        public string? Kalem { get; set; } // iþlem/kalem
        public decimal Tutar { get; set; }
        public int? Km { get; set; }
        public string? Aciklama { get; set; }
    }
}
