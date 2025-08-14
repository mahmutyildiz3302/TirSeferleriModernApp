using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TirSeferleriModernApp.Models
{
    public class Gider
    {
        public int GiderId { get; set; }
        public int CekiciId { get; set; }
        public string? Aciklama { get; set; }
        public decimal Tutar { get; set; }
        public DateTime Tarih { get; set; }
    }
}
