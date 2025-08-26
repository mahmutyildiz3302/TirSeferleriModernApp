using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TirSeferleriModernApp.Models;
using static TirSeferleriModernApp.Views.VergilerAracView;

namespace TirSeferleriModernApp.Services
{
    public class DatabaseService
    {
        private static readonly string DbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TirSeferleri.db");
        public static readonly string ConnectionString = $"Data Source={DbFile}";
        private readonly string _dbFile;
        private readonly string _instanceConnectionString;

        public DatabaseService(string dbFile)
        {
            _dbFile = dbFile;
            _instanceConnectionString = $"Data Source={_dbFile}";
        }

        // -------------------- Initialization --------------------
        public void Initialize()
        {
            EnsureDatabaseFile();

            // Önce varsa kolon adını değiştir (eski -> yeni) ki devam eden CREATE/ALTER adımları çakışmasın
            TryRenameSeferlerEkstraToEmanetSoda();

            CheckAndCreateOrUpdateSeferlerTablosu();
            CheckAndCreateOrUpdateGiderlerTablosu();
            CheckAndCreateOrUpdateKarHesapTablosu();
            EnsureSoforlerArsivliColumn();
            EnsureCekicilerArsivliColumn();
            EnsureDorselerArsivliColumn();
            CheckAndCreateOrUpdateGenelGiderTablosu();
            CheckAndCreateOrUpdatePersonelGiderTablosu();
            CheckAndCreateOrUpdateVergiAracTablosu();
            CheckAndCreateOrUpdateDepoTablosu();
            CheckAndCreateOrUpdateGuzergahTablosu();
            CheckAndCreateOrUpdateYakitGiderTablosu();

            // Seed only first time
            EnsureDefaultDepolar();

            try
            {
                MigrateGuzergahlarToSingleFiyat();
                EnsureGuzergahAutoColumnAndUniqueIndex();
                GenerateAllGuzergahlar();
                SeedGivenGuzergahlar();

                // 'EKSTRA YOK' metnini tek boşluk yap
                NormalizeEkstraBosluk();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Initialize] Migration/Seed error: " + ex.Message);
            }
        }

        // Seferler tablosunda Ekstra -> "Emanet/Soda" sütun adını taşı
        private static void TryRenameSeferlerEkstraToEmanetSoda()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();

                bool hasSeferler = false;
                using (var chkTbl = conn.CreateCommand())
                {
                    chkTbl.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Seferler'";
                    hasSeferler = chkTbl.ExecuteScalar() != null;
                }
                if (!hasSeferler) return;

                bool hasOld = false, hasNew = false;
                using (var info = conn.CreateCommand())
                {
                    info.CommandText = "PRAGMA table_info(Seferler);";
                    using var rdr = info.ExecuteReader();
                    while (rdr.Read())
                    {
                        var col = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                        if (string.Equals(col, "Ekstra", StringComparison.OrdinalIgnoreCase)) hasOld = true;
                        if (string.Equals(col, "Emanet/Soda", StringComparison.OrdinalIgnoreCase)) hasNew = true;
                    }
                }

                if (hasOld && !hasNew)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "ALTER TABLE Seferler RENAME COLUMN Ekstra TO \"Emanet/Soda\"";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine("[TryRenameSeferlerEkstraToEmanetSoda] " + ex.Message); }
        }

        private static void NormalizeEkstraBosluk()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();

                // Yeni isim için güncelle
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE Seferler SET \"Emanet/Soda\"=' ' WHERE UPPER(TRIM(IFNULL(\"Emanet/Soda\",'')))='EKSTRA YOK'";
                    cmd.ExecuteNonQuery();
                }
                // Eski isim varsa (taşınmamış eski DB) onu da güncelle
                using (var cmd2 = conn.CreateCommand())
                {
                    cmd2.CommandText = "UPDATE Seferler SET Ekstra=' ' WHERE UPPER(TRIM(IFNULL(Ekstra,'')))='EKSTRA YOK'";
                    cmd2.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void EnsureDatabaseFile()
        {
            if (!File.Exists(_dbFile))
            {
                using var initialConnection = new SqliteConnection(_instanceConnectionString);
                initialConnection.Open();
            }
        }

        public static void EnsureDatabaseFileStatic()
        {
            if (!File.Exists(DbFile))
            {
                using var initialConnection = new SqliteConnection(ConnectionString);
                initialConnection.Open();
            }
        }

        // -------------------- Helpers --------------------
        private static DateTime ParseDate(string? s)
        {
            if (DateTime.TryParse(s, out var dt)) return dt;
            return DateTime.Today;
        }

        private static bool ValidateSeferInternal(Sefer s)
        {
            if (s == null) return false;
            return !string.IsNullOrWhiteSpace(s.KonteynerBoyutu);
        }

        private static bool ColumnExists(SqliteConnection conn, string table, string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                if (!rdr.IsDBNull(1) && string.Equals(rdr.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Aliases to canonical
        private static string NormalizeDepoName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var n = name.Trim();
            var m = Regex.Match(n, @"^TER[\s-]*([0-9]+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var terNo))
                return $"TERMİNAL{terNo}";
            if (string.Equals(n, "TER-4", StringComparison.OrdinalIgnoreCase)) return "TERMİNAL4";
            return n;
        }

        private static int? TryGetDepoIdByName(SqliteConnection conn, string depoAdiRaw)
        {
            var a = (depoAdiRaw ?? string.Empty).Trim();
            var an = NormalizeDepoName(a);
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT DepoId FROM Depolar WHERE UPPER(TRIM(DepoAdi)) IN (UPPER(TRIM(@a)), UPPER(TRIM(@an))) LIMIT 1";
            check.Parameters.AddWithValue("@a", a);
            check.Parameters.AddWithValue("@an", an);
            var val = check.ExecuteScalar();
            if (val == null || val == DBNull.Value) return null;
            return Convert.ToInt32(val);
        }

        private static int EnsureDepoAndGetId(SqliteConnection conn, string depoAdiRaw)
        {
            var id = TryGetDepoIdByName(conn, depoAdiRaw);
            if (id != null) return id.Value;
            var canonical = NormalizeDepoName(depoAdiRaw);
            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO Depolar (DepoAdi, Aciklama) VALUES (@name, @acik); SELECT last_insert_rowid();";
            ins.Parameters.AddWithValue("@name", canonical);
            if (!string.Equals(canonical, depoAdiRaw, StringComparison.OrdinalIgnoreCase))
                ins.Parameters.AddWithValue("@acik", $"{depoAdiRaw.Trim()} = {canonical}");
            else
                ins.Parameters.AddWithValue("@acik", DBNull.Value);
            return (int)(long)ins.ExecuteScalar()!;
        }

        // -------------------- Seed (respect user deletions) --------------------
        public static void EnsureDefaultDepolar()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();

                // Seed only when empty
                using (var cnt = conn.CreateCommand())
                {
                    cnt.CommandText = "SELECT COUNT(1) FROM Depolar";
                    var c = Convert.ToInt32(cnt.ExecuteScalar() ?? 0);
                    if (c > 0) return;
                }

                string[] defaults =
                {
                    "ARDEP", "DEMİRELLER", "ESKİ MADEN", "FALCON", "KAHRAMANLI", "LİMAN-ŞİŞECAM", "NİSA", "NİSA-4", "OSG",
                    "CCIS", "LİMAN", "OLS", "OLS-1", "TER-2", "EKANET", "TER-4"
                };

                foreach (var raw in defaults)
                {
                    var alias = raw.Trim();
                    var canonical = NormalizeDepoName(alias);
                    using var ins = conn.CreateCommand();
                    ins.CommandText = "INSERT INTO Depolar (DepoAdi, Aciklama) VALUES (@name, @acik)";
                    ins.Parameters.AddWithValue("@name", canonical);
                    if (!string.Equals(alias, canonical, StringComparison.OrdinalIgnoreCase))
                        ins.Parameters.AddWithValue("@acik", $"{alias} = {canonical}");
                    else
                        ins.Parameters.AddWithValue("@acik", DBNull.Value);
                    ins.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        // -------------------- Migrations / Schema helpers --------------------
        public static void MigrateGuzergahlarToSingleFiyat()
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();

            bool hasFiyat = ColumnExists(conn, "Guzergahlar", "Fiyat");
            bool hasAnyOld = ColumnExists(conn, "Guzergahlar", "BosFiyat") || ColumnExists(conn, "Guzergahlar", "DoluFiyat") || ColumnExists(conn, "Guzergahlar", "EmanetBosFiyat") || ColumnExists(conn, "Guzergahlar", "EmanetDoluFiyat") || ColumnExists(conn, "Guzergahlar", "SodaBosFiyat") || ColumnExists(conn, "Guzergahlar", "SodaDoluFiyat");
            if (hasFiyat && !hasAnyOld) return;

            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Guzergahlar_new (
                        GuzergahId INTEGER PRIMARY KEY AUTOINCREMENT,
                        CikisDepoId INTEGER NOT NULL,
                        VarisDepoId INTEGER NOT NULL,
                        Fiyat REAL,
                        Aciklama TEXT
                    );";
                    cmd.ExecuteNonQuery();
                }

                using (var copy = conn.CreateCommand())
                {
                    copy.Transaction = tx;
                    if (hasAnyOld)
                    {
                        copy.CommandText = @"INSERT INTO Guzergahlar_new (GuzergahId, CikisDepoId, VarisDepoId, Fiyat, Aciklama)
                                            SELECT GuzergahId,
                                                   CikisDepoId,
                                                   VarisDepoId,
                                                   COALESCE(DoluFiyat, BosFiyat, EmanetDoluFiyat, EmanetBosFiyat, SodaDoluFiyat, SodaBosFiyat, NULL),
                                                   Aciklama
                                            FROM Guzergahlar";
                    }
                    else if (hasFiyat)
                    {
                        copy.CommandText = @"INSERT INTO Guzergahlar_new (GuzergahId, CikisDepoId, VarisDepoId, Fiyat, Aciklama)
                                            SELECT GuzergahId, CikisDepoId, VarisDepoId, Fiyat, Aciklama FROM Guzergahlar";
                    }
                    else
                    {
                        copy.CommandText = "SELECT 1";
                    }
                    copy.ExecuteNonQuery();
                }

                using (var drop = conn.CreateCommand())
                {
                    drop.Transaction = tx;
                    drop.CommandText = "DROP TABLE Guzergahlar;";
                    drop.ExecuteNonQuery();
                }

                using (var rename = conn.CreateCommand())
                {
                    rename.Transaction = tx;
                    rename.CommandText = "ALTER TABLE Guzergahlar_new RENAME TO Guzergahlar;";
                    rename.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine("[Migration] ToSingleFiyat failed: " + ex.Message);
                throw;
            }
        }

        public static void EnsureGuzergahAutoColumnAndUniqueIndex()
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();

            if (!ColumnExists(conn, "Guzergahlar", "OtomatikMi"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Guzergahlar ADD COLUMN OtomatikMi INTEGER NOT NULL DEFAULT 1;";
                try { alter.ExecuteNonQuery(); } catch (Exception ex) { Debug.WriteLine("[EnsureGuzergahAutoColumn] " + ex.Message); }
            }

            using var idx = conn.CreateCommand();
            idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_guzergah_cikis_varis ON Guzergahlar(CikisDepoId, VarisDepoId);";
            try { idx.ExecuteNonQuery(); } catch (Exception ex) { Debug.WriteLine("[EnsureGuzergahUnique] " + ex.Message); }
        }

        public static int GenerateAllGuzergahlar()
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT OR IGNORE INTO Guzergahlar (CikisDepoId, VarisDepoId, Fiyat, Aciklama, OtomatikMi)
                                    SELECT d1.DepoId, d2.DepoId, 0, NULL, 1
                                    FROM Depolar d1
                                    CROSS JOIN Depolar d2
                                    WHERE d1.DepoId <> d2.DepoId";
                int added = cmd.ExecuteNonQuery();
                tx.Commit();
                return added;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine("[GenerateAllGuzergahlar] " + ex.Message);
                return 0;
            }
        }

        public static int HandleNewDepo(int depoId)
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                int total = 0;
                using (var cmd1 = conn.CreateCommand())
                {
                    cmd1.Transaction = tx;
                    cmd1.CommandText = @"INSERT OR IGNORE INTO Guzergahlar (CikisDepoId, VarisDepoId, Fiyat, Aciklama, OtomatikMi)
                                          SELECT @id, d.DepoId, 0, NULL, 1 FROM Depolar d WHERE d.DepoId <> @id";
                    cmd1.Parameters.AddWithValue("@id", depoId);
                    total += cmd1.ExecuteNonQuery();
                }
                using (var cmd2 = conn.CreateCommand())
                {
                    cmd2.Transaction = tx;
                    cmd2.CommandText = @"INSERT OR IGNORE INTO Guzergahlar (CikisDepoId, VarisDepoId, Fiyat, Aciklama, OtomatikMi)
                                          SELECT d.DepoId, @id, 0, NULL, 1 FROM Depolar d WHERE d.DepoId <> @id";
                    cmd2.Parameters.AddWithValue("@id", depoId);
                    total += cmd2.ExecuteNonQuery();
                }
                tx.Commit();
                return total;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine("[HandleNewDepo] " + ex.Message);
                return 0;
            }
        }

        public static bool OnDepoDelete(int depoId, bool force = false)
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                using (var delAuto = conn.CreateCommand())
                {
                    delAuto.Transaction = tx;
                    delAuto.CommandText = "DELETE FROM Guzergahlar WHERE (CikisDepoId=@id OR VarisDepoId=@id) AND IFNULL(OtomatikMi,1)=1";
                    delAuto.Parameters.AddWithValue("@id", depoId);
                    delAuto.ExecuteNonQuery();
                }

                long manualCount = 0;
                using (var cnt = conn.CreateCommand())
                {
                    cnt.Transaction = tx;
                    cnt.CommandText = "SELECT COUNT(1) FROM Guzergahlar WHERE (CikisDepoId=@id OR VarisDepoId=@id) AND IFNULL(OtomatikMi,1)=0";
                    cnt.Parameters.AddWithValue("@id", depoId);
                    manualCount = (long)(cnt.ExecuteScalar() ?? 0L);
                }

                if (manualCount > 0 && !force)
                {
                    tx.Rollback();
                    return false;
                }

                if (force)
                {
                    using var delAll = conn.CreateCommand();
                    delAll.Transaction = tx;
                    delAll.CommandText = "DELETE FROM Guzergahlar WHERE (CikisDepoId=@id OR VarisDepoId=@id)";
                    delAll.Parameters.AddWithValue("@id", depoId);
                    delAll.ExecuteNonQuery();
                }

                using (var delDepo = conn.CreateCommand())
                {
                    delDepo.Transaction = tx;
                    delDepo.CommandText = "DELETE FROM Depolar WHERE DepoId=@id";
                    delDepo.Parameters.AddWithValue("@id", depoId);
                    delDepo.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine("[OnDepoDelete] " + ex.Message);
                return false;
            }
        }

        public static decimal? GetUcretForRoute(string? cikisDepoAdi, string? varisDepoAdi, string? ekstra, string? bosDolu)
        {
            if (string.IsNullOrWhiteSpace(cikisDepoAdi) || string.IsNullOrWhiteSpace(varisDepoAdi)) return null;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                var c = cikisDepoAdi.Trim();
                var v = varisDepoAdi.Trim();
                var cn = NormalizeDepoName(c);
                var vn = NormalizeDepoName(v);
                cmd.CommandText = @"SELECT g.Fiyat
                                     FROM Guzergahlar g
                                     INNER JOIN Depolar cd ON cd.DepoId = g.CikisDepoId
                                     INNER JOIN Depolar vd ON vd.DepoId = g.VarisDepoId
                                     WHERE (UPPER(cd.DepoAdi) = UPPER(@c) OR UPPER(cd.DepoAdi) = UPPER(@cn))
                                       AND (UPPER(vd.DepoAdi) = UPPER(@v) OR UPPER(vd.DepoAdi) = UPPER(@vn))
                                     LIMIT 1";
                cmd.Parameters.AddWithValue("@c", c);
                cmd.Parameters.AddWithValue("@cn", cn);
                cmd.Parameters.AddWithValue("@v", v);
                cmd.Parameters.AddWithValue("@vn", vn);
                var val = cmd.ExecuteScalar();
                if (val == null || val is DBNull) return null;
                return Convert.ToDecimal((double)val);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DatabaseService] GetUcretForRoute hata: {ex.Message}");
                return null;
            }
        }

        // -------------------- Schema creation helpers --------------------
        public static void CheckAndCreateOrUpdateGuzergahTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS Guzergahlar (
                    GuzergahId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CikisDepoId INTEGER NOT NULL,
                    VarisDepoId INTEGER NOT NULL,
                    Fiyat REAL,
                    Aciklama TEXT,
                    OtomatikMi INTEGER NOT NULL DEFAULT 1
                );";
                string[] requiredColumns = [
                    "CikisDepoId INTEGER",
                    "VarisDepoId INTEGER",
                    "Fiyat REAL",
                    "Aciklama TEXT",
                    "OtomatikMi INTEGER NOT NULL DEFAULT 1"
                ];
                EnsureTable("Guzergahlar", createScript, requiredColumns);

                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var idx = conn.CreateCommand();
                idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_guzergah_cikis_varis ON Guzergahlar(CikisDepoId, VarisDepoId);";
                idx.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateGenelGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS GenelGiderler (
                                        GiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                                        Aciklama TEXT,
                                        Tutar REAL,
                                        Tarih TEXT
                                    );";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdatePersonelGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS PersonelGiderler (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        PersonelId INTEGER,
                                        Aciklama TEXT,
                                        Tutar REAL,
                                        Tarih TEXT
                                    );";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateVergiAracTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS VergilerArac (
                                        VergiId INTEGER PRIMARY KEY AUTOINCREMENT,
                                        CekiciId INTEGER,
                                        Plaka TEXT,
                                        Tarih TEXT,
                                        VergiTuru TEXT,
                                        Donem TEXT,
                                        VarlikTipi TEXT,
                                        DorseId INTEGER,
                                        DorsePlaka TEXT,
                                        Tutar REAL,
                                        Aciklama TEXT
                                    );";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateDepoTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Depolar (
                                        DepoId INTEGER PRIMARY KEY AUTOINCREMENT,
                                        DepoAdi TEXT,
                                        Aciklama TEXT
                                    );";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateSeferlerTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS Seferler (
                        SeferId INTEGER PRIMARY KEY AUTOINCREMENT,
                        KonteynerNo TEXT,
                        KonteynerBoyutu TEXT CHECK(KonteynerBoyutu IN ('20', '40')),
                        YuklemeYeri TEXT,
                        BosaltmaYeri TEXT,
                        Ekstra TEXT,
                        BosDolu TEXT,
                        Tarih TEXT,
                        Saat TEXT,
                        Fiyat REAL,
                        Kdv REAL,
                        Tevkifat REAL,
                        Aciklama TEXT,
                        CekiciId INTEGER,
                        CekiciPlaka TEXT,
                        DorseId INTEGER,
                        SoforId INTEGER,
                        SoforAdi TEXT
                    );";
                string[] requiredColumns = [
                    "Ekstra TEXT",
                    "BosDolu TEXT",
                    "Kdv REAL",
                    "Tevkifat REAL",
                    "CekiciId INTEGER",
                    "CekiciPlaka TEXT",
                    "DorseId INTEGER",
                    "SoforId INTEGER",
                    "SoforAdi TEXT"
                ];
                EnsureTable("Seferler", createScript, requiredColumns);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateGiderlerTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                string createScript = @"CREATE TABLE IF NOT EXISTS Giderler (
                GiderId INTEGER PRIMARY KEY AUTOINCREMENT,
                CekiciId INTEGER NOT NULL,
                Aciklama TEXT,
                Tutar REAL NOT NULL,
                Tarih TEXT NOT NULL
            );";
                string[] requiredColumns = [
                    "CekiciId INTEGER",
                    "Aciklama TEXT",
                    "Tutar REAL",
                    "Tarih TEXT"
                ];
                EnsureTable("Giderler", createScript, requiredColumns);
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void CheckAndCreateOrUpdateKarHesapTablosu()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var command = new SqliteCommand(@"CREATE TABLE IF NOT EXISTS KarHesap (
               KarHesapId INTEGER PRIMARY KEY AUTOINCREMENT,
               CekiciId INTEGER NOT NULL );", connection);
                command.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private static void EnsureTable(string tableName, string createScript, string[] columns)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = createScript;
            createTableCommand.ExecuteNonQuery();
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
                    { columnExists = true; break; }
                }
                if (!columnExists)
                {
                    var addColumnCommand = connection.CreateCommand();
                    addColumnCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {column};";
                    addColumnCommand.ExecuteNonQuery();
                }
            }
        }

        // -------------------- Vehicle helpers --------------------
        public static (int? cekiciId, int? dorseId, int? soforId, string? soforAdi, string? dorsePlaka) GetVehicleInfoByCekiciPlaka(string cekiciPlaka)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT C.CekiciId, C.DorseId, C.SoforId, S.SoforAdi, D.Plaka
                                     FROM Cekiciler C
                                     LEFT JOIN Soforler S ON S.SoforId = C.SoforId
                                     LEFT JOIN Dorseler D ON D.DorseId = C.DorseId
                                     WHERE C.Plaka=@p LIMIT 1";
                cmd.Parameters.AddWithValue("@p", cekiciPlaka);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    int? cekiciId = r.IsDBNull(0) ? null : r.GetInt32(0);
                    int? dorseId = r.IsDBNull(1) ? null : r.GetInt32(1);
                    int? soforId = r.IsDBNull(2) ? null : r.GetInt32(2);
                    string? soforAdi = r.IsDBNull(3) ? null : r.GetString(3);
                    string? dorsePlaka = r.IsDBNull(4) ? null : r.GetString(4);
                    return (cekiciId, dorseId, soforId, soforAdi, dorsePlaka);
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            return (null, null, null, null, null);
        }

        public static string? GetDorsePlakaByCekiciPlaka(string cekiciPlaka)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT D.Plaka FROM Cekiciler C LEFT JOIN Dorseler D ON D.DorseId=C.DorseId WHERE C.Plaka=@p LIMIT 1";
                cmd.Parameters.AddWithValue("@p", cekiciPlaka);
                var val = cmd.ExecuteScalar();
                return val == null || val is DBNull ? null : (string)val;
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); return null; }
        }

        public static List<Arac> GetAraclar()
        {
            var list = new List<Arac>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT  C.Plaka, S.SoforAdi FROM Cekiciler as C
        LEFT JOIN Soforler S ON S.SoforId = C.SoforId
        WHERE   IFNULL(C.Arsivli,0)=0
        ORDER BY C.Plaka;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Arac
                    {
                        Plaka = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        SoforAdi = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            return list;
        }

        // -------------------- Sefer CRUD --------------------
        public static int SeferEkle(Sefer s)
        {
            if (!ValidateSeferInternal(s)) return 0;
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO Seferler (KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Ekstra, BosDolu, Tarih, Saat, Fiyat, Kdv, Tevkifat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi)
                               VALUES (@KonteynerNo, @KonteynerBoyutu, @YuklemeYeri, @BosaltmaYeri, @Ekstra, @BosDolu, @Tarih, @Saat, @Fiyat, @Kdv, @Tevkifat, @Aciklama, @CekiciId, @CekiciPlaka, @DorseId, @SoforId, @SoforAdi);
                               SELECT last_insert_rowid();";
            BindSeferParams(cmd, s);
            var id = (long)cmd.ExecuteScalar()!;
            return (int)id;
        }

        public static void SeferGuncelle(Sefer s)
        {
            if (!ValidateSeferInternal(s) || s.SeferId <= 0) return;
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"UPDATE Seferler SET
                                KonteynerNo=@KonteynerNo,
                                KonteynerBoyutu=@KonteynerBoyutu,
                                YuklemeYeri=@YuklemeYeri,
                                BosaltmaYeri=@BosaltmaYeri,
                                Ekstra=@Ekstra,
                                BosDolu=@BosDolu,
                                Tarih=@Tarih,
                                Saat=@Saat,
                                Fiyat=@Fiyat,
                                Kdv=@Kdv,
                                Tevkifat=@Tevkifat,
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

        private static void BindSeferParams(SqliteCommand cmd, Sefer s)
        {
            cmd.Parameters.AddWithValue("@KonteynerNo", (object?)s.KonteynerNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@KonteynerBoyutu", (object?)s.KonteynerBoyutu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@YuklemeYeri", (object?)s.YuklemeYeri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BosaltmaYeri", (object?)s.BosaltmaYeri ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ekstra", (object?)s.Ekstra ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BosDolu", (object?)s.BosDolu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tarih", s.Tarih == default ? DateTime.Today.ToString("yyyy-MM-dd") : s.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Saat", (object?)s.Saat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Fiyat", Convert.ToDouble(s.Fiyat));
            cmd.Parameters.AddWithValue("@Kdv", Convert.ToDouble(s.Kdv));
            cmd.Parameters.AddWithValue("@Tevkifat", Convert.ToDouble(s.Tevkifat));
            cmd.Parameters.AddWithValue("@Aciklama", (object?)s.Aciklama ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CekiciId", (object?)s.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CekiciPlaka", (object?)s.CekiciPlaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DorseId", (object?)s.DorseId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SoforId", (object?)s.SoforId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SoforAdi", (object?)s.SoforAdi ?? DBNull.Value);
        }

        public static List<Sefer> GetSeferler()
        {
            var result = new List<Sefer>();
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SeferId, KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Ekstra, BosDolu, Tarih, Saat, Fiyat, Kdv, Tevkifat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi FROM Seferler ORDER BY SeferId DESC";
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
                    Ekstra = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BosDolu = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Tarih = ParseDate(reader.IsDBNull(7) ? null : reader.GetString(7)),
                    Saat = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Fiyat = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9)),
                    Kdv = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader.GetDouble(10)),
                    Tevkifat = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11)),
                    Aciklama = reader.IsDBNull(12) ? null : reader.GetString(12),
                    CekiciId = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    CekiciPlaka = reader.IsDBNull(14) ? null : reader.GetString(14),
                    DorseId = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                    SoforId = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                    SoforAdi = reader.IsDBNull(17) ? null : reader.GetString(17)
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
            cmd.CommandText = "SELECT SeferId, KonteynerNo, KonteynerBoyutu, YuklemeYeri, BosaltmaYeri, Ekstra, BosDolu, Tarih, Saat, Fiyat, Kdv, Tevkifat, Aciklama, CekiciId, CekiciPlaka, DorseId, SoforId, SoforAdi FROM Seferler WHERE CekiciPlaka = @p ORDER BY SeferId DESC";
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
                    Ekstra = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BosDolu = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Tarih = ParseDate(reader.IsDBNull(7) ? null : reader.GetString(7)),
                    Saat = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Fiyat = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9)),
                    Kdv = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader.GetDouble(10)),
                    Tevkifat = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11)),
                    Aciklama = reader.IsDBNull(12) ? null : reader.GetString(12),
                    CekiciId = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    CekiciPlaka = reader.IsDBNull(14) ? null : reader.GetString(14),
                    DorseId = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                    SoforId = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                    SoforAdi = reader.IsDBNull(17) ? null : reader.GetString(17)
                };
                result.Add(s);
            }
            return result;
        }

        // -------------------- Lookup lists --------------------
        public static List<string> GetDepoAdlari()
        {
            var list = new List<string>();
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DepoAdi FROM Depolar WHERE IFNULL(TRIM(DepoAdi),'')<>'' ORDER BY DepoAdi";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0)) list.Add(reader.GetString(0));
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            return list;
        }

        public static List<string> GetEkstraAdlari()
        {
            var list = new List<string>();
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Ad FROM EkstraUcretler WHERE IFNULL(TRIM(Ad),'')<>'' ORDER BY Ad";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0)) list.Add(reader.GetString(0));
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            return list;
        }

        // -------------------- Sofor/Cekici/Dorse schema helpers --------------------
        public static void EnsureSoforlerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(Soforler);";
                using var rdr = cmd.ExecuteReader();
                bool exists = false;
                while (rdr.Read())
                {
                    if (string.Equals(rdr.GetString(1), "Arsivli", StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Soforler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void EnsureCekicilerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(Cekiciler);";
                using var rdr = cmd.ExecuteReader();
                bool exists = false;
                while (rdr.Read())
                {
                    if (string.Equals(rdr.GetString(1), "Arsivli", StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Cekiciler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void EnsureDorselerArsivliColumn()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA table_info(Dorseler);";
                using var rdr = cmd.ExecuteReader();
                bool exists = false;
                while (rdr.Read())
                {
                    if (string.Equals(rdr.GetString(1), "Arsivli", StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (!exists)
                {
                    using var alter = connection.CreateCommand();
                    alter.CommandText = "ALTER TABLE Dorseler ADD COLUMN Arsivli INTEGER DEFAULT 0;";
                    alter.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
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
            catch (Exception ex) { Debug.WriteLine(ex.Message); return 0; }
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
            catch (Exception ex) { Debug.WriteLine(ex.Message); return 0; }
        }

        public static int RestoreMissingCekicilerFromSeferler()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                var count = 0;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Cekiciler (Plaka, Aktif, Arsivli)
                                        SELECT DISTINCT s.CekiciPlaka, 1, 0
                                        FROM Seferler s
                                        LEFT JOIN Cekiciler c ON c.Plaka = s.CekiciPlaka
                                        WHERE IFNULL(TRIM(s.CekiciPlaka),'')<>'' AND c.Plaka IS NULL";
                    count += cmd.ExecuteNonQuery();
                }
                return count;
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); return 0; }
        }

        public static int RestoreMissingDorselerFromSeferler()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Dorseler (Plaka, Arsivli)
                                    SELECT DISTINCT s.CekiciPlaka, 0 FROM Seferler s
                                    LEFT JOIN Dorseler d ON d.Plaka = s.CekiciPlaka
                                    WHERE 1=0";
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); return 0; }
        }

        public static int RestoreMissingSoforlerFromSeferler()
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Soforler (SoforAdi, Arsivli)
                                    SELECT DISTINCT s.SoforAdi, 0 FROM Seferler s
                                    LEFT JOIN Soforler d ON d.SoforAdi = s.SoforAdi
                                    WHERE IFNULL(TRIM(s.SoforAdi),'')<>'' AND d.SoforAdi IS NULL";
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); return 0; }
        }

        // -------------------- Yakit Giderleri --------------------
        public static void CheckAndCreateOrUpdateYakitGiderTablosu()
        {
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS YakitGiderleri (
                                        YakitId INTEGER PRIMARY KEY AUTOINCREMENT,
                                        CekiciId INTEGER,
                                        Plaka TEXT,
                                        Tarih TEXT,
                                        Istasyon TEXT,
                                        Litre REAL,
                                        BirimFiyat REAL,
                                        Tutar REAL,
                                        Km INTEGER,
                                        Aciklama TEXT);";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static List<YakitGider> GetYakitGiderleri(int? cekiciId, DateTime? bas, DateTime? bit)
        {
            var list = new List<YakitGider>();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            var sql = "SELECT YakitId, CekiciId, Plaka, Tarih, Istasyon, Litre, BirimFiyat, Tutar, Km, Aciklama FROM YakitGiderleri WHERE 1=1";
            using var cmd = conn.CreateCommand();
            if (cekiciId.HasValue) { sql += " AND (CekiciId = @id OR CekiciId IS NULL)"; cmd.Parameters.AddWithValue("@id", cekiciId.Value); }
            if (bas.HasValue) { sql += " AND Tarih >= @bas"; cmd.Parameters.AddWithValue("@bas", bas.Value.ToString("yyyy-MM-dd")); }
            if (bit.HasValue) { sql += " AND Tarih <= @bit"; cmd.Parameters.AddWithValue("@bit", bit.Value.ToString("yyyy-MM-dd")); }
            sql += " ORDER BY Tarih DESC, YakitId DESC";
            cmd.CommandText = sql;
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new YakitGider
                {
                    YakitId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                    CekiciId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                    Plaka = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    Tarih = DateTime.TryParse(rdr.IsDBNull(3) ? null : rdr.GetString(3), out var dt) ? dt : DateTime.Today,
                    Istasyon = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                    Litre = rdr.IsDBNull(5) ? 0 : Convert.ToDecimal(rdr.GetDouble(5)),
                    BirimFiyat = rdr.IsDBNull(6) ? 0 : Convert.ToDecimal(rdr.GetDouble(6)),
                    Tutar = rdr.IsDBNull(7) ? 0 : Convert.ToDecimal(rdr.GetDouble(7)),
                    Km = rdr.IsDBNull(8) ? null : rdr.GetInt32(8),
                    Aciklama = rdr.IsDBNull(9) ? null : rdr.GetString(9)
                });
            }
            return list;
        }

        public static int YakitEkle(YakitGider y)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO YakitGiderleri (CekiciId, Plaka, Tarih, Istasyon, Litre, BirimFiyat, Tutar, Km, Aciklama)
                                VALUES (@cid,@p,@t,@i,@l,@b,@u,@k,@a); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid", (object?)y.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", (object?)y.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", y.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue(",@i", (object?)y.Istasyon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@l", Convert.ToDouble(y.Litre));
            cmd.Parameters.AddWithValue("@b", Convert.ToDouble(y.BirimFiyat));
            cmd.Parameters.AddWithValue("@u", Convert.ToDouble(y.Tutar));
            cmd.Parameters.AddWithValue("@k", (object?)y.Km ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", (object?)y.Aciklama ?? DBNull.Value);
            var id = (long)cmd.ExecuteScalar()!; return (int)id;
        }

        public static void YakitGuncelle(YakitGider y)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE YakitGiderleri SET CekiciId=@cid, Plaka=@p, Tarih=@t, Istasyon=@i, Litre=@l, BirimFiyat=@b, Tutar=@u, Km=@k, Aciklama=@a WHERE YakitId=@id";
            cmd.Parameters.AddWithValue("@cid", (object?)y.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", (object?)y.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", y.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@i", (object?)y.Istasyon ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@l", Convert.ToDouble(y.Litre));
            cmd.Parameters.AddWithValue("@b", Convert.ToDouble(y.BirimFiyat));
            cmd.Parameters.AddWithValue("@u", Convert.ToDouble(y.Tutar));
            cmd.Parameters.AddWithValue("@k", (object?)y.Km ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", (object?)y.Aciklama ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", y.YakitId);
            cmd.ExecuteNonQuery();
        }

        public static void YakitSil(int id)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM YakitGiderleri WHERE YakitId=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // -------------------- Genel Giderler --------------------
        public static List<GenelGider> GetGenelGiderleri(int? cekiciId, DateTime? bas, DateTime? bit)
        {
            var list = new List<GenelGider>();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            var sql = "SELECT GiderId, CekiciId, Plaka, Tarih, Tutar, Aciklama FROM GenelGiderler WHERE 1=1";
            using var cmd = conn.CreateCommand();
            if (cekiciId.HasValue) { sql += " AND (CekiciId = @id OR CekiciId IS NULL)"; cmd.Parameters.AddWithValue("@id", cekiciId.Value); }
            if (bas.HasValue) { sql += " AND Tarih >= @bas"; cmd.Parameters.AddWithValue("@bas", bas.Value.ToString("yyyy-MM-dd")); }
            if (bit.HasValue) { sql += " AND Tarih <= @bit"; cmd.Parameters.AddWithValue("@bit", bit.Value.ToString("yyyy-MM-dd")); }
            sql += " ORDER BY Tarih DESC, GiderId DESC";
            cmd.CommandText = sql;
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new GenelGider
                {
                    GiderId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                    CekiciId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                    Plaka = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    Tarih = DateTime.TryParse(rdr.IsDBNull(3) ? null : rdr.GetString(3), out var dt) ? dt : DateTime.Today,
                    Tutar = rdr.IsDBNull(4) ? 0 : Convert.ToDecimal(rdr.GetDouble(4)),
                    Aciklama = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                });
            }
            return list;
        }

        public static int GenelGiderEkle(GenelGider g)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO GenelGiderler (CekiciId, Plaka, Tarih, Tutar, Aciklama)
                                VALUES (@cid,@p,@t,@u,@a); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@cid", (object?)g.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", (object?)g.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", g.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@u", Convert.ToDouble(g.Tutar));
            cmd.Parameters.AddWithValue("@a", (object?)g.Aciklama ?? DBNull.Value);
            var id = (long)cmd.ExecuteScalar()!; return (int)id;
        }

        public static void GenelGiderGuncelle(GenelGider g)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE GenelGiderler SET CekiciId=@cid, Plaka=@p, Tarih=@t, Tutar=@u, Aciklama=@a WHERE GiderId=@id";
            cmd.Parameters.AddWithValue("@cid", (object?)g.CekiciId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", (object?)g.Plaka ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", g.Tarih.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@u", Convert.ToDouble(g.Tutar));
            cmd.Parameters.AddWithValue("@a", (object?)g.Aciklama ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", g.GiderId);
            cmd.ExecuteNonQuery();
        }

        public static void GenelGiderSil(int id)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GenelGiderler WHERE GiderId=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // -------------------- Stubs for screens not implemented here --------------------
        public static void CheckAndCreateOrUpdateSanaiGiderTablosu() { }
        public static List<SanaiGider> GetSanaiGiderleri(int? cekiciId, DateTime? bas, DateTime? bit) => new();
        public static int SanaiGiderEkle(SanaiGider s) => 0;
        public static void SanaiGiderGuncelle(SanaiGider s) { }
        public static void SanaiGiderSil(int id) { }

        public static List<PersonelGider> GetPersonelGiderleri(int? cekiciId, DateTime? bas, DateTime? bit) => new();
        public static int PersonelGiderEkle(PersonelGider p) => 0;
        public static void PersonelGiderGuncelle(PersonelGider p) { }
        public static void PersonelGiderSil(int id) { }

        public static List<VergiArac> GetVergiAraclari(int? cekiciId, DateTime? bas, DateTime? bit) => new();
        public static int VergiAracEkle(VergiArac v) => 0;
        public static void VergiAracGuncelle(VergiArac v) { }
        public static void VergiAracSil(int id) { }

        // -------------------- Domain Seeds --------------------
        public static void SeedGivenGuzergahlar()
        {
            EnsureDatabaseFileStatic();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var routes = new (string from, string to, decimal fiyat)[]
                {
                    ("ARDEP", "LİMAN", 1400m),
                    ("DEMİRELLER", "LİMAN", 1650m),
                    ("KAHRAMANLI", "LİMAN", 1650m),
                    ("NİSA", "LİMAN", 1400m),
                    ("OSG", "LİMAN", 1300m),
                    ("FALCON", "LİMAN", 1400m)
                };

                foreach (var (fromName, toName, price) in routes)
                {
                    int? cId = TryGetDepoIdByName(conn, fromName);
                    int? vId = TryGetDepoIdByName(conn, toName);
                    if (cId == null || vId == null) continue; // don't recreate deleted depots

                    using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = @"UPDATE Guzergahlar
                                          SET Fiyat=@p, Aciklama='', OtomatikMi=0
                                        WHERE CikisDepoId=@c AND VarisDepoId=@v";
                    upd.Parameters.AddWithValue("@p", Convert.ToDouble(price));
                    upd.Parameters.AddWithValue("@c", cId.Value);
                    upd.Parameters.AddWithValue("@v", vId.Value);
                    int affected = upd.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        using var ins = conn.CreateCommand();
                        ins.Transaction = tx;
                        ins.CommandText = @"INSERT OR IGNORE INTO Guzergahlar (CikisDepoId, VarisDepoId, Fiyat, Aciklama, OtomatikMi)
                                              VALUES (@c, @v, @p, '', 0)";
                        ins.Parameters.AddWithValue("@c", cId.Value);
                        ins.Parameters.AddWithValue("@v", vId.Value);
                        ins.Parameters.AddWithValue("@p", Convert.ToDouble(price));
                        ins.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine("[SeedGivenGuzergahlar] error: " + ex.Message);
            }
        }
    }
}
