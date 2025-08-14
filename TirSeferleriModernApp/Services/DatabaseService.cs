using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using TirSeferleriModernApp.Models; // Sefer sınıfının bulunduğu namespace'i ekleyin

namespace TirSeferleriModernApp.Services
{
    public class DatabaseService
    {
        private static readonly string DbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TirSeferleri.db");
        public static readonly string ConnectionString = $"Data Source={DbFile}";
        private readonly string _dbFile;
        private readonly string _instanceConnectionString;
        private static readonly List<Sefer> SeferListesi = [];        // SeferListesi'ni tanımlar

        private static readonly ConcurrentQueue<string> MessageQueue = new(); // Bu satırı ekleyin

        public DatabaseService(string dbFile)
        {
            _dbFile = dbFile;
            _instanceConnectionString = $"Data Source={_dbFile}";
        }

        public void Initialize()
        {
            EnsureDatabaseFile();

            // Tabloları oluştur ve eksik kolonları kontrol et
            CreateOrUpdateTable("Seferler", @"
                CREATE TABLE IF NOT EXISTS Seferler (
                    SeferId INTEGER PRIMARY KEY AUTOINCREMENT,
                    KonteynerNo TEXT,
                    KonteynerBoyutu TEXT CHECK(KonteynerBoyutu IN ('20', '40')),
                    YuklemeYeri TEXT,
                    BosaltmaYeri TEXT,
                    Tarih TEXT,
                    Saat TEXT,
                    Fiyat REAL,
                    Aciklama TEXT,
                    CekiciId INTEGER,
                    CekiciPlaka TEXT,
                    DorseId INTEGER,
                    SoforId INTEGER,
                    SoforAdi TEXT
                );", [
                    "CekiciId INTEGER",
                    "CekiciPlaka TEXT",
                    "DorseId INTEGER",
                    "SoforId INTEGER",
                    "SoforAdi TEXT"
                ]);

            Debug.WriteLine("[DatabaseService.xaml.cs] Giderler tablosu kontrol ediliyor...");
            CreateOrUpdateTable("Giderler", @"
                CREATE TABLE IF NOT EXISTS Giderler (
                    GiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER NOT NULL,
                    Aciklama TEXT,
                    Tutar REAL NOT NULL,
                    Tarih TEXT NOT NULL
                );", [
                    "CekiciId INTEGER",
                    "Aciklama TEXT",
                    "Tutar REAL",
                    "Tarih TEXT"
                ]);

            CreateOrUpdateTable("KarHesap", @"
                CREATE TABLE IF NOT EXISTS KarHesap (
                    KarHesapId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER NOT NULL
                );", []);
        }

        private void EnsureDatabaseFile()
        {
            if (!File.Exists(_dbFile))
            {
                using var initialConnection = new SqliteConnection(_instanceConnectionString);
                initialConnection.Open();
                Debug.WriteLine("[DatabaseService.xaml.cs] Veritabanı dosyası oluşturuldu.");
            }
            else
            {
                Debug.WriteLine($"[DatabaseService.xaml.cs] Veritabanı dosyası zaten mevcut: {_dbFile}");
            }
        }

        private static void CreateOrUpdateTable(string tableName, string createScript, string[] columns)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            // Tabloyu oluştur
            ExecuteScript(connection, createScript);
            Debug.WriteLine($"[DatabaseService.xaml.cs] {tableName} tablosu kontrol edildi veya oluşturuldu.");

            // Eksik kolonları kontrol et ve ekle
            EnsureColumns(connection, tableName, columns);
        }

        private static void ExecuteScript(SqliteConnection connection, string script)
        {
            using var command = connection.CreateCommand();
            command.CommandText = script;
            command.ExecuteNonQuery();
        }

        private static void EnsureColumns(SqliteConnection connection, string tableName, string[] columns)
        {
            foreach (var column in columns)
            {
                string columnName = column.Split(' ')[0];
                var checkColumnCommand = connection.CreateCommand();
                checkColumnCommand.CommandText = $"PRAGMA table_info({tableName});";

                using var reader = checkColumnCommand.ExecuteReader();
                bool columnExists = false;

                while (reader.Read())
                {
                    if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }

                if (!columnExists)
                {
                    var addColumnCommand = connection.CreateCommand();
                    addColumnCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {column};";
                    addColumnCommand.ExecuteNonQuery();
                    Debug.WriteLine($"[DatabaseService.xaml.cs] Kolon eklendi: {column}");
                }
            }
        }

        public static void CheckAndCreateOrUpdateSeferlerTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();

                string createScript = @"
                    CREATE TABLE IF NOT EXISTS Seferler (
                        SeferId INTEGER PRIMARY KEY AUTOINCREMENT,
                        KonteynerNo TEXT,
                        KonteynerBoyutu TEXT CHECK(KonteynerBoyutu IN ('20', '40')),
                        YuklemeYeri TEXT,
                        BosaltmaYeri TEXT,
                        Tarih TEXT,
                        Saat TEXT,
                        Fiyat REAL,
                        Aciklama TEXT,
                        CekiciId INTEGER,
                        CekiciPlaka TEXT,
                        DorseId INTEGER,
                        SoforId INTEGER,
                        SoforAdi TEXT
                    );
                ";

                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "CekiciPlaka TEXT",
                    "DorseId INTEGER",
                    "SoforId INTEGER",
                    "SoforAdi TEXT"
                ];

                EnsureTable("Seferler", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService.xaml.cs] Hata: {ex.Message}");
            }
        }

        public static void EnsureDatabaseFileStatic()
        {
            if (!File.Exists(DbFile))
            {
                using var initialConnection = new SqliteConnection(ConnectionString);
                initialConnection.Open();
                Debug.WriteLine("[DatabaseService.xaml.cs] Veritabanı dosyası oluşturuldu.");
            }
            else
            {
                Debug.WriteLine($"[DatabaseService.xaml.cs] Veritabanı dosyası zaten mevcut: {DbFile}");
            }
        }

        public static void CheckAndCreateOrUpdateGiderlerTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();

                string createScript = @"
            CREATE TABLE IF NOT EXISTS Giderler (
                GiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                CekiciId INTEGER NOT NULL,
                Aciklama TEXT,
                Tutar REAL NOT NULL,
                Tarih TEXT NOT NULL
            );
            ";

                string[] requiredColumns = [
                "CekiciId INTEGER",
                "Aciklama TEXT",
                "Tutar REAL",
                "Tarih TEXT"
            ];

                Debug.WriteLine("[DatabaseService.xaml.cs] Giderler tablosu kontrol ediliyor...");
                EnsureTable("Giderler", createScript, requiredColumns);
                Debug.WriteLine("[DatabaseService.xaml.cs] Giderler tablosu kontrol edildi veya oluşturuldu.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService.xaml.cs] Hata: {ex.Message}");
            }
        }

        public static void CheckAndCreateOrUpdateKarHesapTablosu()
        {
            string createScript = @"
           CREATE TABLE IF NOT EXISTS KarHesap (
               KarHesapId INTEGER PRIMARY KEY AUTOINCREMENT,
               CekiciId INTEGER NOT NULL
           );
       ";

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = new SqliteCommand(createScript, connection);
            command.ExecuteNonQuery();
        }

        private static void EnsureTable(string tableName, string createScript, string[] columns)
        {
            using var connection = new SqliteConnection($"Data Source={DbFile}");
            connection.Open();

            // Tabloyu oluştur
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = createScript;
            createTableCommand.ExecuteNonQuery();
            Debug.WriteLine($"[DatabaseService.xaml.cs] {tableName} tablosu kontrol edildi veya oluşturuldu.");

            // Eksik kolonları kontrol et ve ekle
            foreach (var column in columns)
            {
                string columnName = column.Split(' ')[0];
                var checkColumnCommand = connection.CreateCommand();
                checkColumnCommand.CommandText = $"PRAGMA table_info({tableName});";

                using var reader = checkColumnCommand.ExecuteReader();
                bool columnExists = false;

                while (reader.Read())
                {
                    if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }

                if (!columnExists)
                {
                    var addColumnCommand = connection.CreateCommand();
                    addColumnCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {column};";
                    addColumnCommand.ExecuteNonQuery();
                    Debug.WriteLine($"[DatabaseService.xaml.cs] Kolon eklendi: {column}");
                }
            }
        }

        // >>>>>>>>>>>> BURASI STATIK <<<<<<<<<<<<<<
        public static List<Arac> GetAraclar()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT  C.Plaka, S.SoforAdi FROM Cekiciler as C
        INNER JOIN Soforler S ON S.SoforId = C.SoforId
        WHERE   C.Aktif = 1
        ORDER BY C.Plaka;";

            using var reader = command.ExecuteReader();
            var araclar = new List<Arac>();

            while (reader.Read())
            {
                var plaka = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var soforAdi = reader.IsDBNull(1) ? "" : reader.GetString(1);

                Debug.WriteLine($"[DatabaseServices.cs:305 - DB SQLite] Plaka: {plaka}, Şoför: {soforAdi}");

                araclar.Add(new Arac { Plaka = plaka, SoforAdi = soforAdi });
            }

            return araclar;
        }

        public static void SeferEkle(Sefer yeniSefer)
        {
            if (!ValidateSefer(yeniSefer)) return;

            SeferListesi.Add(yeniSefer);
            MessageQueue.Enqueue($"{yeniSefer.KonteynerNo} numaralı konteyner seferi başarıyla eklendi!");
        }

        public static void SeferGuncelle(Sefer guncellenecekSefer)
        {
            if (guncellenecekSefer == null) return;
            // Güncelleme SQL'i ileride eklenecek
        }

        public class Arac
        {
            public string? Plaka { get; set; }
            public string? SoforAdi { get; set; }
        }

        private static bool ValidateSefer(Sefer sefer)
        {
            // Örnek bir doğrulama işlemi
            return !string.IsNullOrEmpty(sefer.KonteynerNo) && sefer.Fiyat > 0;
        }
    }
}
