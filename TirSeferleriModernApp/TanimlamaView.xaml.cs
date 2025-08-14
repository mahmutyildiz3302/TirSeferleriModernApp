using Microsoft.Data.Sqlite;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace TirSeferleriModernApp.Views
{
    public partial class TanimlamaView : UserControl
    {
        private const string ConnectionString = "Data Source=TirSeferleri.db";

        private enum Tables
        {
            Cekiciler,
            Dorseler,
            Soforler
        }

        public TanimlamaView()
        {
            InitializeComponent();
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

        private static DataTable LoadTable(string tableName)
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

                    var result = MessageBox.Show("Bu kaydı silmek istiyor musunuz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        DeleteRow(selectedTable.Value.ToString(), idColumnName, id);
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

        private static void DeleteRow(string tableName, string idColumnName, string id)
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
                _ => throw new ArgumentException("Geçersiz tablo adı.") // Null dönüşü yerine hata fırlatılıyor
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

        // 🔽 Kaydet Butonları

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
                command.Parameters.AddWithValue("@SoforId", cmbSoforId.SelectedValue ?? DBNull.Value); // Seçilen SoforId
                command.Parameters.AddWithValue("@DorseId", cmbDorseId.SelectedValue ?? DBNull.Value); // Seçilen DorseId
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
                command.Parameters.AddWithValue("@Tip", "Standard"); // Geliştirilebilir

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
                command.Parameters.AddWithValue("@Telefon", ""); // İleride genişletilebilir

                command.ExecuteNonQuery();
                MessageBox.Show("Şoför başarıyla eklendi.");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Kayıt Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCekiciGuncelle_Click(object sender, RoutedEventArgs e)
        {
            // Güncelleme işlemi burada yapılmalı
        }

        private void BtnDorseGuncelle_Click(object sender, RoutedEventArgs e)
        {
            // Güncelleme işlemi burada yapılmalı
        }

        private void BtnSoforGuncelle_Click(object sender, RoutedEventArgs e)
        {
            // Güncelleme işlemi burada yapılmalı
        }


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
                    dorseler.Add(new Dorse
                    {
                        DorseId = reader.GetInt32(0),
                        Plaka = reader.GetString(1)
                    });
                }

                cmbDorseId.ItemsSource = dorseler;
                cmbDorseId.DisplayMemberPath = "Plaka"; // Görünen değer
                cmbDorseId.SelectedValuePath = "DorseId"; // Seçilen değer
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

                string query = "SELECT SoforId, SoforAdi FROM Soforler";
                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();

                var soforler = new List<Sofor>();
                while (reader.Read())
                {
                    soforler.Add(new Sofor
                    {
                        SoforId = reader.GetInt32(0),
                        SoforAdi = reader.GetString(1)
                    });
                }

                cmbSoforId.ItemsSource = soforler;
                cmbSoforId.DisplayMemberPath = "SoforAdi"; // Görünen değer
                cmbSoforId.SelectedValuePath = "SoforId"; // Seçilen değer
            }
            catch (Exception ex)
            {   
                MessageBox.Show($"Şoförler yüklenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Dorse
    {
        public int DorseId { get; set; }
        public string Plaka { get; set; } = string.Empty; // Varsayılan boş değerle başlatıldı
    }

    public class Sofor
    {
        public int SoforId { get; set; }
        public string SoforAdi { get; set; } = string.Empty; // Varsayılan boş değerle başlatıldı
    }
}
