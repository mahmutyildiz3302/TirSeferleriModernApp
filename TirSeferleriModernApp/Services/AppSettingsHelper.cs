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

                    // Do�rulama ve loglama
                    if (string.IsNullOrWhiteSpace(settings.FirebaseProjectId))
                        LogService.Warn("FirebaseProjectId bo�. Firestore eri�imi yap�lamaz.");
                    else
                        LogService.Info($"FirebaseProjectId okundu: {settings.FirebaseProjectId}");

                    if (string.IsNullOrWhiteSpace(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn("GoogleApplicationCredentialsPath bo�. Hizmet hesab� anahtar yolu gerekli.");
                    }
                    else if (!File.Exists(settings.GoogleApplicationCredentialsPath))
                    {
                        LogService.Warn($"Hizmet hesab� JSON dosyas� bulunamad�: {settings.GoogleApplicationCredentialsPath}");
                    }
                    else
                    {
                        // Firestore i�in kimlik bilgisi yolu ortam de�i�kenine aktar�l�r
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", settings.GoogleApplicationCredentialsPath);
                        LogService.Info("Google kimlik bilgisi dosyas� bulundu ve ortam de�i�keni ayarland�.");
                    }

                    return settings;
                }

                LogService.Warn("AppSettings.json bulunamad�.");
            }
            catch (Exception ex)
            {
                LogService.Error("AppSettings okunamad�", ex);
            }

            return new AppSettings();
        }
    }
}
