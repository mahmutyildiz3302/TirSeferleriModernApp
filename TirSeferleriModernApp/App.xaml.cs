using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
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

            // Varsayılan durum
            SyncStatusHub.Set("Kapalı");

            // Log servisinin başlatılması (Debug ve Trace yakalanır)
            LogService.Initialize(alsoWriteToFile: true);
            LogService.Info("Uygulama başlıyor...");

            // AppSettings'i erken yükle ve doğrula
            var settings = AppSettingsHelper.Current;

            // Veritabanı ve tablolar uygulama açılışında kontrol edilir/oluşturulur
            try
            {
                LogService.Info("Records tablosu kontrol/oluşturma başlıyor...");
                DatabaseService.CheckAndCreateOrUpdateRecordsTable();
                LogService.Info("Records tablosu kontrol/oluşturma tamamlandı.");

                DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
                LogService.Info("Seferler tablosu kontrol/oluşturma tamamlandı.");
            }
            catch (System.Exception ex)
            {
                LogService.Error("DB init hata", ex);
            }

            // Senkron ajanını ve Firestore dinleyicisini başlat (hata toleranslı)
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // Arka plan ajanlarını güvenle durdur
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

