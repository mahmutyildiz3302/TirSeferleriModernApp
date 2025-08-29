using System.Configuration;
using System.Data;
using System.Diagnostics;
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

            // Log servisinin başlatılması (Debug ve Trace yakalanır)
            LogService.Initialize();

            // Veritabanı ve tablolar uygulama açılışında kontrol edilir/oluşturulur
            try
            {
                DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
                DatabaseService.CheckAndCreateOrUpdateRecordsTable();
                Debug.WriteLine("[App] DB tabloları kontrol edildi.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[App] DB init hata: {ex.Message}");
            }

            // Senkron ajanını ve Firestore dinleyicisini başlat
            try
            {
                _syncAgent.Start();
                Debug.WriteLine("[App] SyncAgent başlatıldı.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[App] SyncAgent başlatılamadı: {ex.Message}");
            }

            try
            {
                _firestore.HepsiniDinle();
                Debug.WriteLine("[App] Firestore dinleyici başlatıldı.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[App] Firestore dinleyici hatası: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // Arka plan ajanlarını güvenle durdur
            try
            {
                Debug.WriteLine("[App] SyncAgent durduruluyor...");
                _syncAgent.StopAsync().GetAwaiter().GetResult();
                Debug.WriteLine("[App] SyncAgent durduruldu.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[App] SyncAgent durdurma hatası: {ex.Message}");
            }

            try
            {
                Debug.WriteLine("[App] Firestore dinleyici durduruluyor...");
                _firestore.DinlemeyiDurdurAsync().GetAwaiter().GetResult();
                Debug.WriteLine("[App] Firestore dinleyici durduruldu.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[App] Firestore dinleyici durdurma hatası: {ex.Message}");
            }
        }
    }
}

