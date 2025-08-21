using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using TirSeferleriModernApp.Models; // Sefer sınıfının bulunduğu namespace'i ekleyin
using static TirSeferleriModernApp.Views.VergilerAracView; // VergiArac modelini kullanmak için

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

            // Soforler tablosunda Arsivli kolonu olduğundan emin ol
            EnsureSoforlerArsivliColumn();
            EnsureCekicilerArsivliColumn();
            EnsureDorselerArsivliColumn();

            CheckAndCreateOrUpdateGenelGiderTablosu();
            CheckAndCreateOrUpdatePersonelGiderTablosu();
            CheckAndCreateOrUpdateVergiAracTablosu();
            CheckAndCreateOrUpdateDepoTablosu();
            CheckAndCreateOrUpdateGuzergahTablosu(); // Güzergahlar tablosunu kontrol et
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

        public static void EnsureSoforlerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Kolon var mı kontrol et
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Soforler);";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var colName = reader.GetString(1);
                        if (string.Equals(colName, "Arsivli", StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Soforler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                    Debug.WriteLine("[DatabaseService] Soforler.Arsivli kolonu eklendi.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] EnsureSoforlerArsivliColumn hata: {ex.Message}");
            }
        }

        public static void EnsureCekicilerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Cekiciler);";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), "Arsivli", StringComparison.OrdinalIgnoreCase))
                        { exists = true; break; }
                    }
                }
                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Cekiciler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] EnsureCekicilerArsivliColumn hata: {ex.Message}");
            }
        }

        public static void EnsureDorselerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(Dorseler);";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), "Arsivli", StringComparison.OrdinalIgnoreCase))
                        { exists = true; break; }
                    }
                }
                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Dorseler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] EnsureDorselerArsivliColumn hata: {ex.Message}");
            }
        }

        public static int GetSeferCountBySoforId(int soforId)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM Seferler WHERE SoforId = @sid";
                cmd.Parameters.AddWithValue("@sid", soforId);
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                return count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetSeferCountBySoforId hata: {ex.Message}");
                return 0;
            }
        }

        public static int GetSeferCountByCekiciId(int cekiciId)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM Seferler WHERE CekiciId = @id";
                cmd.Parameters.AddWithValue("@id", cekiciId);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetSeferCountByCekiciId hata: {ex.Message}");
                return 0;
            }
        }

        public static int GetSeferCountByDorseId(int dorseId)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM Seferler WHERE DorseId = @id";
                cmd.Parameters.AddWithValue("@id", dorseId);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetSeferCountByDorseId hata: {ex.Message}");
                return 0;
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
        WHERE   C.Aktif = 1 AND IFNULL(C.Arsivli,0)=0
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

        // Yeni: Sefer CRUD
        public static int SeferEkle(Sefer s)
        {
            if (!ValidateSefer(s)) return 0;
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO Seferler (KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Tarih, Saat, Fiyat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi)
                               VALUES (@KonteynerNo, @KonteynerBoyutu, @YuklemeYeri, @BosaltmaYeri, @Tarih, @Saat, @Fiyat, @Aciklama, @CekiciId, @CekiciPlaka, @DorseId, @SoforId, @SoforAdi);
                               SELECT last_insert_rowid();";
            BindSeferParams(cmd, s);
            var id = (long)cmd.ExecuteScalar()!;
            return (int)id;
        }

        public static void SeferGuncelle(Sefer s)
        {
            if (!ValidateSefer(s) || s.SeferId <= 0) return;
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"UPDATE Seferler SET
                                KonteynerNo=@KonteynerNo,
                                KonteynerBoyutu=@KonteynerBoyutu,
                                YuklemeYeri=@YuklemeYeri,
                                BosaltmaYeri=@BosaltmaYeri,
                                Tarih=@Tarih,
                                Saat=@Saat,
                                Fiyat=@Fiyat,
                                Aciklama=@Aciklama,
                                CekiciId=@CekiciId,
                                CekiciPlaka=@CekiciPlaka,
                                DorseId=@DorseId,
                                SoforId=@SoforId,
                                SoforAdi=@SoforAdi
                               WHERE SeferId=@SeferId";
            BindSeferParams(cmd, s);
            cmd.Parameters.AddWithValue("@SeferId", s.SeferId);
            cmd.ExecuteNonQuery();
        }

        public static List<Sefer> GetSeferler()
        {
            var result = new List<Sefer>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SeferId, KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Tarih, Saat, Fiyat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi FROM Seferler ORDER BY SeferId DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var s = new Sefer
                {
                    SeferId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    KonteynerNo = reader.IsDBNull(1) ? null : reader.GetString(1),
                    KonteynerBoyutu = reader.IsDBNull(2) ? null : reader.GetString(2),
                    YuklemeYeri = reader.IsDBNull(3) ? null : reader.GetString(3),
                    BosaltmaYeri = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Tarih = ParseDate(reader.IsDBNull(5) ? null : reader.GetString(5)),
                    Saat = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Fiyat = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7)),
                    Aciklama = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CekiciId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    CekiciPlaka = reader.IsDBNull(10) ? null : reader.GetString(10),
                    DorseId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    SoforId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    SoforAdi = reader.IsDBNull(13) ? null : reader.GetString(13)
                };
                result.Add(s);
            }
            return result;
        }

        public static List<Sefer> GetSeferlerByCekiciPlaka(string cekiciPlaka)
        {
            var result = new List<Sefer>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SeferId, KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Tarih, Saat, Fiyat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi FROM Seferler WHERE CekiciPlaka = @p ORDER BY SeferId DESC";
            cmd.Parameters.AddWithValue("@p", cekiciPlaka);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var s = new Sefer
                {
                    SeferId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    KonteynerNo = reader.IsDBNull(1) ? null : reader.GetString(1),
                    KonteynerBoyutu = reader.IsDBNull(2) ? null : reader.GetString(2),
                    YuklemeYeri = reader.IsDBNull(3) ? null : reader.GetString(3),
                    BosaltmaYeri = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Tarih = ParseDate(reader.IsDBNull(5) ? null : reader.GetString(5)),
                    Saat = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Fiyat = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7)),
                    Aciklama = reader.IsDBNull(8) ? null : reader.GetString(8),
                    CekiciId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    CekiciPlaka = reader.IsDBNull(10) ? null : reader.GetString(10),
                    DorseId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    SoforId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    SoforAdi = reader.IsDBNull(13) ? null : reader.GetString(13)
                };
                result.Add(s);
            }
            return result;
        }

        private static void BindSeferParams(SqliteCommand cmd, Sefer s)
        {
            cmd.Parameters.AddWithValue("@KonteynerNo", (object?)s.KonteynerNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KonteynerBoyutu", (object?)s.KonteynerBoyutu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@YuklemeYeri", (object?)s.YuklemeYeri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BosaltmaYeri", (object?)s.BosaltmaYeri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", s.Tarih == default ? DateTime.Today.ToString("yyyy-MM-dd") : s.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Saat", (object?)s.Saat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Fiyat", Convert.ToDouble(s.Fiyat));
            cmd.Parameters.AddWithValue("@Aciklama", (object?)s.Aciklama ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CekiciId", (object?)s.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CekiciPlaka", (object?)s.CekiciPlaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DorseId", (object?)s.DorseId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SoforId", (object?)s.SoforId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SoforAdi", (object?)s.SoforAdi ?? DBNull.Value);
        }

        public class Arac
        {
            public string? Plaka { get; set; }
            public string? SoforAdi { get; set; }
        }

        private static bool ValidateSefer(Sefer sefer)
        {
            // Minimal doğrulama: KonteynerNo boş olmasın
            return !string.IsNullOrWhiteSpace(sefer.KonteynerNo);
        }

        private static DateTime ParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return DateTime.Today;
            if (DateTime.TryParse(text, out var d)) return d;
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return d;
            return DateTime.Today;
        }

        public static string? GetDorsePlakaByCekiciPlaka(string plaka)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT D.Plaka
                                    FROM Cekiciler C
                                    LEFT JOIN Dorseler D ON C.DorseId = D.DorseId
                                    WHERE C.Plaka = @Plaka
                                    LIMIT 1";
                cmd.Parameters.AddWithValue("@Plaka", plaka);
                var result = cmd.ExecuteScalar();
                return result == null || result is DBNull ? null : Convert.ToString(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetDorsePlakaByCekiciPlaka hata: {ex.Message}");
                return null;
            }
        }

        public static (int? cekiciId, int? soforId, int? dorseId, string? soforAdi, string? dorsePlaka) GetVehicleInfoByCekiciPlaka(string plaka)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"SELECT C.CekiciId, C.SoforId, C.DorseId, S.SoforAdi, D.Plaka
                                    FROM Cekiciler C
                                    LEFT JOIN Soforler S ON C.SoforId = S.SoforId
                                    LEFT JOIN Dorseler D ON C.DorseId = D.DorseId
                                    WHERE C.Plaka = @Plaka
                                    LIMIT 1";
                cmd.Parameters.AddWithValue("@Plaka", plaka);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int? cekiciId = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                    int? soforId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                    int? dorseId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                    string? soforAdi = reader.IsDBNull(3) ? null : reader.GetString(3);
                    string? dorsePlaka = reader.IsDBNull(4) ? null : reader.GetString(4);
                    return (cekiciId, soforId, dorseId, soforAdi, dorsePlaka);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetVehicleInfoByCekiciPlaka hata: {ex.Message}");
            }
            return (null, null, null, null, null);
        }

        public static int RestoreMissingCekicilerFromSeferler()
        {
            int inserted = 0;
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Seferler'deki plakalardan Cekiciler'de olmayanları, en son sefer bilgileriyle ekle
                string query = @"
WITH latest AS (
    SELECT CekiciPlaka, MAX(SeferId) AS MaxSeferId
    FROM Seferler
    WHERE CekiciPlaka IS NOT NULL AND TRIM(CekiciPlaka) <> ''
    GROUP BY CekiciPlaka
), src AS (
    SELECT s.CekiciPlaka, s.SoforId, s.DorseId
    FROM Seferler s
    INNER JOIN latest l ON l.CekiciPlaka = s.CekiciPlaka AND l.MaxSeferId = s.SeferId
)
SELECT src.CekiciPlaka, src.SoforId, src.DorseId
FROM src
LEFT JOIN Cekiciler c ON c.Plaka = src.CekiciPlaka
WHERE c.CekiciId IS NULL;";

                using var selectCmd = new SqliteCommand(query, connection);
                using var reader = selectCmd.ExecuteReader();
                var items = new List<(string plaka, int? soforId, int? dorseId)>();
                while (reader.Read())
                {
                    var plaka = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    int? soforId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                    int? dorseId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                    if (!string.IsNullOrWhiteSpace(plaka))
                        items.Add((plaka, soforId, dorseId));
                }
                reader.Close();

                foreach (var it in items)
                {
                    using var ins = new SqliteCommand("INSERT INTO Cekiciler (Plaka, SoforId, DorseId, Aktif) VALUES (@p, @s, @d, 1)", connection);
                    ins.Parameters.AddWithValue("@p", it.plaka);
                    ins.Parameters.AddWithValue("@s", (object?)it.soforId ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@d", (object?)it.dorseId ?? DBNull.Value);
                    inserted += ins.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] RestoreMissingCekicilerFromSeferler hata: {ex.Message}");
            }
            return inserted;
        }

        public static int RestoreMissingDorselerFromSeferler()
        {
            int inserted = 0;
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string query = @"
SELECT DISTINCT s.DorseId
FROM Seferler s
LEFT JOIN Dorseler d ON d.DorseId = s.DorseId
WHERE s.DorseId IS NOT NULL AND d.DorseId IS NULL;";

                using var selectCmd = new SqliteCommand(query, connection);
                using var reader = selectCmd.ExecuteReader();
                var ids = new List<int>();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0)) ids.Add(reader.GetInt32(0));
                }
                reader.Close();

                foreach (var id in ids)
                {
                    using var ins = new SqliteCommand("INSERT INTO Dorseler (DorseId, Plaka, Tip, Arsivli) VALUES (@id, @plaka, @tip, 0)", connection);
                    ins.Parameters.AddWithValue("@id", id);
                    ins.Parameters.AddWithValue("@plaka", $"RESTORE-{id}");
                    ins.Parameters.AddWithValue("@tip", "Unknown");
                    inserted += ins.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] RestoreMissingDorselerFromSeferler hata: {ex.Message}");
            }
            return inserted;
        }

        public static int RestoreMissingSoforlerFromSeferler()
        {
            int inserted = 0;
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Seferlerdeki SoforId’lerden Soforler’de olmayanları, en son seferdeki isimle ekle
                string query = @"
WITH latest AS (
    SELECT SoforId, MAX(SeferId) AS MaxSeferId
    FROM Seferler
    WHERE SoforId IS NOT NULL
    GROUP BY SoforId
), src AS (
    SELECT s.SoforId, COALESCE(s.SoforAdi, 'Bilinmeyen') AS SoforAdi
    FROM Seferler s
    INNER JOIN latest l ON l.SoforId = s.SoforId AND l.MaxSeferId = s.SeferId
)
SELECT src.SoforId, src.SoforAdi
FROM src
LEFT JOIN Soforler f ON f.SoforId = src.SoforId
WHERE f.SoforId IS NULL;";

                using var selectCmd = new SqliteCommand(query, connection);
                using var reader = selectCmd.ExecuteReader();
                var items = new List<(int soforId, string soforAdi)>();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var id = reader.GetInt32(0);
                        var adi = reader.IsDBNull(1) ? "Bilinmeyen" : reader.GetString(1);
                        items.Add((id, adi));
                    }
                }
                reader.Close();

                foreach (var it in items)
                {
                    using var ins = new SqliteCommand("INSERT INTO Soforler (SoforId, SoforAdi, Telefon, Arsivli) VALUES (@id, @ad, '', 0)", connection);
                    ins.Parameters.AddWithValue("@id", it.soforId);
                    ins.Parameters.AddWithValue("@ad", it.soforAdi);
                    inserted += ins.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] RestoreMissingSoforlerFromSeferler hata: {ex.Message}");
            }
            return inserted;
        }

        public static void CheckAndCreateOrUpdateYakitGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS YakitGider (
                    YakitId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER,
                    Plaka TEXT,
                    Tarih TEXT NOT NULL,
                    Istasyon TEXT,
                    Litre REAL,
                    BirimFiyat REAL,
                    Tutar REAL,
                    Km INTEGER,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Plaka TEXT",
                    "Tarih TEXT",
                    "Istasyon TEXT",
                    "Litre REAL",
                    "BirimFiyat REAL",
                    "Tutar REAL",
                    "Km INTEGER",
                    "Aciklama TEXT"
                ];
                EnsureTable("YakitGider", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateYakitGiderTablosu hata: {ex.Message}");
            }
        }

        public static int YakitEkle(YakitGider y)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO YakitGider (CekiciId, Plaka, Tarih, Istasyon, Litre, BirimFiyat, Tutar, Km, Aciklama)
                                    VALUES (@CekiciId, @Plaka, @Tarih, @Istasyon, @Litre, @BirimFiyat, @Tutar, @Km, @Aciklama);
                                    SELECT last_insert_rowid();";
                BindYakitParams(cmd, y);
                var id = (long)cmd.ExecuteScalar()!;
                return (int)id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] YakitEkle hata: {ex.Message}");
                return 0;
            }
        }

        public static void YakitGuncelle(YakitGider y)
        {
            if (y.YakitId <= 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE YakitGider SET
                                    CekiciId=@CekiciId, Plaka=@Plaka, Tarih=@Tarih, Istasyon=@Istasyon,
                                    Litre=@Litre, BirimFiyat=@BirimFiyat, Tutar=@Tutar, Km=@Km, Aciklama=@Aciklama
                                   WHERE YakitId=@Id";
                BindYakitParams(cmd, y);
                cmd.Parameters.AddWithValue("@Id", y.YakitId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] YakitGuncelle hata: {ex.Message}");
            }
        }

        public static void YakitSil(int id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM YakitGider WHERE YakitId=@Id";
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] YakitSil hata: {ex.Message}");
            }
        }

        public static List<YakitGider> GetYakitGiderleri(int? cekiciId)
            => GetYakitGiderleri(cekiciId, null, null);

        public static List<YakitGider> GetYakitGiderleri(int? cekiciId, DateTime? baslangic, DateTime? bitis)
        {
            var list = new List<YakitGider>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();

                var sql = "SELECT YakitId, CekiciId, Plaka, Tarih, Istasyon, Litre, BirimFiyat, Tutar, Km, Aciklama FROM YakitGider WHERE 1=1";
                if (cekiciId.HasValue) sql += " AND CekiciId=@Id";
                if (baslangic.HasValue) sql += " AND Tarih >= @Bas";
                if (bitis.HasValue) sql += " AND Tarih <= @Bit";
                sql += " ORDER BY Tarih DESC, YakitId DESC";
                cmd.CommandText = sql;
                if (cekiciId.HasValue) cmd.Parameters.AddWithValue("@Id", cekiciId.Value);
                if (baslangic.HasValue) cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                if (bitis.HasValue) cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new YakitGider
                    {
                        YakitId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        CekiciId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        Plaka = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Tarih = DateTime.TryParse(reader.IsDBNull(3) ? null : reader.GetString(3), out var d) ? d : DateTime.Today,
                        Istasyon = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Litre = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5)),
                        BirimFiyat = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6)),
                        Tutar = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7)),
                        Km = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        Aciklama = reader.IsDBNull(9) ? null : reader.GetString(9)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetYakitGiderleri hata: {ex.Message}");
            }
            return list;
        }

        private static void BindYakitParams(SqliteCommand cmd, YakitGider y)
        {
            cmd.Parameters.AddWithValue("@CekiciId", (object?)y.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plaka", (object?)y.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", y.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Istasyon", (object?)y.Istasyon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Litre", Convert.ToDouble(y.Litre));
            cmd.Parameters.AddWithValue("@BirimFiyat", Convert.ToDouble(y.BirimFiyat));
            cmd.Parameters.AddWithValue("@Tutar", Convert.ToDouble(y.Tutar));
            cmd.Parameters.AddWithValue("@Km", (object?)y.Km ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Aciklama", (object?)y.Aciklama ?? DBNull.Value);
        }
        
        // SanaiGider tablosu ile ilgili eklemeler
        public static void CheckAndCreateOrUpdateSanaiGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS SanaiGider (
                    SanaiId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER,
                    Plaka TEXT,
                    Tarih TEXT NOT NULL,
                    Kalem TEXT,
                    Tutar REAL,
                    Km INTEGER,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Plaka TEXT",
                    "Tarih TEXT",
                    "Kalem TEXT",
                    "Tutar REAL",
                    "Km INTEGER",
                    "Aciklama TEXT"
                ];
                EnsureTable("SanaiGider", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateSanaiGiderTablosu hata: {ex.Message}");
            }
        }

        public static int SanaiGiderEkle(SanaiGider s)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO SanaiGider (CekiciId, Plaka, Tarih, Kalem, Tutar, Km, Aciklama)
                                    VALUES (@CekiciId, @Plaka, @Tarih, @Kalem, @Tutar, @Km, @Aciklama);
                                    SELECT last_insert_rowid();";
                BindSanaiParams(cmd, s);
                var id = (long)cmd.ExecuteScalar()!;
                return (int)id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] SanaiGiderEkle hata: {ex.Message}");
                return 0;
            }
        }

        public static void SanaiGiderGuncelle(SanaiGider s)
        {
            if (s.SanaiId <= 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE SanaiGider SET
                                    CekiciId=@CekiciId, Plaka=@Plaka, Tarih=@Tarih, Kalem=@Kalem,
                                    Tutar=@Tutar, Km=@Km, Aciklama=@Aciklama
                                   WHERE SanaiId=@Id";
                BindSanaiParams(cmd, s);
                cmd.Parameters.AddWithValue("@Id", s.SanaiId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] SanaiGiderGuncelle hata: {ex.Message}");
            }
        }

        public static void SanaiGiderSil(int id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM SanaiGider WHERE SanaiId=@Id";
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] SanaiGiderSil hata: {ex.Message}");
            }
        }

        public static List<SanaiGider> GetSanaiGiderleri(int? cekiciId, DateTime? baslangic, DateTime? bitis)
        {
            var list = new List<SanaiGider>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                var sql = "SELECT SanaiId, CekiciId, Plaka, Tarih, Kalem, Tutar, Km, Aciklama FROM SanaiGider WHERE 1=1";
                if (cekiciId.HasValue) sql += " AND CekiciId=@Id";
                if (baslangic.HasValue) sql += " AND Tarih >= @Bas";
                if (bitis.HasValue) sql += " AND Tarih <= @Bit";
                sql += " ORDER BY Tarih DESC, SanaiId DESC";
                cmd.CommandText = sql;
                if (cekiciId.HasValue) cmd.Parameters.AddWithValue("@Id", cekiciId.Value);
                if (baslangic.HasValue) cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                if (bitis.HasValue) cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new SanaiGider
                    {
                        SanaiId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        CekiciId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        Plaka = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Tarih = DateTime.TryParse(reader.IsDBNull(3) ? null : reader.GetString(3), out var d) ? d : DateTime.Today,
                        Kalem = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Tutar = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5)),
                        Km = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        Aciklama = reader.IsDBNull(7) ? null : reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetSanaiGiderleri hata: {ex.Message}");
            }
            return list;
        }

        private static void BindSanaiParams(SqliteCommand cmd, SanaiGider s)
        {
            cmd.Parameters.AddWithValue("@CekiciId", (object?)s.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plaka", (object?)s.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", s.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Kalem", (object?)s.Kalem ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tutar", Convert.ToDouble(s.Tutar));
            cmd.Parameters.AddWithValue("@Km", (object?)s.Km ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Aciklama", (object?)s.Aciklama ?? DBNull.Value);
        }

        public static void CheckAndCreateOrUpdateGenelGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS GenelGider (
                    GiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER,
                    Plaka TEXT,
                    Tarih TEXT NOT NULL,
                    Tutar REAL,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Plaka TEXT",
                    "Tarih TEXT",
                    "Tutar REAL",
                    "Aciklama TEXT"
                ];
                EnsureTable("GenelGider", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateGenelGiderTablosu hata: {ex.Message}");
            }
        }

        public static int GenelGiderEkle(GenelGider g)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO GenelGider (CekiciId, Plaka, Tarih, Tutar, Aciklama)
                                    VALUES (@CekiciId, @Plaka, @Tarih, @Tutar, @Aciklama);
                                    SELECT last_insert_rowid();";
                BindGenelParams(cmd, g);
                var id = (long)cmd.ExecuteScalar()!;
                return (int)id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GenelGiderEkle hata: {ex.Message}");
                return 0;
            }
        }

        public static void GenelGiderGuncelle(GenelGider g)
        {
            if (g.GiderId <= 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE GenelGider SET
                                    CekiciId=@CekiciId, Plaka=@Plaka, Tarih=@Tarih,
                                    Tutar=@Tutar, Aciklama=@Aciklama
                                   WHERE GiderId=@Id";
                BindGenelParams(cmd, g);
                cmd.Parameters.AddWithValue("@Id", g.GiderId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GenelGiderGuncelle hata: {ex.Message}");
            }
        }

        public static void GenelGiderSil(int id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM GenelGider WHERE GiderId=@Id";
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GenelGiderSil hata: {ex.Message}");
            }
        }

        public static List<GenelGider> GetGenelGiderleri(int? cekiciId, DateTime? baslangic, DateTime? bitis)
        {
            var list = new List<GenelGider>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                var sql = "SELECT GiderId, CekiciId, Plaka, Tarih, Tutar, Aciklama FROM GenelGider WHERE 1=1";
                if (cekiciId.HasValue) sql += " AND CekiciId=@Id";
                if (baslangic.HasValue) sql += " AND Tarih >= @Bas";
                if (bitis.HasValue) sql += " AND Tarih <= @Bit";
                sql += " ORDER BY Tarih DESC, GiderId DESC";
                cmd.CommandText = sql;
                if (cekiciId.HasValue) cmd.Parameters.AddWithValue("@Id", cekiciId.Value);
                if (baslangic.HasValue) cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                if (bitis.HasValue) cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new GenelGider
                    {
                        GiderId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        CekiciId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        Plaka = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Tarih = DateTime.TryParse(reader.IsDBNull(3) ? null : reader.GetString(3), out var d) ? d : DateTime.Today,
                        Tutar = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4)),
                        Aciklama = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetGenelGiderleri hata: {ex.Message}");
            }
            return list;
        }

        private static void BindGenelParams(SqliteCommand cmd, GenelGider g)
        {
            cmd.Parameters.AddWithValue("@CekiciId", (object?)g.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plaka", (object?)g.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", g.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Tutar", Convert.ToDouble(g.Tutar));
            cmd.Parameters.AddWithValue("@Aciklama", (object?)g.Aciklama ?? DBNull.Value);
        }

        public static void CheckAndCreateOrUpdatePersonelGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS PersonelGider (
                    PersonelGiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER,
                    Plaka TEXT,
                    Tarih TEXT NOT NULL,
                    PersonelAdi TEXT,
                    OdemeTuru TEXT,
                    SgkDonem TEXT,
                    VergiTuru TEXT,
                    Tutar REAL,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Plaka TEXT",
                    "Tarih TEXT",
                    "PersonelAdi TEXT",
                    "OdemeTuru TEXT",
                    "SgkDonem TEXT",
                    "VergiTuru TEXT",
                    "Tutar REAL",
                    "Aciklama TEXT"
                ];
                EnsureTable("PersonelGider", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdatePersonelGiderTablosu hata: {ex.Message}");
            }
        }

        public static int PersonelGiderEkle(PersonelGider g)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO PersonelGider (CekiciId, Plaka, Tarih, PersonelAdi, OdemeTuru, SgkDonem, VergiTuru, Tutar, Aciklama)
                                    VALUES (@CekiciId, @Plaka, @Tarih, @PersonelAdi, @OdemeTuru, @SgkDonem, @VergiTuru, @Tutar, @Aciklama);
                                    SELECT last_insert_rowid();";
                BindPersonelParams(cmd, g);
                var id = (long)cmd.ExecuteScalar()!;
                return (int)id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] PersonelGiderEkle hata: {ex.Message}");
                return 0;
            }
        }

        public static void PersonelGiderGuncelle(PersonelGider g)
        {
            if (g.PersonelGiderId <= 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE PersonelGider SET
                                    CekiciId=@CekiciId, Plaka=@Plaka, Tarih=@Tarih,
                                    PersonelAdi=@PersonelAdi, OdemeTuru=@OdemeTuru, SgkDonem=@SgkDonem, VergiTuru=@VergiTuru,
                                    Tutar=@Tutar, Aciklama=@Aciklama
                                   WHERE PersonelGiderId=@Id";
                BindPersonelParams(cmd, g);
                cmd.Parameters.AddWithValue("@Id", g.PersonelGiderId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] PersonelGiderGuncelle hata: {ex.Message}");
            }
        }

        public static void PersonelGiderSil(int id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM PersonelGider WHERE PersonelGiderId=@Id";
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] PersonelGiderSil hata: {ex.Message}");
            }
        }

        public static List<PersonelGider> GetPersonelGiderleri(int? cekiciId, DateTime? baslangic, DateTime? bitis)
        {
            var list = new List<PersonelGider>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                var sql = "SELECT PersonelGiderId, CekiciId, Plaka, Tarih, PersonelAdi, OdemeTuru, SgkDonem, VergiTuru, Tutar, Aciklama FROM PersonelGider WHERE 1=1";
                if (cekiciId.HasValue) sql += " AND CekiciId=@Id";
                if (baslangic.HasValue) sql += " AND Tarih >= @Bas";
                if (bitis.HasValue) sql += " AND Tarih <= @Bit";
                sql += " ORDER BY Tarih DESC, PersonelGiderId DESC";
                cmd.CommandText = sql;
                if (cekiciId.HasValue) cmd.Parameters.AddWithValue("@Id", cekiciId.Value);
                if (baslangic.HasValue) cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                if (bitis.HasValue) cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PersonelGider
                    {
                        PersonelGiderId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        CekiciId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        Plaka = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Tarih = DateTime.TryParse(reader.IsDBNull(3) ? null : reader.GetString(3), out var d) ? d : DateTime.Today,
                        PersonelAdi = reader.IsDBNull(4) ? null : reader.GetString(4),
                        OdemeTuru = reader.IsDBNull(5) ? null : reader.GetString(5),
                        SgkDonem = reader.IsDBNull(6) ? null : reader.GetString(6),
                        VergiTuru = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Tutar = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetDouble(8)),
                        Aciklama = reader.IsDBNull(9) ? null : reader.GetString(9)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetPersonelGiderleri hata: {ex.Message}");
            }
            return list;
        }

        private static void BindPersonelParams(SqliteCommand cmd, PersonelGider g)
        {
            cmd.Parameters.AddWithValue("@CekiciId", (object?)g.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plaka", (object?)g.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", g.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@PersonelAdi", (object?)g.PersonelAdi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OdemeTuru", (object?)g.OdemeTuru ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SgkDonem", (object?)g.SgkDonem ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VergiTuru", (object?)g.VergiTuru ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tutar", Convert.ToDouble(g.Tutar));
            cmd.Parameters.AddWithValue("@Aciklama", (object?)g.Aciklama ?? DBNull.Value);
        }

        public static void CheckAndCreateOrUpdateVergiAracTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS VergiArac (
                    VergiId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CekiciId INTEGER,
                    Plaka TEXT,
                    Tarih TEXT NOT NULL,
                    VergiTuru TEXT,
                    Donem TEXT,
                    VarlikTipi TEXT,
                    DorseId INTEGER,
                    DorsePlaka TEXT,
                    Tutar REAL,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Plaka TEXT",
                    "Tarih TEXT",
                    "VergiTuru TEXT",
                    "Donem TEXT",
                    "VarlikTipi TEXT",
                    "DorseId INTEGER",
                    "DorsePlaka TEXT",
                    "Tutar REAL",
                    "Aciklama TEXT"
                ];
                EnsureTable("VergiArac", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateVergiAracTablosu hata: {ex.Message}");
            }
        }

        public static int VergiAracEkle(VergiArac v)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO VergiArac (CekiciId, Plaka, Tarih, VergiTuru, Donem, VarlikTipi, DorseId, DorsePlaka, Tutar, Aciklama)
                                    VALUES (@CekiciId, @Plaka, @Tarih, @VergiTuru, @Donem, @VarlikTipi, @DorseId, @DorsePlaka, @Tutar, @Aciklama);
                                    SELECT last_insert_rowid();";
                BindVergiAracParams(cmd, v);
                var id = (long)cmd.ExecuteScalar()!;
                return (int)id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] VergiAracEkle hata: {ex.Message}");
                return 0;
            }
        }

        public static void VergiAracGuncelle(VergiArac v)
        {
            if (v.VergiId <= 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"UPDATE VergiArac SET
                                    CekiciId=@CekiciId, Plaka=@Plaka, Tarih=@Tarih, VergiTuru=@VergiTuru, Donem=@Donem,
                                    VarlikTipi=@VarlikTipi, DorseId=@DorseId, DorsePlaka=@DorsePlaka,
                                    Tutar=@Tutar, Aciklama=@Aciklama
                                   WHERE VergiId=@Id";
                BindVergiAracParams(cmd, v);
                cmd.Parameters.AddWithValue("@Id", v.VergiId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] VergiAracGuncelle hata: {ex.Message}");
            }
        }

        public static void VergiAracSil(int id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM VergiArac WHERE VergiId=@Id";
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] VergiAracSil hata: {ex.Message}");
            }
        }

        public static List<VergiArac> GetVergiAraclari(int? cekiciId, DateTime? baslangic, DateTime? bitis)
        {
            var list = new List<VergiArac>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                var sql = "SELECT VergiId, CekiciId, Plaka, Tarih, VergiTuru, Donem, VarlikTipi, DorseId, DorsePlaka, Tutar, Aciklama FROM VergiArac WHERE 1=1";
                if (cekiciId.HasValue) sql += " AND CekiciId=@Id";
                if (baslangic.HasValue) sql += " AND Tarih >= @Bas";
                if (bitis.HasValue) sql += " AND Tarih <= @Bit";
                sql += " ORDER BY Tarih DESC, VergiId DESC";
                cmd.CommandText = sql;
                if (cekiciId.HasValue) cmd.Parameters.AddWithValue("@Id", cekiciId.Value);
                if (baslangic.HasValue) cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                if (bitis.HasValue) cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new VergiArac
                    {
                        VergiId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        CekiciId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        Plaka = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Tarih = DateTime.TryParse(reader.IsDBNull(3) ? null : reader.GetString(3), out var d) ? d : DateTime.Today,
                        VergiTuru = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Donem = reader.IsDBNull(5) ? null : reader.GetString(5),
                        VarlikTipi = reader.IsDBNull(6) ? null : reader.GetString(6),
                        DorseId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        DorsePlaka = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Tutar = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9)),
                        Aciklama = reader.IsDBNull(10) ? null : reader.GetString(10)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetVergiAraclari hata: {ex.Message}");
            }
            return list;
        }

        private static void BindVergiAracParams(SqliteCommand cmd, VergiArac v)
        {
            cmd.Parameters.AddWithValue("@CekiciId", (object?)v.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Plaka", (object?)v.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", v.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@VergiTuru", (object?)v.VergiTuru ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Donem", (object?)v.Donem ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VarlikTipi", (object?)v.VarlikTipi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DorseId", (object?)v.DorseId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DorsePlaka", (object?)v.DorsePlaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tutar", Convert.ToDouble(v.Tutar));
            cmd.Parameters.AddWithValue("@Aciklama", (object?)v.Aciklama ?? DBNull.Value);
        }

        public static void CheckAndCreateOrUpdateDepoTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS Depolar (
                    DepoId INTEGER PRIMARY KEY AUTOINCREMENT,
                    DepoAdi TEXT NOT NULL,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "DepoAdi TEXT",
                    "Aciklama TEXT"
                ];
                EnsureTable("Depolar", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateDepoTablosu hata: {ex.Message}");
            }
        }

        public static void CheckAndCreateOrUpdateGuzergahTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS Guzergahlar (
                    GuzergahId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CikisDepoId INTEGER NOT NULL,
                    VarisDepoId INTEGER NOT NULL,
                    BosDolu TEXT,
                    Ucret REAL,
                    Aciklama TEXT
                );";
                string[] requiredColumns = [
                    "CikisDepoId INTEGER",
                    "VarisDepoId INTEGER",
                    "BosDolu TEXT",
                    "Ucret REAL",
                    "Aciklama TEXT"
                ];
                EnsureTable("Guzergahlar", createScript, requiredColumns);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] CheckAndCreateOrUpdateGuzergahTablosu hata: {ex.Message}");
            }
        }
    }
}
