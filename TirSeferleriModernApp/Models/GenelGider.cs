using System;

namespace TirSeferleriModernApp.Models
{
    public class GenelGider
    {
        public int GiderId { get; set; }
        public int? CekiciId { get; set; }
        public string? Plaka { get; set; }
        public DateTime Tarih { get; set; }
        public decimal Tutar { get; set; }
        public string? Aciklama { get; set; }
    }
}
