using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TirSeferleriModernApp.Services
{
    public class AppSettings
    {
        public string? FirebaseProjectId { get; set; }
        public string? GoogleApplicationCredentialsPath { get; set; }
    }

    public static class AppSettingsHelper
    {
        private static readonly Lazy<AppSettings> _current = new(() => Load());
        public static AppSettings Current => _current.Value;

        private static AppSettings Load()
        {
            try
            {
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppSettings.json");
                if (File.Exists(localPath))
                {
                    LogService.Info("AppSettings.json okunuyor...");
                    var json = File.ReadAllText(localPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();

                    // Do�rulama ve y�nlendirici loglar
                    if (string.IsNullOrWhiteSpace(settings.FirebaseProjectId))
                    {
                        LogService.Warn("FirebaseProjectId bo�. �pucu: AppSettings.json i�ine proje kimli�ini yaz�n (Firebase/GCP Project ID). FIRESTORE_SETUP.md'ye bak�n.");
                    }
                    else
                    {
                        LogService.Info($"FirebaseProjectId okundu: {settings.FirebaseProjectId}");
                    }

                    if (string.IsNullOrWhiteSpace(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn("GoogleApplicationCredentialsPath bo�. �pucu: Hizmet hesab� JSON anahtar�n� indirin ve tam yolu AppSettings.json'a yaz�n. FIRESTORE_SETUP.md'ye bak�n.");
                    }
                    else if (!File.Exists(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn($"Hizmet hesab� JSON dosyas� bulunamad�: {settings.GoogleApplicationCredentialsPath}. �pucu: Dosya yolunu do�rulay�n veya yeni JSON anahtar olu�turun (Service Accounts > Keys).");
                    }
                    else
                    {
                        // Firestore i�in kimlik bilgisi yolu ortam de�i�kenine aktar�l�r
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", settings.GoogleApplicationCredentialsPath);
                        LogService.Info("Google kimlik bilgisi dosyas� bulundu ve ortam de�i�keni ayarland�.");
                    }

                    return settings;
                }

                LogService.Warn("AppSettings.json bulunamad�. �pucu: Proje k�k�ne AppSettings.json ekleyin. �rnek i�in FIRESTORE_SETUP.md'ye bak�n.");
            }
            catch (Exception ex)
            {
                LogService.Error("AppSettings okunamad�", ex);
            }

            return new AppSettings();
        }
    }
}
