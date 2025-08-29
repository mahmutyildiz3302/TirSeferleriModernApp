using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using TirSeferleriModernApp.Models;

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

        // Bulutta yeni belge olu�turur veya mevcut belgeyi g�nceller.
        // Ba�ar�l�ysa belge ID'sini, hata olursa k�sa bir mesaj d�nd�r�r.
        public async Task<string> BulutaYazOrGuncelle(Record r)
        {
            try
            {
                if (r == null) return "Ge�ersiz veri";

                if (_db == null)
                {
                    await Baglan();
                }
                if (_db == null) return "Firestore ba�lant�s� kurulamad�";

                var collection = _db.Collection("records");

                var data = new System.Collections.Generic.Dictionary<string, object?>
                {
                    ["id"] = r.id,
                    ["remote_id"] = r.remote_id,
                    ["updated_at"] = r.updated_at,
                    ["is_dirty"] = r.is_dirty,
                    ["deleted"] = r.deleted,
                    ["containerNo"] = r.containerNo,
                    ["loadLocation"] = r.loadLocation,
                    ["unloadLocation"] = r.unloadLocation,
                    ["size"] = r.size,
                    ["status"] = r.status,
                    ["nightOrDay"] = r.nightOrDay,
                    ["truckPlate"] = r.truckPlate,
                    ["notes"] = r.notes,
                    ["createdByUserId"] = r.createdByUserId,
                    ["createdAt"] = r.createdAt
                };

                if (string.IsNullOrWhiteSpace(r.remote_id))
                {
                    var added = await collection.AddAsync(data);
                    var newId = added.Id;
                    r.remote_id = newId; // geri setlemek faydal� olur
                    return newId;
                }
                else
                {
                    var doc = collection.Document(r.remote_id);
                    await doc.SetAsync(data, SetOptions.MergeAll);
                    return r.remote_id;
                }
            }
            catch (Exception ex)
            {
                return $"Hata: {ex.Message}";
            }
        }

        // �leride buluta yazma/g�ncelleme i�lemleri yap�lacak. (Genel stub korunur)
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
