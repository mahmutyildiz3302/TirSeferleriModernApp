using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace TirSeferleriModernApp.Services
{
    public class FirestoreServisi
    {
        private FirestoreDb? _db;
        public FirestoreDb? Db => _db;

        // AppSettings.json'dan proje kimliði ve kimlik bilgisi yolu okunur ve Firestore'a baðlanýlýr.
        // Baðlantý kurulamazsa anlaþýlýr bir hata mesajý ile istisna fýrlatýlýr.
        public async Task Baglan()
        {
            var settings = AppSettingsHelper.Current;
            var projectId = settings.FirebaseProjectId?.Trim();
            var credPath = settings.GoogleApplicationCredentialsPath?.Trim();

            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("Firestore baðlantýsý için AppSettings.json içinde 'FirebaseProjectId' deðeri gereklidir.");

            if (string.IsNullOrWhiteSpace(credPath))
                throw new InvalidOperationException("Firestore baðlantýsý için AppSettings.json içinde 'GoogleApplicationCredentialsPath' deðeri gereklidir.");

            if (!File.Exists(credPath))
                throw new FileNotFoundException($"Google kimlik bilgisi dosyasý bulunamadý: {credPath}");

            var currentEnv = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(currentEnv) || !string.Equals(currentEnv, credPath, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
            }

            try
            {
                _db = await FirestoreDb.CreateAsync(projectId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Firestore'a baðlanýlamadý. Lütfen proje kimliði ve kimlik bilgisi dosyasýný kontrol edin. Ayrýntý: {ex.Message}", ex);
            }
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
