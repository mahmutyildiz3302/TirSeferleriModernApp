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

                    // Doðrulama ve yönlendirici loglar
                    if (string.IsNullOrWhiteSpace(settings.FirebaseProjectId))
                    {
                        LogService.Warn("FirebaseProjectId boþ. Ýpucu: AppSettings.json içine proje kimliðini yazýn (Firebase/GCP Project ID). FIRESTORE_SETUP.md'ye bakýn.");
                    }
                    else
                    {
                        LogService.Info($"FirebaseProjectId okundu: {settings.FirebaseProjectId}");
                    }

                    if (string.IsNullOrWhiteSpace(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn("GoogleApplicationCredentialsPath boþ. Ýpucu: Hizmet hesabý JSON anahtarýný indirin ve tam yolu AppSettings.json'a yazýn. FIRESTORE_SETUP.md'ye bakýn.");
                    }
                    else if (!File.Exists(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn($"Hizmet hesabý JSON dosyasý bulunamadý: {settings.GoogleApplicationCredentialsPath}. Ýpucu: Dosya yolunu doðrulayýn veya yeni JSON anahtar oluþturun (Service Accounts > Keys).");
                    }
                    else
                    {
                        // Firestore için kimlik bilgisi yolu ortam deðiþkenine aktarýlýr
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", settings.GoogleApplicationCredentialsPath);
                        LogService.Info("Google kimlik bilgisi dosyasý bulundu ve ortam deðiþkeni ayarlandý.");
                    }

                    return settings;
                }

                LogService.Warn("AppSettings.json bulunamadý. Ýpucu: Proje köküne AppSettings.json ekleyin. Örnek için FIRESTORE_SETUP.md'ye bakýn.");
            }
            catch (Exception ex)
            {
                LogService.Error("AppSettings okunamadý", ex);
            }

            return new AppSettings();
        }
    }
}
