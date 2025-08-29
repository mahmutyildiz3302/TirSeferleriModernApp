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
                // �nce �al��ma dizinindeki AppSettings.json
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppSettings.json");
                if (File.Exists(localPath))
                {
                    var json = File.ReadAllText(localPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();

                    // Google uygulama kimlik bilgisi yolu �evre de�i�kenine aktar (Firestore i�in)
                    if (!string.IsNullOrWhiteSpace(settings.GoogleApplicationCredentialsPath) && File.Exists(settings.GoogleApplicationCredentialsPath))
                    {
                        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", settings.GoogleApplicationCredentialsPath);
                    }

                    return settings;
                }

                Trace.WriteLine("[AppSettingsHelper] AppSettings.json bulunamad�.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AppSettingsHelper] Okuma hatas�: {ex}");
            }

            return new AppSettings();
        }
    }
}
