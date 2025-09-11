using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Sync;

namespace TirSeferleriModernApp
{
    public partial class App : Application
    {
        private readonly SyncAgent _syncAgent = new();
        private readonly FirestoreServisi _firestore = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SyncStatusHub.Set("Kapalı");

            LogService.Initialize(alsoWriteToFile: true);
            LogService.Info("Uygulama başlıyor...");

            // AppSettings'i erken yükle ve doğrula (bu aynı zamanda log'a yazar)
            var settings = AppSettingsHelper.Current;

            // DB tabloları
            try
            {
                LogService.Info("Records tablosu kontrol/oluşturma başlıyor...");
                DatabaseService.CheckAndCreateOrUpdateRecordsTable();
                LogService.Info("Records tablosu kontrol/oluşturma tamamlandı.");

                DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
                LogService.Info("Seferler tablosu kontrol/oluşturma tamamlandı.");

                // Yeni: Eski Seferler'den Records'a eksik olanları taşı (is_dirty=1)
                var moved = DatabaseService.SeedRecordsFromSeferlerIfMissing();
                if (moved > 0)
                    LogService.Info($"Startup: {moved} kayıt senkron için işaretlendi.");
            }
            catch (System.Exception ex)
            {
                LogService.Error("DB init hata", ex);
            }

            // Senkron ve dinleyici
            try
            {
                _syncAgent.Start();
                LogService.Info("SyncAgent başlatıldı.");
                SyncStatusHub.Set("Senkron: Çalışıyor");
            }
            catch (System.Exception ex)
            {
                LogService.Error("SyncAgent başlatılamadı", ex);
                SyncStatusHub.Set("Senkron: Hata");
            }

            try
            {
                _firestore.HepsiniDinle();
                LogService.Info("Firestore dinleyici başlatıldı.");
                SyncStatusHub.Set("Bulut: Dinleniyor");
            }
            catch (System.Exception ex)
            {
                LogService.Error("Firestore dinleyici hatası", ex);
                SyncStatusHub.Set("Bulut: Hata");
            }

            // DEBUG mini doğrulama (hızlı test)
#if DEBUG
            _ = Task.Run(async () =>
            {
                // 1) Records tablosu var mı?
                var hasRecords = DatabaseService.RecordsTableExists();
                if (!hasRecords)
                {
                    LogService.Warn("MiniTest: Records tablosu yoktu, oluşturma çağrılıyor...");
                    DatabaseService.CheckAndCreateOrUpdateRecordsTable();
                    hasRecords = DatabaseService.RecordsTableExists();
                }
                LogService.Info($"MiniTest: Records tablosu mevcut mu? {(hasRecords ? "Evet" : "Hayır")}");

                // 2) SenkronDurumu 5 sn içinde ‘Dinleniyor/Bağlandı’ benzeri oldu mu?
                string[] okStates = ["Dinleniyor", "Bağlandı", "Çalışıyor", "Güncel"]; // içeriyorsa geçerli say
                var start = System.DateTime.UtcNow;
                bool stateOk = false;
                while ((System.DateTime.UtcNow - start).TotalSeconds < 5)
                {
                    var s = SyncStatusHub.Current ?? string.Empty;
                    if (okStates.Any(x => s.Contains(x))) { stateOk = true; break; }
                    await Task.Delay(300);
                }
                LogService.Info($"MiniTest: SenkronDurumu uygun mu? {(stateOk ? "Evet" : "Hayır")} | Durum='{SyncStatusHub.Current}'");

                // 3) AppSettings loglarında değerler yazıldı mı? (dolaylı kontrol)
                // Burada sadece mevcutluğunu ve yolun varlığını tekrar kontrol edip maskeleyerek bildiriyoruz
                var pid = settings.FirebaseProjectId;
                var cred = settings.GoogleApplicationCredentialsPath;
                var pidShown = string.IsNullOrWhiteSpace(pid) ? "(boş)" : pid;
                var credShown = string.IsNullOrWhiteSpace(cred) ? "(boş)" : MaskPath(cred);
                LogService.Info($"MiniTest: AppSettings -> ProjectId={pidShown}, CredPath={credShown}");

                // Uyarı: üç koşuldan herhangi biri başarısızsa
                if (!hasRecords || !stateOk || string.IsNullOrWhiteSpace(pid) || string.IsNullOrWhiteSpace(cred))
                {
                    LogService.Warn("MiniTest: Başarısız kontrol(ler) var. İpucu: FIRESTORE_SETUP.md ve AppSettings.json’u doğrulayın.");
                }
            });
#endif
        }

        private static string MaskPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                var file = System.IO.Path.GetFileName(path);
                var dir = System.IO.Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) return file;
                var parts = dir.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i < parts.Length - 2) parts[i] = new string('*', parts[i].Length);
                }
                var maskedDir = string.Join(System.IO.Path.DirectorySeparatorChar, parts);
                return System.IO.Path.Combine(maskedDir, file);
            }
            catch { return "***"; }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                LogService.Info("SyncAgent durduruluyor...");
                _syncAgent.StopAsync().GetAwaiter().GetResult();
                LogService.Info("SyncAgent durduruldu.");
            }
            catch (System.Exception ex)
            {
                LogService.Error("SyncAgent durdurma hatası", ex);
            }

            try
            {
                LogService.Info("Firestore dinleyici durduruluyor...");
                _firestore.DinlemeyiDurdurAsync().GetAwaiter().GetResult();
                LogService.Info("Firestore dinleyici durduruldu.");
            }
            catch (System.Exception ex)
            {
                LogService.Error("Firestore dinleyici durdurma hatası", ex);
            }

            SyncStatusHub.Set("Kapalı");
            LogService.Info("Uygulama kapanıyor.");
        }
    }
}

