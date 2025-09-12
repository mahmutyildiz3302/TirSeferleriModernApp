using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Sync
{
    public sealed class SyncAgent : IDisposable
    {
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private readonly FirestoreServisi _firestore = new();
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;

        public void Start()
        {
            if (IsRunning) return;
            // Uygulama seviyesindeki token'a ba�l� bir CTS olu�tur (ek iptaller i�in)
            _cts = CancellationTokenSource.CreateLinkedTokenSource(App.AppCts.Token);
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
            SyncStatusHub.Set("Senkron: �al���yor");
            LogService.Info("SyncAgent ba�lat�ld�.");
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { if (_loopTask != null) await _loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested || App.AppCts.IsCancellationRequested)
            {
                // sessizce yut
                Debug.WriteLine("[SyncAgent] StopAsync -> OperationCanceled (expected)");
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[SyncAgent] StopAsync -> TaskCanceled (expected)");
            }
            catch (Exception ex)
            {
                LogService.Error("SyncAgent StopAsync bekleme hatas�", ex);
            }
            finally { _loopTask = null; _cts.Dispose(); _cts = null; }
            SyncStatusHub.Set("Kapal�");
            LogService.Info("SyncAgent durduruldu.");
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(_interval);
            using var reg = token.Register(() => { try { timer.Dispose(); } catch { } });

            while (true)
            {
                try
                {
                    await SyncOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    Debug.WriteLine("[SyncAgent] RunLoop -> canceled");
                    break;
                }
                catch (TaskCanceledException) when (token.IsCancellationRequested)
                {
                    Debug.WriteLine("[SyncAgent] RunLoop -> task canceled");
                    break;
                }
                catch (Exception ex)
                {
                    LogService.Error("SyncAgent d�ng� hatas�. �pucu: A� ba�lant�s�n� ve Firestore yap�land�rmas�n� kontrol edin.", ex);
                    SyncStatusHub.Set($"Senkron: Hata ({ex.Message})");
                }

                // Token ge�meden bekle; iptal edildi�inde reg->Dispose �a�r�s� false d�nd�r�r
                try
                {
                    if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                        break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SyncOnceAsync(CancellationToken token)
        {
            // Firestore'a ba�l� de�ilse ba�lanmay� dene (her denemede de�il, sadece ihtiya� oldu�unda)
            if (_firestore.Db == null)
            {
                try { await _firestore.Baglan(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogService.Error("Firestore ba�lant� hatas�. �pucu: AppSettings.json ve FIRESTORE_SETUP.md ad�mlar�n� kontrol edin.", ex);
                    SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                    return; // Ba�lant� yoksa bu turu pas ge�
                }
            }

            var dirtyList = await GetDirtyRecordsAsync(token).ConfigureAwait(false);
            LogService.Info($"SyncAgent: {dirtyList.Count} kirli kay�t bulundu.");
            foreach (var rec in dirtyList)
            {
                if (token.IsCancellationRequested) break;

                // Buluta yaz veya g�ncelle
                var result = await _firestore.BulutaYazOrGuncelle(rec, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) break;

                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Hata", StringComparison.OrdinalIgnoreCase))
                {
                    // Ba�ar�l� - yerelde remote_id'yi yaz ve is_dirty=0 yap
                    await MarkRecordSyncedAsync(rec.id, result, token).ConfigureAwait(false);
                    SyncStatusHub.Set("Senkron: G�ncel");
                    LogService.Info($"SyncAgent: local_id={rec.id} senkron edildi, remote_id={result}");
                }
                else if (!token.IsCancellationRequested)
                {
                    LogService.Error($"Senkronizasyon ba�ar�s�z (id={rec.id}) - {result}");
                    SyncStatusHub.Set($"Senkron: Hata ({result})");
                }
            }
        }

        private static async Task<List<Record>> GetDirtyRecordsAsync(CancellationToken token)
        {
            var list = new List<Record>();
            try
            {
                await using var conn = new SqliteConnection(DatabaseService.ConnectionString);
                await conn.OpenAsync(token).ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT id, remote_id, updated_at, IFNULL(is_dirty,0), IFNULL(deleted,0),
                                           containerNo, loadLocation, unloadLocation, size, status,
                                           nightOrDay, truckPlate, notes, createdByUserId, createdAt
                                    FROM Records
                                    WHERE IFNULL(is_dirty,0)=1
                                    ORDER BY updated_at ASC";
                await using var rdr = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await rdr.ReadAsync(token).ConfigureAwait(false))
                {
                    var r = new Record
                    {
                        id = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        remote_id = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                        updated_at = rdr.IsDBNull(2) ? 0 : rdr.GetInt64(2),
                        is_dirty = !rdr.IsDBNull(3) && (rdr.GetInt64(3) != 0),
                        deleted = !rdr.IsDBNull(4) && (rdr.GetInt64(4) != 0),
                        containerNo = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                        loadLocation = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                        unloadLocation = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                        size = rdr.IsDBNull(8) ? null : rdr.GetString(8),
                        status = rdr.IsDBNull(9) ? null : rdr.GetString(9),
                        nightOrDay = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                        truckPlate = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                        notes = rdr.IsDBNull(12) ? null : rdr.GetString(12),
                        createdByUserId = rdr.IsDBNull(13) ? null : rdr.GetString(13),
                        createdAt = rdr.IsDBNull(14) ? 0 : rdr.GetInt64(14)
                    };
                    list.Add(r);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // sessizce
            }
            catch (TaskCanceledException)
            {
                // sessizce
            }
            catch (Exception ex)
            {
                LogService.Error("Kirli kay�tlar okunamad�. �pucu: Records �emas�n� kontrol edin.", ex);
                SyncStatusHub.Set($"Senkron: Hata ({ex.Message})");
            }
            return list;
        }

        private static async Task MarkRecordSyncedAsync(int id, string remoteId, CancellationToken token)
        {
            try
            {
                await using var conn = new SqliteConnection(DatabaseService.ConnectionString);
                await conn.OpenAsync(token).ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE Records
                                    SET remote_id = @rid,
                                        is_dirty = 0
                                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@rid", (object?)remoteId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // sessizce
            }
            catch (TaskCanceledException)
            {
                // sessizce
            }
            catch (Exception ex)
            {
                LogService.Error($"Yerel kay�t g�ncellenemedi (id={id}). �pucu: DB dosyas� eri�imi veya �ema.", ex);
                SyncStatusHub.Set($"Senkron: Hata ({ex.Message})");
            }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
        }
    }
}
