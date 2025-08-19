using Microsoft.Data.Sqlite;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Services; // EnsureSoforlerArsivliColumn için

namespace TirSeferleriModernApp.Views
{
    public partial class TanimlamaView : UserControl
    {
        private const string ConnectionString = "Data Source=TirSeferleri.db";
        private bool _arsivGosteriliyor = false; // Sofor listesi görünümü (aktif/arsiv)

        private enum Tables
        {
            Cekiciler,
            Dorseler,
            Soforler
        }

        public TanimlamaView()
        {
            InitializeComponent();
            // Soforler tablosunda Arsivli kolonu yoksa ekle
            DatabaseService.EnsureSoforlerArsivliColumn();
            LoadData();
            LoadDorseler(); // Dorseler ComboBox için yükleniyor
            LoadSoforler(); // Soforler ComboBox için yükleniyor
        }

        private void LoadData()
        {
            try
            {
                foreach (var table in Enum.GetValues<Tables>())
                {
                    var dataGrid = GetDataGridForTable(table);
                    if (dataGrid != null)
                    {
                        dataGrid.ItemsSource = LoadTable(table.ToString()).DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veri yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataTable LoadTable(string tableName)
        {
            var dataTable = new DataTable();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string query = tableName switch
                {
                    "Cekiciler" => @"
                SELECT 
                    C.CekiciId, 
                    C.Plaka, 
                    S.SoforAdi AS SoforAd, 
                    D.Plaka AS DorsePlaka, 
                    C.Aktif 
                FROM Cekiciler C
                LEFT JOIN Soforler S ON C.SoforId = S.SoforId
                LEFT JOIN Dorseler D ON C.DorseId = D.DorseId",
                    // Soförler listesi toggle (aktif/arsivli)
                    "Soforler" => _arsivGosteriliyor
                        ? "SELECT SoforId, SoforAdi, Telefon FROM Soforler WHERE IFNULL(Arsivli,0)=1"
                        : "SELECT SoforId, SoforAdi, Telefon FROM Soforler WHERE IFNULL(Arsivli,0)=0",
                    _ => $"SELECT * FROM {tableName}"
                };

                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();
                dataTable.Load(reader);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tablo yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return dataTable;
        }

        private void BtnSoforArsiv_Click(object sender, RoutedEventArgs e)
        {
            _arsivGosteriliyor = !_arsivGosteriliyor;
            if (FindName("btnSoforArsiv") is Button b)
                b.Content = _arsivGosteriliyor ? "Aktifler" : "Arşiv";
            LoadData();
        }

        private void Sil_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTable = GetSelectedTable();
                if (selectedTable == null)
                {
                    MessageBox.Show("Lütfen silmek istediğiniz tabloyu seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dataGrid = GetDataGridForTable(selectedTable.Value);
                if (dataGrid?.SelectedItem is DataRowView selectedRow)
                {
                    var idColumnName = GetIdColumnNameForTable(selectedTable.Value);
                    var id = selectedRow[idColumnName]?.ToString();

                    if (string.IsNullOrEmpty(id))
                    {
                        MessageBox.Show("Seçili satırda ID bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (selectedTable == Tables.Soforler)
                    {
                        if (_arsivGosteriliyor)
                        {
                            // Arşiv görünümündeyken kalıcı silme denemesi -> önce sefer kontrolü
                            var ok = TryHardDeleteDriver(id!);
                            if (ok)
                            {
                                dataGrid.SelectedItem = null;
                                LoadData();
                            }
                        }
                        else
                        {
                            // Aktif listede: arşivle
                            var result = MessageBox.Show("Seçili şoförü listeden kaldırıp arşive taşımak istiyor musunuz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (result == MessageBoxResult.Yes)
                            {
                                ArchiveDriver(id!);
                                dataGrid.SelectedItem = null;
                                LoadData();
                            }
                        }
                        return;
                    }

                    // Diğer tablolar: eski davranış
                    var confirm = MessageBox.Show("Bu kaydı silmek istiyor musunuz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        DeleteRowInternal(selectedTable.Value.ToString(), idColumnName, id!);
                        dataGrid.SelectedItem = null;
                        LoadData();
                    }
                }
                else
                {
                    MessageBox.Show("Lütfen silmek istediğiniz satırı seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme işlemi sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void DeleteRowInternal(string tableName, string idColumnName, string id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = new SqliteCommand($"DELETE FROM {tableName} WHERE {idColumnName} = @Id", connection);
                command.Parameters.AddWithValue("@Id", id);
                command.ExecuteNonQuery();

                MessageBox.Show("Silme işlemi başarıyla tamamlandı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Silme işlemi sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ArchiveDriver(string id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var cmd = new SqliteCommand("UPDATE Soforler SET Arsivli = 1 WHERE SoforId = @Id", connection);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
                MessageBox.Show("Şoför arşive taşındı (listeden kaldırıldı).", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Arşivleme sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Arşivden kalıcı silme işlemi
        public static bool TryHardDeleteDriver(string id)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var check = new SqliteCommand("SELECT COUNT(1) FROM Seferler WHERE SoforId = @Id", connection);
                check.Parameters.AddWithValue("@Id", id);
                var count = Convert.ToInt32(check.ExecuteScalar());
                if (count > 0)
                {
                    MessageBox.Show("Bu şoföre ait sefer kayıtları var. Önce o seferleri silin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                using var del = new SqliteCommand("DELETE FROM Soforler WHERE SoforId = @Id", connection);
                del.Parameters.AddWithValue("@Id", id);
                del.ExecuteNonQuery();
                MessageBox.Show("Şoför kalıcı olarak silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kalıcı silme sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private Tables? GetSelectedTable()
        {
            if (dgCekiciler.SelectedItem != null) return Tables.Cekiciler;
            if (dgDorseler.SelectedItem != null) return Tables.Dorseler;
            if (dgSoforler.SelectedItem != null) return Tables.Soforler;
            return null;
        }

        private DataGrid GetDataGridForTable(Tables table)
        {
            return table switch
            {
                Tables.Cekiciler => dgCekiciler,
                Tables.Dorseler => dgDorseler,
                Tables.Soforler => dgSoforler,
                _ => throw new ArgumentException("Geçersiz tablo adı.")
            };
        }

        private static string GetIdColumnNameForTable(Tables table)
        {
            return table switch
            {
                Tables.Cekiciler => "CekiciId",
                Tables.Dorseler => "DorseId",
                Tables.Soforler => "SoforId",
                _ => throw new ArgumentException("Geçersiz tablo adı.")
            };
        }

        // 🔽 Kaydet Butonları (orijinal metotlar)
        private void BtnCekiciKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = new SqliteCommand(@"
                    INSERT INTO Cekiciler (Plaka, SoforId, DorseId, Aktif) 
                    VALUES (@Plaka, @SoforId, @DorseId, @Aktif)", connection);

                command.Parameters.AddWithValue("@Plaka", txtCekiciPlaka.Text.Trim());
                command.Parameters.AddWithValue("@SoforId", cmbSoforId.SelectedValue ?? DBNull.Value);
                command.Parameters.AddWithValue("@DorseId", cmbDorseId.SelectedValue ?? DBNull.Value);
                command.Parameters.AddWithValue("@Aktif", chkAktifMi.IsChecked == true ? 1 : 0);

                command.ExecuteNonQuery();
                MessageBox.Show("Çekici başarıyla eklendi.");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Kayıt Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDorseKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = new SqliteCommand(@"
                    INSERT INTO Dorseler (Plaka, Tip) 
                    VALUES (@Plaka, @Tip)", connection);

                command.Parameters.AddWithValue("@Plaka", txtDorsePlaka.Text.Trim());
                command.Parameters.AddWithValue("@Tip", "Standard");

                command.ExecuteNonQuery();
                MessageBox.Show("Dorse başarıyla eklendi.");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Kayıt Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSoforKaydet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = new SqliteCommand(@"
                    INSERT INTO Soforler (SoforAdi, Telefon) 
                    VALUES (@SoforAdi, @Telefon)", connection);

                command.Parameters.AddWithValue("@SoforAdi", txtSoforAd.Text.Trim());
                command.Parameters.AddWithValue("@Telefon", "");

                command.ExecuteNonQuery();
                MessageBox.Show("Şoför başarıyla eklendi.");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Kayıt Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCekiciGuncelle_Click(object sender, RoutedEventArgs e) { }
        private void BtnDorseGuncelle_Click(object sender, RoutedEventArgs e) { }
        private void BtnSoforGuncelle_Click(object sender, RoutedEventArgs e) { }

        private void LoadDorseler()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string query = "SELECT DorseId, Plaka FROM Dorseler";
                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();

                var dorseler = new List<Dorse>();
                while (reader.Read())
                {
                    dorseler.Add(new Dorse { DorseId = reader.GetInt32(0), Plaka = reader.GetString(1) });
                }

                cmbDorseId.ItemsSource = dorseler;
                cmbDorseId.DisplayMemberPath = "Plaka";
                cmbDorseId.SelectedValuePath = "DorseId";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dorseler yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSoforler()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                string query = "SELECT SoforId, SoforAdi FROM Soforler WHERE IFNULL(Arsivli,0)=0";
                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();

                var soforler = new List<Sofor>();
                while (reader.Read())
                {
                    soforler.Add(new Sofor { SoforId = reader.GetInt32(0), SoforAdi = reader.GetString(1) });
                }

                cmbSoforId.ItemsSource = soforler;
                cmbSoforId.DisplayMemberPath = "SoforAdi";
                cmbSoforId.SelectedValuePath = "SoforId";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şoförler yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Dorse { public int DorseId { get; set; } public string Plaka { get; set; } = string.Empty; }
    public class Sofor { public int SoforId { get; set; } public string SoforAdi { get; set; } = string.Empty; }
}
