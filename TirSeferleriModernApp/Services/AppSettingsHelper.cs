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
                    var json = File.ReadAllText(localPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();

                    // Doðrulama ve loglama
                    if (string.IsNullOrWhiteSpace(settings.FirebaseProjectId))
                        Trace.WriteLine("[AppSettings] Uyarý: FirebaseProjectId boþ. Firestore eriþimi yapýlamaz.");
                    else
                        Trace.WriteLine($"[AppSettings] FirebaseProjectId okundu: {settings.FirebaseProjectId}");

                    if (string.IsNullOrWhiteSpace(settings.GoogleApplicationCredentialsPath))
                    {
                        Trace.WriteLine("[AppSettings] Uyarý: GoogleApplicationCredentialsPath boþ. Hizmet hesabý anahtar yolu gerekli.");
                    }
                    else if (!File.Exists(settings.GoogleApplicationCredentialsPath))
                    {
                        Trace.WriteLine($"[AppSettings] Uyarý: Hizmet hesabý JSON dosyasý bulunamadý: {settings.GoogleApplicationCredentialsPath}");
                    }
                    else
                    {
                        // Firestore için kimlik bilgisi yolu ortam deðiþkenine aktarýlýr
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", settings.GoogleApplicationCredentialsPath);
                        Trace.WriteLine("[AppSettings] Google kimlik bilgisi dosyasý bulundu ve ortam deðiþkeni ayarlandý.");
                    }

                    return settings;
                }

                Trace.WriteLine("[AppSettingsHelper] AppSettings.json bulunamadý.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AppSettingsHelper] Okuma hatasý: {ex.Message}");
            }

            return new AppSettings();
        }
    }
}
