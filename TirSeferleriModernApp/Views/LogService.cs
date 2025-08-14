using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TirSeferleriModernApp.Views
{
    public static class LogService
    {
        public static ObservableCollection<string> Entries { get; } = [];

        private static bool _initialized;
        private static readonly object _fileLock = new();

        public static void Initialize(bool alsoWriteToFile = true, string? filePath = null)
        {
            if (_initialized) return;
            var listener = new ObservableTraceListener(Entries, alsoWriteToFile, filePath);
            Trace.Listeners.Add(listener); // Use Trace instead of Debug
            _initialized = true;
        }

        private sealed class ObservableTraceListener : TraceListener
        {
            private readonly ObservableCollection<string> _entries;
            private readonly bool _writeFile;
            private readonly string _filePath;

            public ObservableTraceListener(ObservableCollection<string> entries, bool writeFile, string? filePath)
            {
                _entries = entries;
                _writeFile = writeFile;
                _filePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"log_{DateTime.Now:yyyyMMdd}.txt");
                if (_writeFile) Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            }

            public override void Write(string? message) => Append(message, newline: false);
            public override void WriteLine(string? message) => Append(message, newline: true);

            private void Append(string? message, bool newline)
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
                Application.Current?.Dispatcher?.Invoke(() => _entries.Add(line));

                if (_writeFile)
                {
                    lock (_fileLock)
                    {
                        File.AppendAllText(_filePath, line + (newline ? Environment.NewLine : ""));
                    }
                }
            }
        }
    }
}
