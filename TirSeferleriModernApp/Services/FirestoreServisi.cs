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

        // AppSettings.json'dan proje kimli�i ve kimlik bilgisi yolu okunur ve Firestore'a ba�lan�l�r.
        // Ba�lant� kurulamazsa anla��l�r bir hata mesaj� ile istisna f�rlat�l�r.
        public async Task Baglan()
        {
            var settings = AppSettingsHelper.Current;
            var projectId = settings.FirebaseProjectId?.Trim();
            var credPath = settings.GoogleApplicationCredentialsPath?.Trim();

            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("Firestore ba�lant�s� i�in AppSettings.json i�inde 'FirebaseProjectId' de�eri gereklidir.");

            if (string.IsNullOrWhiteSpace(credPath))
                throw new InvalidOperationException("Firestore ba�lant�s� i�in AppSettings.json i�inde 'GoogleApplicationCredentialsPath' de�eri gereklidir.");

            if (!File.Exists(credPath))
                throw new FileNotFoundException($"Google kimlik bilgisi dosyas� bulunamad�: {credPath}");

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
                    $"Firestore'a ba�lan�lamad�. L�tfen proje kimli�i ve kimlik bilgisi dosyas�n� kontrol edin. Ayr�nt�: {ex.Message}", ex);
            }
        }

        // �leride buluta yazma/g�ncelleme i�lemleri yap�lacak.
        public Task BulutaYazOrGuncelle(object? veri = null, string? koleksiyon = null, string? belgeId = null)
        {
            return Task.CompletedTask;
        }

        // �leride t�m verileri dinleme/subscribe i�lemleri yap�lacak.
        public void HepsiniDinle(Action<object?>? onChanged = null)
        {
        }
    }
}
