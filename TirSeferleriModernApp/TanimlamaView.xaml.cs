using Microsoft.Data.Sqlite;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Services;
using System.Collections.Generic;

namespace TirSeferleriModernApp.Views
{
    public partial class TanimlamaView : UserControl
    {
        private const string ConnectionString = "Data Source=TirSeferleri.db";
        private bool _arsivGosteriliyor = false; // şoför
        private bool _cekiciArsivGoster = false;
        private bool _dorseArsivGoster = false;

        private enum Tables { Cekiciler, Dorseler, Soforler }

        // Basit taşıyıcı tipler (ComboBox kaynakları)
        private class DorseItem { public int DorseId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private class SoforItem { public int SoforId { get; set; } public string SoforAdi { get; set; } = string.Empty; }

        public TanimlamaView()
        {
            InitializeComponent();
            DatabaseService.EnsureSoforlerArsivliColumn();
            DatabaseService.EnsureCekicilerArsivliColumn();
            DatabaseService.EnsureDorselerArsivliColumn();
            LoadData();
            LoadDorseler();
            LoadSoforler();
        }

        private void LoadData()
        {
            dgCekiciler.ItemsSource = LoadTable("Cekiciler").DefaultView;
            dgDorseler.ItemsSource = LoadTable("Dorseler").DefaultView;
            dgSoforler.ItemsSource = LoadTable("Soforler").DefaultView;
        }

        private DataTable LoadTable(string tableName)
        {
            var dt = new DataTable();
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            string query;
            if (tableName == "Cekiciler")
                query = _cekiciArsivGoster ?
                    "SELECT CekiciId, Plaka, Aktif, SoforId, DorseId FROM Cekiciler WHERE IFNULL(Arsivli,0)=1" :
                    "SELECT C.CekiciId, C.Plaka, S.SoforAdi AS SoforAd, D.Plaka AS DorsePlaka, C.Aktif FROM Cekiciler C LEFT JOIN Soforler S ON C.SoforId=S.SoforId LEFT JOIN Dorseler D ON C.DorseId=D.DorseId WHERE IFNULL(C.Arsivli,0)=0";
            else if (tableName == "Dorseler")
                query = _dorseArsivGoster ?
                    "SELECT DorseId, Plaka, Tip FROM Dorseler WHERE IFNULL(Arsivli,0)=1" :
                    "SELECT DorseId, Plaka, Tip FROM Dorseler WHERE IFNULL(Arsivli,0)=0";
            else if (tableName == "Soforler")
                query = _arsivGosteriliyor ?
                    "SELECT SoforId, SoforAdi, Telefon FROM Soforler WHERE IFNULL(Arsivli,0)=1" :
                    "SELECT SoforId, SoforAdi, Telefon FROM Soforler WHERE IFNULL(Arsivli,0)=0";
            else query = $"SELECT * FROM {tableName}";
            using var cmd = new SqliteCommand(query, conn);
            using var rdr = cmd.ExecuteReader();
            dt.Load(rdr);
            return dt;
        }

        private void BtnCekiciArsiv_Click(object sender, RoutedEventArgs e)
        { _cekiciArsivGoster = !_cekiciArsivGoster; if (sender is Button b) b.Content = _cekiciArsivGoster ? "Aktifler" : "Arşiv"; LoadData(); }
        private void BtnDorseArsiv_Click(object sender, RoutedEventArgs e)
        { _dorseArsivGoster = !_dorseArsivGoster; if (sender is Button b) b.Content = _dorseArsivGoster ? "Aktifler" : "Arşiv"; LoadData(); }
        private void BtnSoforArsiv_Click(object sender, RoutedEventArgs e)
        { _arsivGosteriliyor = !_arsivGosteriliyor; if (FindName("btnSoforArsiv") is Button b) b.Content = _arsivGosteriliyor ? "Aktifler" : "Arşiv"; LoadData(); }

        private void Sil_Click(object sender, RoutedEventArgs e)
        {
            var table = GetSelectedTable(); if (table == null) { MessageBox.Show("Tablo seçin"); return; }
            var grid = GetDataGridForTable(table.Value); if (grid?.SelectedItem is not DataRowView row) { MessageBox.Show("Satır seçin"); return; }
            var idName = GetIdColumnNameForTable(table.Value); var id = row[idName]?.ToString(); if (string.IsNullOrEmpty(id)) { MessageBox.Show("ID yok"); return; }

            if (table == Tables.Soforler)
            {
                if (_arsivGosteriliyor) { if (!TryHardDeleteDriver(id!)) return; }
                else { Archive("Soforler", id!); }
                LoadData(); return;
            }
            if (table == Tables.Cekiciler)
            {
                if (_cekiciArsivGoster)
                {
                    if (int.TryParse(id, out var cekiciId) && DatabaseService.GetSeferCountByCekiciId(cekiciId) > 0) { MessageBox.Show("Bu çekiciye ait sefer kayıtları var. Önce o seferleri silin."); return; }
                    DeleteRowInternal("Cekiciler", idName, id!);
                }
                else { Archive("Cekiciler", id!); }
                LoadData(); return;
            }
            if (table == Tables.Dorseler)
            {
                if (_dorseArsivGoster)
                {
                    if (int.TryParse(id, out var dorseId) && DatabaseService.GetSeferCountByDorseId(dorseId) > 0) { MessageBox.Show("Bu dorsenin sefer kaydı var. Önce o seferleri silin."); return; }
                    DeleteRowInternal("Dorseler", idName, id!);
                }
                else { Archive("Dorseler", id!); }
                LoadData(); return;
            }
        }

        private static void Archive(string table, string id)
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            var idCol = table switch { "Cekiciler" => "CekiciId", "Dorseler" => "DorseId", "Soforler" => "SoforId", _ => "Id" };
            using var cmd = new SqliteCommand($"UPDATE {table} SET Arsivli = 1 WHERE {idCol} = @id", conn);
            cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
        }

        private void BtnCekiciEksikleriniDuzelt_Click(object sender, RoutedEventArgs e)
        { var n = DatabaseService.RestoreMissingCekicilerFromSeferler(); MessageBox.Show(n > 0 ? $"{n} çekici geri getirildi." : "Eksik çekici bulunamadı."); LoadData(); }
        private void BtnDorseEksikleriniDuzelt_Click(object sender, RoutedEventArgs e)
        { var n = DatabaseService.RestoreMissingDorselerFromSeferler(); MessageBox.Show(n > 0 ? $"{n} dorse geri getirildi." : "Eksik dorse bulunamadı."); LoadData(); }
        private void BtnSoforEksikleriniDuzelt_Click(object sender, RoutedEventArgs e)
        { var n = DatabaseService.RestoreMissingSoforlerFromSeferler(); MessageBox.Show(n > 0 ? $"{n} şoför geri getirildi." : "Eksik şoför bulunamadı."); LoadData(); }

        private Tables? GetSelectedTable()
        { if (dgCekiciler.SelectedItem != null) return Tables.Cekiciler; if (dgDorseler.SelectedItem != null) return Tables.Dorseler; if (dgSoforler.SelectedItem != null) return Tables.Soforler; return null; }
        private DataGrid GetDataGridForTable(Tables t) => t switch { Tables.Cekiciler => dgCekiciler, Tables.Dorseler => dgDorseler, Tables.Soforler => dgSoforler, _ => dgCekiciler };
        private static string GetIdColumnNameForTable(Tables t) => t switch { Tables.Cekiciler => "CekiciId", Tables.Dorseler => "DorseId", Tables.Soforler => "SoforId", _ => "Id" };

        // Bölüm bazlı KAYDET/GÜNCELLE işlevleri
        private void BtnCekiciKaydet_Click(object sender, RoutedEventArgs e)
        {
            var plaka = txtCekiciPlaka.Text?.Trim();
            if (string.IsNullOrWhiteSpace(plaka)) { MessageBox.Show("Plaka girin"); return; }
            var soforId = cmbSoforId.SelectedValue as int?;
            var dorseId = cmbDorseId.SelectedValue as int?;
            var aktif = chkAktifMi.IsChecked == true ? 1 : 0;
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("INSERT INTO Cekiciler (Plaka, SoforId, DorseId, Aktif, Arsivli) VALUES (@p,@s,@d,@a,0)", conn);
            cmd.Parameters.AddWithValue("@p", plaka);
            cmd.Parameters.AddWithValue("@s", (object?)soforId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)dorseId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@a", aktif);
            cmd.ExecuteNonQuery();
            LoadData();
        }

        private void BtnCekiciGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dgCekiciler.SelectedItem is not DataRowView row) { MessageBox.Show("Güncellenecek çekiciyi listeden seçin"); return; }
            var id = row["CekiciId"]?.ToString(); if (string.IsNullOrEmpty(id)) { MessageBox.Show("ID bulunamadı"); return; }
            var plaka = txtCekiciPlaka.Text?.Trim();
            var soforId = cmbSoforId.SelectedValue as int?;
            var dorseId = cmbDorseId.SelectedValue as int?;
            var aktif = chkAktifMi.IsChecked == true ? 1 : 0;
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Cekiciler SET Plaka=@p, SoforId=@s, DorseId=@d, Aktif=@a WHERE CekiciId=@id", conn);
            cmd.Parameters.AddWithValue("@p", (object?)plaka ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@s", (object?)soforId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@d", (object?)dorseId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@a", aktif);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadData();
        }

        private void BtnDorseKaydet_Click(object sender, RoutedEventArgs e)
        {
            var plaka = txtDorsePlaka.Text?.Trim(); if (string.IsNullOrWhiteSpace(plaka)) { MessageBox.Show("Dorse plakası girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("INSERT INTO Dorseler (Plaka, Tip, Arsivli) VALUES (@p,'Standard',0)", conn);
            cmd.Parameters.AddWithValue("@p", plaka);
            cmd.ExecuteNonQuery();
            LoadData(); LoadDorseler();
        }

        private void BtnDorseGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dgDorseler.SelectedItem is not DataRowView row) { MessageBox.Show("Güncellenecek dorsayı listeden seçin"); return; }
            var id = row["DorseId"]?.ToString(); if (string.IsNullOrEmpty(id)) { MessageBox.Show("ID bulunamadı"); return; }
            var plaka = txtDorsePlaka.Text?.Trim(); if (string.IsNullOrWhiteSpace(plaka)) { MessageBox.Show("Dorse plakası girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Dorseler SET Plaka=@p WHERE DorseId=@id", conn);
            cmd.Parameters.AddWithValue("@p", plaka);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadData(); LoadDorseler();
        }

        private void BtnSoforKaydet_Click(object sender, RoutedEventArgs e)
        {
            var ad = txtSoforAd.Text?.Trim(); if (string.IsNullOrWhiteSpace(ad)) { MessageBox.Show("Şoför adı girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("INSERT INTO Soforler (SoforAdi, Telefon, Arsivli) VALUES (@a,'',0)", conn);
            cmd.Parameters.AddWithValue("@a", ad);
            cmd.ExecuteNonQuery();
            LoadData(); LoadSoforler();
        }

        private void BtnSoforGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (dgSoforler.SelectedItem is not DataRowView row) { MessageBox.Show("Güncellenecek şoförü listeden seçin"); return; }
            var id = row["SoforId"]?.ToString(); if (string.IsNullOrEmpty(id)) { MessageBox.Show("ID bulunamadı"); return; }
            var ad = txtSoforAd.Text?.Trim(); if (string.IsNullOrWhiteSpace(ad)) { MessageBox.Show("Şoför adı girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Soforler SET SoforAdi=@a WHERE SoforId=@id", conn);
            cmd.Parameters.AddWithValue("@a", ad);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadData(); LoadSoforler();
        }

        private void LoadDorseler()
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("SELECT DorseId, Plaka FROM Dorseler WHERE IFNULL(Arsivli,0)=0 ORDER BY Plaka", conn);
            using var rdr = cmd.ExecuteReader();
            var list = new List<DorseItem>();
            while (rdr.Read()) list.Add(new DorseItem { DorseId = rdr.GetInt32(0), Plaka = rdr.GetString(1) });
            cmbDorseId.ItemsSource = list; cmbDorseId.DisplayMemberPath = nameof(DorseItem.Plaka); cmbDorseId.SelectedValuePath = nameof(DorseItem.DorseId);
        }

        private void LoadSoforler()
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("SELECT SoforId, SoforAdi FROM Soforler WHERE IFNULL(Arsivli,0)=0 ORDER BY SoforAdi", conn);
            using var rdr = cmd.ExecuteReader();
            var list = new List<SoforItem>();
            while (rdr.Read()) list.Add(new SoforItem { SoforId = rdr.GetInt32(0), SoforAdi = rdr.GetString(1) });
            cmbSoforId.ItemsSource = list; cmbSoforId.DisplayMemberPath = nameof(SoforItem.SoforAdi); cmbSoforId.SelectedValuePath = nameof(SoforItem.SoforId);
        }

        // Soför kalıcı silme kontrolü
        public static bool TryHardDeleteDriver(string id)
        {
            try
            {
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var check = new SqliteCommand("SELECT COUNT(1) FROM Seferler WHERE SoforId = @Id", conn);
                check.Parameters.AddWithValue("@Id", id);
                var count = Convert.ToInt32(check.ExecuteScalar());
                if (count > 0) { MessageBox.Show("Bu şoföre ait sefer kayıtları var. Önce o seferleri silin."); return false; }
                using var del = new SqliteCommand("DELETE FROM Soforler WHERE SoforId = @Id", conn);
                del.Parameters.AddWithValue("@Id", id); del.ExecuteNonQuery();
                MessageBox.Show("Şoför kalıcı olarak silindi.");
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return false; }
        }

        private static void DeleteRowInternal(string tableName, string idColumnName, string id)
        {
            using var conn = new SqliteConnection("Data Source=TirSeferleri.db");
            conn.Open();
            using var cmd = new SqliteCommand($"DELETE FROM {tableName} WHERE {idColumnName} = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
    }
}
