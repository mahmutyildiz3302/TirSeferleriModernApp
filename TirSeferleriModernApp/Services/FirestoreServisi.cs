using System;
using System.Threading.Tasks;

namespace TirSeferleriModernApp.Services
{
    public class FirestoreServisi
    {
        // Ýleride Firestore baðlantýsý kurulacak.
        public Task Baglan()
        {
            return Task.CompletedTask;
        }

        // Ýleride buluta yazma/güncelleme iþlemleri yapýlacak.
        public Task BulutaYazOrGuncelle(object? veri = null, string? koleksiyon = null, string? belgeId = null)
        {
            return Task.CompletedTask;
        }

        // Ýleride tüm verileri dinleme/subscribe iþlemleri yapýlacak.
        public void HepsiniDinle(Action<object?>? onChanged = null)
        {
        }
    }
}
