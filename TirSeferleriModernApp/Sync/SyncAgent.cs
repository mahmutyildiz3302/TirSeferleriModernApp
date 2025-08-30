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
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
            SyncStatusHub.Set("Senkron: Çalýþýyor");
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { if (_loopTask != null) await _loopTask.ConfigureAwait(false); }
            catch { /* ignore */ }
            finally { _loopTask = null; _cts.Dispose(); _cts = null; }
            SyncStatusHub.Set("Kapalý");
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await SyncOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SyncAgent] Hata: {ex.Message}");
                    SyncStatusHub.Set($"Senkron: Hata ({ex.Message})");
                }
                try
                {
                    await Task.Delay(_interval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            }
        }

        private async Task SyncOnceAsync(CancellationToken token)
        {
            // Firestore'a baðlý deðilse baðlanmayý dene (her denemede deðil, sadece ihtiyaç olduðunda)
            if (_firestore.Db == null)
            {
                try { await _firestore.Baglan().ConfigureAwait(false); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SyncAgent] Firestore baðlantý hatasý: {ex.Message}");
                    SyncStatusHub.Set($"Bulut: Hata ({ex.Message})");
                    return; // Baðlantý yoksa bu turu pas geç
                }
            }

            var dirtyList = await GetDirtyRecordsAsync(token).ConfigureAwait(false);
            foreach (var rec in dirtyList)
            {
                if (token.IsCancellationRequested) break;

                // Buluta yaz veya güncelle
                var result = await _firestore.BulutaYazOrGuncelle(rec).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Hata", StringComparison.OrdinalIgnoreCase))
                {
                    // Baþarýlý - yerelde remote_id'yi yaz ve is_dirty=0 yap
                    await MarkRecordSyncedAsync(rec.id, result, token).ConfigureAwait(false);
                    SyncStatusHub.Set("Senkron: Güncel");
                }
                else
                {
                    Debug.WriteLine($"[SyncAgent] Senkronizasyon baþarýsýz (id={rec.id}): {result}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncAgent] Dirty kayýt okunamadý: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[SyncAgent] Yerel kayýt güncellenemedi (id={id}): {ex.Message}");
                SyncStatusHub.Set($"Senkron: Hata ({ex.Message})");
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
