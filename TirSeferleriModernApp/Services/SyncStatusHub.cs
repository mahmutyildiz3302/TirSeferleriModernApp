using System;
using System.Diagnostics;

namespace TirSeferleriModernApp.Services
{
    // Basit bir durum yay�mc�s�. Global durum metni ve de�i�im olay� sa�lar.
    public static class SyncStatusHub
    {
        private static string _current = "Kapal�";
        public static string Current => _current;
        public static event Action<string>? StatusChanged;

        public static void Set(string status)
        {
            _current = status ?? "";
            try { StatusChanged?.Invoke(_current); } catch { }
            Debug.WriteLine($"[SyncStatus] {_current}");
        }

        public static IDisposable Subscribe(Action<string> handler)
        {
            StatusChanged += handler;
            // Abone olan tarafa mevcut durumu hemen bildir
            try { handler(_current); } catch { }
            return new Unsubscriber(() => { StatusChanged -= handler; });
        }

        private sealed class Unsubscriber(Action dispose) : IDisposable
        {
            private readonly Action _dispose = dispose;
            public void Dispose() { try { _dispose(); } catch { } }
        }
    }
}
