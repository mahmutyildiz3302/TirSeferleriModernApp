using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using TirSeferleriModernApp.Models;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using Grpc.Core; // RpcException ve StatusCode i�in

namespace TirSeferleriModernApp.Services
{
    public class FirestoreServisi
    {
        private FirestoreDb? _db;
        public FirestoreDb? Db => _db;
        private FirestoreChangeListener? _recordsListener;
        private CancellationTokenSource? _listenCts;
        private Task? _listenTask;

        // Firestore dinleyicisinden yerel veriye gelen yans�malar� bildirmek i�in olay
        // Parametre: etkilenen yerel Records.id (SeferId ile e�lenir)
        public static event Action<int>? RecordChangedFromFirestore;

        private static bool LooksLikePlaceholder(string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId)) return true;
            var p = projectId.Trim().ToLowerInvariant();
            return p.Contains("your-") || p.Contains("project") || p.Contains("<") || p.Contains(">");
        }

        // Basit transient kontrol�
        private static bool IsTransient(StatusCode code)
            => code == StatusCode.Unavailable || code == StatusCode.DeadlineExceeded;

        // AppSettings.json'dan proje kimli�i ve kimlik bilgisi yolu okunur ve Firestore'a ba�lan�l�r.
        // Ba�lant� kurulamazsa anla��l�r bir hata mesaj� ile istisna f�rlat�lmaz; durum hub ve log ile bildirilir.
        public async Task Baglan(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var settings = AppSettingsHelper.Current;
            var projectId = settings.FirebaseProjectId?.Trim();
            var credPath = settings.GoogleApplicationCredentialsPath?.Trim();

            if (LooksLikePlaceholder(projectId))
            {
                SyncStatusHub.Set("Bulut: Hata (Ge�ersiz ProjectId)");
                LogService.Warn("FirebaseProjectId placeholder gibi g�r�n�yor. AppSettings.json i�inde ger�ek proje ID'sini yaz�n.");
                return;
            }

            if (string.IsNullOrWhiteSpace(projectId))
            {
                SyncStatusHub.Set("Bulut: Hata (ProjectId yok)");
                LogService.Warn("Firestore ba�lant�s� i�in AppSettings.json i�inde 'FirebaseProjectId' de�eri gereklidir.");
                return;
            }

            if (string.IsNullOrWhiteSpace(credPath))
            {
                SyncStatusHub.Set("Bulut: Hata (Kimlik dosyas� yolu yok)");
                LogService.Warn("Firestore ba�lant�s� i�in AppSettingsHelper.Current i�inde 'GoogleApplicationCredentialsPath' de�eri gereklidir.");
                return;
            }

            if (!File.Exists(credPath))
            {
                SyncStatusHub.Set("Bulut: Hata (Kimlik dosyas� bulunamad�)");
                LogService.Error($"Google kimlik bilgisi dosyas� bulunamad�: {credPath}");
                return;
            }

            var currentEnv = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(currentEnv) || !string.Equals(currentEnv, credPath, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
            }

            int attempt = 0;
            var delays = new[] { 500, 1000, 2000 }; // ms

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    LogService.Info("Firestore ba�lant�s� kuruluyor...");
                    _db = await FirestoreDb.CreateAsync(projectId);
                    SyncStatusHub.Set("Bulut: Ba�land�");
                    LogService.Info("Firestore ba�lant�s� kuruldu.");
                    return;
                }
                catch (RpcException rex)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (rex.StatusCode == StatusCode.PermissionDenied)
                    {
                        SyncStatusHub.Set("Bulut: Hata (API etkin de�il veya izin yok)");
                        LogService.Error("Firestore PermissionDenied: Cloud Firestore API etkin de�il veya servis hesab�n�n izni yok.");
                        return; // tekrar deneme
                    }

                    if (IsTransient(rex.StatusCode) && attempt < delays.Length)
                    {
                        var wait = delays[attempt++];
                        SyncStatusHub.Set($"Bulut: Ge�ici hata, tekrar denenecek ({rex.StatusCode})");
                        try { await Task.Delay(wait, cancellationToken).ConfigureAwait(false); } catch { }
                        continue;
                    }

                    SyncStatusHub.Set($"Bulut: Hata ({rex.Status.Detail ?? rex.StatusCode.ToString()})");
                    LogService.Error("Firestore'a ba�lan�lamad� (RpcException). �pucu: API, a� ve rol yetkilerini kontrol edin.", rex);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // sessizce ��k
                    return;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    SyncStatusHub.Set("Bulut: Hata");
                    LogService.Error("Firestore'a ba�lan�lamad�. �pucu: FIRESTORE_SETUP.md ad�mlar�n� do�rulay�n (API etkin, rol, JSON yolu, ProjectId)", ex);
                    return;
                }
            }
        }

        // Bulutta yeni belge olu�turur veya mevcut belgeyi g�nceller.
        // Ba�ar�l�ysa belge ID'sini, hata olursa k�sa bir mesaj d�nd�r�r.
        public async Task<string> BulutaYazOrGuncelle(Record r, CancellationToken cancellationToken = default)
        {
            try
            {
                if (r == null) return "Ge�ersiz veri";

                if (_db == null)
                {
                    await Baglan(cancellationToken).ConfigureAwait(false);
                }
                if (_db == null) return "Hata: Firestore API etkin de�il veya izin yok (ba�lant� kurulamad�)";

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

                // basit retry sadece transient hatalarda
                async Task<string> WriteAsync()
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(r.remote_id))
                    {
                        var added = await collection.AddAsync(data);
                        var newId = added.Id;
                        r.remote_id = newId;
                        LogService.Info($"Firestore'a yeni belge yaz�ld�. remote_id={newId}, local_id={r.id}");
                        return newId;
                    }
                    else
                    {
                        var doc = collection.Document(r.remote_id);
                        await doc.SetAsync(data, SetOptions.MergeAll);
                        LogService.Info($"Firestore belgesi g�ncellendi. remote_id={r.remote_id}, local_id={r.id}");
                        return r.remote_id;
                    }
                }

                int attempt = 0; var delays = new[] { 500, 1000, 2000 };
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { return await WriteAsync(); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return ""; // sessiz ��k��
                    }
                    catch (RpcException rex)
                    {
                        if (rex.StatusCode == StatusCode.PermissionDenied)
                        {
                            SyncStatusHub.Set("Bulut: Hata (API etkin de�il veya izin yok)");
                            LogService.Error("Firestore yazma PermissionDenied. API etkin de�il veya yetki yok.", rex);
                            return $"Hata: PermissionDenied ({rex.Status.Detail})";
                        }
                        if (IsTransient(rex.StatusCode) && attempt < delays.Length)
                        {
                            var wait = delays[attempt++];
                            SyncStatusHub.Set($"Bulut: Ge�ici hata, tekrar denenecek ({rex.StatusCode})");
                            try { await Task.Delay(wait, cancellationToken).ConfigureAwait(false); } catch { return ""; }
                            continue;
                        }
                        SyncStatusHub.Set($"Bulut: Hata ({rex.Status.Detail ?? rex.StatusCode.ToString()})");
                        LogService.Error("Buluta yazma/g�ncelleme hatas� (RpcException).", rex);
                        return $"Hata: {rex.StatusCode}";
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ""; // sessizce
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) return "";
                SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                LogService.Error("Buluta yazma/g�ncelleme hatas�. �pucu: A� ba�lant�s�n� ve IAM rol�n� kontrol edin.", ex);
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
        public void HepsiniDinle(CancellationToken cancellationToken = default, Action<object?>? onChanged = null)
        {
            if (_recordsListener != null || _listenTask != null) return; // zaten dinleniyor

            _listenCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _listenCts.Token;

            _listenTask = Task.Run(async () =>
            {
                try
                {
                    if (_db == null)
                    {
                        await Baglan(ct).ConfigureAwait(false);
                    }
                    if (_db == null || ct.IsCancellationRequested) return;

                    var col = _db.Collection("records");
                    _recordsListener = col.Listen(snapshot =>
                    {
                        if (ct.IsCancellationRequested) return;
                        SyncStatusHub.Set("Bulut: Dinleniyor");
                        LogService.Info("Firestore dinleyici snapshot ald�.");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                foreach (var change in snapshot.Changes)
                                {
                                    if (ct.IsCancellationRequested) break;
                                    var doc = change.Document;
                                    if (doc == null || !doc.Exists) continue;

                                    var rid = doc.Id;
                                    long remoteUpdated = 0;
                                    if (!doc.TryGetValue("updated_at", out remoteUpdated))
                                        remoteUpdated = 0;

                                    // �nce remote_id -> local e�le�meyi dene
                                    int localId = 0;
                                    long localUpdated = 0;
                                    await using (var conn = new SqliteConnection(DatabaseService.ConnectionString))
                                    {
                                        await conn.OpenAsync(ct).ConfigureAwait(false);

                                        // 1) remote_id ile e�le�en localId var m�?
                                        await using (var cmd = conn.CreateCommand())
                                        {
                                            cmd.CommandText = "SELECT id, IFNULL(updated_at,0) FROM Records WHERE remote_id=@rid LIMIT 1";
                                            cmd.Parameters.AddWithValue("@rid", rid);
                                            await using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                                            if (await rdr.ReadAsync(ct).ConfigureAwait(false))
                                            {
                                                localId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                                                localUpdated = rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1);
                                            }
                                        }

                                        // 2) Bulunamad�ysa, dok�mandaki 'id' alan�na g�re e�le ve remote_id'yi yerelde yaz
                                        if (localId == 0)
                                        {
                                            int docLocalId = 0;
                                            try
                                            {
                                                // 'id' field number -> int
                                                if (doc.ContainsField("id"))
                                                {
                                                    var boxed = doc.GetValue<object>("id");
                                                    if (boxed is long l) docLocalId = (int)l;
                                                    else if (boxed is int i) docLocalId = i;
                                                }
                                            }
                                            catch { /* ignore conversion issues */ }

                                            if (docLocalId > 0)
                                            {
                                                await using var map = conn.CreateCommand();
                                                map.CommandText = "UPDATE Records SET remote_id=@rid WHERE id=@id AND IFNULL(remote_id,'')=''";
                                                map.Parameters.AddWithValue("@rid", rid);
                                                map.Parameters.AddWithValue("@id", docLocalId);
                                                var affected = await map.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                                                if (affected > 0)
                                                {
                                                    localId = docLocalId; // e�le�tirme sa�land�
                                                    // updated_at mevcut ise oku
                                                    await using var readUpd = conn.CreateCommand();
                                                    readUpd.CommandText = "SELECT IFNULL(updated_at,0) FROM Records WHERE id=@id";
                                                    readUpd.Parameters.AddWithValue("@id", docLocalId);
                                                    var val = await readUpd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                                                    localUpdated = val == null || val == DBNull.Value ? 0 : Convert.ToInt64(val);
                                                }
                                            }
                                        }

                                        // 3) Uzak daha yeni ise veriyi �ekip yereli g�ncelle
                                        if (localId != 0 && remoteUpdated > localUpdated)
                                        {
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
                                            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                                            LogService.Info($"Yerel kay�t g�ncellendi. local_id={localId}, remote_id={rid}");
                                        }

                                        // 4) UI taraf�: localId e�le�tiyse bildir (remote==local olsa bile)
                                        if (localId != 0)
                                        {
                                            try { RecordChangedFromFirestore?.Invoke(localId); } catch { }
                                        }
                                    }
                                }
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested)
                            {
                                // sessiz ��k
                            }
                            catch (Exception ex)
                            {
                                LogService.Error("Snapshot i�leme hatas�. �pucu: �ema alan adlar� uyumlu mu?", ex);
                                SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                            }
                            finally
                            {
                                try { onChanged?.Invoke(null); } catch { }
                            }
                        }, ct);
                    });
                    LogService.Info("Firestore dinleyici ba�lat�ld�.");

                    // Dinleme iptal olana dek bekle
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            await Task.Delay(200, ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // beklenen iptal
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // sessiz ��k
                }
                catch (RpcException rex)
                {
                    if (rex.StatusCode == StatusCode.PermissionDenied)
                    {
                        SyncStatusHub.Set("Bulut: Hata (API etkin de�il veya izin yok)");
                        LogService.Error("Dinleme ba�lat�lamad�: PermissionDenied (API devre d��� veya rol eksik)", rex);
                    }
                    else if (IsTransient(rex.StatusCode))
                    {
                        SyncStatusHub.Set($"Bulut: Ge�ici hata ({rex.StatusCode})");
                        LogService.Error("Dinleme ba�lat�lamad� (transient).", rex);
                    }
                    else
                    {
                        SyncStatusHub.Set($"Bulut: Hata ({rex.Status.Detail ?? rex.StatusCode.ToString()})");
                        LogService.Error("Dinleme ba�lat�lamad�. �pucu: Firestore ba�lant�s�n� ve yetkileri kontrol edin.", rex);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("Dinleme ba�lat�lamad�. �pucu: Firestore ba�lant�s�n� ve yetkileri kontrol edin.", ex);
                    SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                }
                finally
                {
                    // listener objesini burada kapatmaya �al��may�n, DinlemeyiDurdurAsync yapacak
                }
            }, ct);
        }

        // Dinlemeyi durdur (uygulama kapan���nda)
        public async Task DinlemeyiDurdurAsync()
        {
            try
            {
                if (_listenCts != null && !_listenCts.IsCancellationRequested)
                {
                    _listenCts.Cancel();
                }

                if (_recordsListener != null)
                {
                    LogService.Info("Firestore dinleyici durduruluyor...");
                    try { _recordsListener.StopAsync().GetAwaiter().GetResult(); } catch { }
                    _recordsListener = null;
                    LogService.Info("Firestore dinleyici durduruldu.");
                }

                if (_listenTask != null)
                {
                    try { await _listenTask.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                    // TaskCanceledException, OperationCanceledException'dan t�redi�i i�in ayr� catch gerekmez
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Dinleme durdurma hatas�", ex);
            }
            finally
            {
                try { _listenCts?.Dispose(); } catch { }
                _listenCts = null;
                _listenTask = null;
            }
            SyncStatusHub.Set("Kapal�");
            await Task.CompletedTask;
        }
    }
}
