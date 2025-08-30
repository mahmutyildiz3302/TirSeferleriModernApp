using System;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using TirSeferleriModernApp.Models;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace TirSeferleriModernApp.Services
{
    public class FirestoreServisi
    {
        private FirestoreDb? _db;
        public FirestoreDb? Db => _db;
        private FirestoreChangeListener? _recordsListener;

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
                SyncStatusHub.Set("Bulut: Ba�land�");
            }
            catch (Exception ex)
            {
                SyncStatusHub.Set("Bulut: Hata");
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
                SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                return $"Hata: {ex.Message}";
            }
        }

        // �leride buluta yazma/g�ncelleme i�lemleri yap�lacak. (Genel stub korunur)
        public Task BulutaYazOrGuncelle(object? veri = null, string? koleksiyon = null, string? belgeId = null)
        {
            return Task.CompletedTask;
        }

        // records koleksiyonunu dinler. De�i�iklik geldi�inde ilgili yerel kayd�
        // remote_id ile bulur, uzaktaki updated_at daha yeni ise yerelde g�nceller.
        // ��lemler arka planda yap�l�r, UI kilitlenmez.
        public void HepsiniDinle(Action<object?>? onChanged = null)
        {
            if (_recordsListener != null) return; // zaten dinleniyor

            _ = Task.Run(async () =>
            {
                try
                {
                    if (_db == null)
                    {
                        await Baglan().ConfigureAwait(false);
                    }
                    if (_db == null) return;

                    var col = _db.Collection("records");
                    _recordsListener = col.Listen(snapshot =>
                    {
                        SyncStatusHub.Set("Bulut: Dinleniyor");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                foreach (var change in snapshot.Changes)
                                {
                                    var doc = change.Document;
                                    if (doc == null || !doc.Exists) continue;

                                    var rid = doc.Id;
                                    long remoteUpdated = 0;
                                    if (!doc.TryGetValue("updated_at", out remoteUpdated))
                                        remoteUpdated = 0;

                                    // Yereldeki updated_at oku
                                    int localId = 0;
                                    long localUpdated = 0;
                                    await using (var conn = new SqliteConnection(DatabaseService.ConnectionString))
                                    {
                                        await conn.OpenAsync().ConfigureAwait(false);
                                        await using (var cmd = conn.CreateCommand())
                                        {
                                            cmd.CommandText = "SELECT id, IFNULL(updated_at,0) FROM Records WHERE remote_id=@rid LIMIT 1";
                                            cmd.Parameters.AddWithValue("@rid", rid);
                                            await using var rdr = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                                            if (await rdr.ReadAsync().ConfigureAwait(false))
                                            {
                                                localId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                                                localUpdated = rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1);
                                            }
                                        }

                                        if (localId != 0 && remoteUpdated > localUpdated)
                                        {
                                            // Uzak alanlar� oku
                                            string? containerNo = doc.ContainsField("containerNo") ? doc.GetValue<string>("containerNo") : null;
                                            string? loadLocation = doc.ContainsField("loadLocation") ? doc.GetValue<string>("loadLocation") : null;
                                            string? unloadLocation = doc.ContainsField("unloadLocation") ? doc.GetValue<string>("unloadLocation") : null;
                                            string? size = doc.ContainsField("size") ? doc.GetValue<string>("size") : null;
                                            string? status = doc.ContainsField("status") ? doc.GetValue<string>("status") : null;
                                            string? nightOrDay = doc.ContainsField("nightOrDay") ? doc.GetValue<string>("nightOrDay") : null;
                                            string? truckPlate = doc.ContainsField("truckPlate") ? doc.GetValue<string>("truckPlate") : null;
                                            string? notes = doc.ContainsField("notes") ? doc.GetValue<string>("notes") : null;
                                            string? createdByUserId = doc.ContainsField("createdByUserId") ? doc.GetValue<string>("createdByUserId") : null;
                                            long createdAt = doc.ContainsField("createdAt") ? doc.GetValue<long>("createdAt") : 0;
                                            bool deleted = doc.ContainsField("deleted") && doc.GetValue<bool>("deleted");

                                            await using var upd = conn.CreateCommand();
                                            upd.CommandText = @"UPDATE Records SET
                                                                updated_at=@updated_at,
                                                                is_dirty=0,
                                                                deleted=@deleted,
                                                                containerNo=@containerNo,
                                                                loadLocation=@loadLocation,
                                                                unloadLocation=@unloadLocation,
                                                                size=@size,
                                                                status=@status,
                                                                nightOrDay=@nightOrDay,
                                                                truckPlate=@truckPlate,
                                                                notes=@notes,
                                                                createdByUserId=@createdByUserId,
                                                                createdAt=@createdAt
                                                              WHERE id=@id";
                                            upd.Parameters.AddWithValue("@updated_at", remoteUpdated);
                                            upd.Parameters.AddWithValue("@deleted", deleted ? 1 : 0);
                                            upd.Parameters.AddWithValue("@containerNo", (object?)containerNo ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@loadLocation", (object?)loadLocation ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@unloadLocation", (object?)unloadLocation ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@size", (object?)size ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@status", (object?)status ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@nightOrDay", (object?)nightOrDay ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@truckPlate", (object?)truckPlate ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@createdByUserId", (object?)createdByUserId ?? DBNull.Value);
                                            upd.Parameters.AddWithValue("@createdAt", createdAt);
                                            upd.Parameters.AddWithValue("@id", localId);
                                            await upd.ExecuteNonQueryAsync().ConfigureAwait(false);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[HepsiniDinle] Snapshot i�leme hatas�: {ex.Message}");
                                SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                            }
                            finally
                            {
                                try { onChanged?.Invoke(null); } catch { }
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HepsiniDinle] Dinleme ba�lat�lamad�: {ex.Message}");
                    SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                }
            });
        }

        // Dinlemeyi durdur (uygulama kapan���nda)
        public async Task DinlemeyiDurdurAsync()
        {
            try
            {
                if (_recordsListener != null)
                {
                    _recordsListener.StopAsync().GetAwaiter().GetResult();
                    _recordsListener = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Firestore] Dinleme durdurma hatas�: {ex.Message}");
            }
            SyncStatusHub.Set("Kapal�");
            await Task.CompletedTask;
        }
    }
}
