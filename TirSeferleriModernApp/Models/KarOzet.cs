using System.Collections.Generic;

namespace TirSeferleriModernApp.Models
{
    public class KarKalem
    {
        public string Ad { get; set; } = string.Empty;
        public decimal Tutar { get; set; }
    }

    public class KarOzet
    {
        public decimal Gelir { get; set; }
        public decimal ToplamGider { get; set; }
        public decimal Kar => Gelir - ToplamGider;
        public List<KarKalem> Kalemler { get; set; } = new();
    }
}
