using System;
using System.Net.Http;

namespace TirSeferleriModernApp.Services
{
    // Merkezi HttpClient havuzu. Gerektiðinde geniþletilebilir (handler ayarlarý vb.).
    public static class HttpClientProvider
    {
        private static readonly Lazy<HttpClient> _lazy = new(() => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(100)
        });

        public static HttpClient Client => _lazy.Value;

        public static void Dispose()
        {
            if (_lazy.IsValueCreated)
            {
                try { _lazy.Value.Dispose(); } catch { }
            }
        }
    }
}
