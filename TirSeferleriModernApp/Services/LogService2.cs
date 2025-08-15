using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TirSeferleriModernApp.Services
{
    /// <summary>
    /// Trace/Debug ��kt�lar�n� yakalay�p UI'ya yay�nlayan ve opsiyonel olarak dosyaya yazan log servisi.
    /// </summary>
    public static class LogService
    {
        private static bool _initialized;
        private static int _maxItems = 2000;
        private static bool _writeFile;
        private static string? _filePath;
        private static readonly object _fileLock = new();

        public static ObservableCollection<string> Entries { get; } = [];

        /// <summary>
        /// Log servisini ba�lat�r. Trace dinleyicisi eklenir ve t�m mesajlar yakalan�r.
        /// </summary>
        /// <param name="alsoWriteToFile">Dosyaya da yaz�ls�n m�?</param>
        /// <param name="filePath">�zel dosya yolu. null ise logs/log_yyyyMMdd.txt kullan�l�r.</param>
        /// <param name="maxItems">Bellekte tutulacak maksimum sat�r say�s� (FIFO). null ise varsay�lan kal�r.</param>
        public static void Initialize(bool alsoWriteToFile = false, string? filePath = null, int? maxItems = null)
        {
            if (_initialized) return;
            _initialized = true;

            _writeFile = alsoWriteToFile;
            _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"log_{DateTime.Now:yyyyMMdd}.txt");
            if (_writeFile)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath!)!);
            }

            if (maxItems.HasValue && maxItems.Value > 0)
                _maxItems = maxItems.Value;

            var listener = new UiTraceListener(_writeFile, _filePath);
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;

            Trace.WriteLine("[LogService] Ba�lat�ld� ve Trace dinleyicisi eklendi.");
        }

        private class UiTraceListener(bool writeFile, string? filePath) : TraceListener
        {
            private readonly bool _writeFileLocal = writeFile;
            private readonly string? _filePathLocal = filePath;

            private void AddLine(string? message)
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
                try
                {
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (Entries.Count >= _maxItems)
                            {
                                Entries.RemoveAt(0);
                            }
                            Entries.Add(line);
                        }));
                    }
                    else
                    {
                        if (Entries.Count >= _maxItems)
                            Entries.RemoveAt(0);
                        Entries.Add(line);
                    }

                    if (_writeFileLocal && !string.IsNullOrWhiteSpace(_filePathLocal))
                    {
                        lock (_fileLock)
                        {
                            File.AppendAllText(_filePathLocal!, line + Environment.NewLine);
                        }
                    }
                }
                catch
                {
                    // yut
                }
            }

            public override void Write(string? message)
            {
                AddLine(message);
            }

            public override void WriteLine(string? message)
            {
                AddLine(message);
            }
        }
    }
}
