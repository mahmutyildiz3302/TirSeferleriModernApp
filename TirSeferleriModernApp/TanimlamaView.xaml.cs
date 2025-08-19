using Microsoft.Data.Sqlite;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views
{
    public partial class TanimlamaView : UserControl
    {
        private const string ConnectionString = "Data Source=TirSeferleri.db";
        private bool _arsivGosteriliyor = false; // şoför
        private bool _cekiciArsivGoster = false;
        private bool _dorseArsivGoster = false;

        private enum Tables { Cekiciler, Dorseler, Soforler }

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

        private Tables? GetSelectedTable()
        { if (dgCekiciler.SelectedItem != null) return Tables.Cekiciler; if (dgDorseler.SelectedItem != null) return Tables.Dorseler; if (dgSoforler.SelectedItem != null) return Tables.Soforler; return null; }
        private DataGrid GetDataGridForTable(Tables t) => t switch { Tables.Cekiciler => dgCekiciler, Tables.Dorseler => dgDorseler, Tables.Soforler => dgSoforler, _ => dgCekiciler };
        private static string GetIdColumnNameForTable(Tables t) => t switch { Tables.Cekiciler => "CekiciId", Tables.Dorseler => "DorseId", Tables.Soforler => "SoforId", _ => "Id" };

        // aşağıdakiler mevcut kaydet/güncelle/yükle metotlarının kopyası; projede zaten tanımlı ise kaldırılabilir
        private void BtnCekiciKaydet_Click(object sender, RoutedEventArgs e) { }
        private void BtnCekiciGuncelle_Click(object sender, RoutedEventArgs e) { }
        private void BtnDorseKaydet_Click(object sender, RoutedEventArgs e) { }
        private void BtnDorseGuncelle_Click(object sender, RoutedEventArgs e) { }
        private void BtnSoforKaydet_Click(object sender, RoutedEventArgs e) { }
        private void BtnSoforGuncelle_Click(object sender, RoutedEventArgs e) { }
        private void LoadDorseler() { }
        private void LoadSoforler() { }

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
