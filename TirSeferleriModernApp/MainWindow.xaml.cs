// MainWindow.xaml.cs
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Views;
using TirSeferleriModernApp.ViewModels;
using Microsoft.Data.Sqlite;
using MaterialDesignThemes.Wpf;
using TirSeferleriModernApp.Services;
using System.IO;
using System.Windows.Input;
using System.Collections.Generic;

namespace TirSeferleriModernApp
{
    public partial class MainWindow : Window
    {
        private readonly SeferlerView seferlerView = new();
        private readonly AraclarView araclarView = new();
        private readonly GiderlerView giderlerView = new();
        private readonly KarHesapView karHesapView = new();
        private readonly SecimTakibi _secimTakibi = new();
        private readonly string dbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TirSeferleri.db");



        // private readonly ItemsControl _araclarMenuPanel;

        public MainWindow()
        {
            InitializeComponent();
            
            // DataContext'i constructor'da ayarlayın
            DataContext = new MainViewModel();
            
            // Event handler'ı kod ile bağlayın (alternatif)
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs] MainWindow_Loaded çağrıldı.");

            try
            {
                DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
                DatabaseService.CheckAndCreateOrUpdateGiderlerTablosu();
                DatabaseService.CheckAndCreateOrUpdateKarHesapTablosu();

                LoadAraclarMenu();
                Debug.WriteLine("[MainWindow.xaml.cs] Araçlar menüsü yüklendi.");

                var databaseService = new DatabaseService(dbFile);
                databaseService.Initialize();
                Debug.WriteLine("[MainWindow.xaml.cs] DatabaseService başlatıldı.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow.xaml.cs] Hata: {ex.Message}");
                MessageBox.Show($"Başlatma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAraclarMenu()
        {
            Debug.WriteLine("[MainWindow.xaml.cs:46] LoadAraclarMenu metodu çağrıldı.");

            // ❌ DataContext null olabilir
            if (DataContext is not MainViewModel vm)
            {
                Debug.WriteLine("[MainWindow.xaml.cs] DataContext null veya MainViewModel değil!");
                return;
            }
            
                vm.AraclarMenu.Clear();
            
                const string query = @"
                SELECT Cekiciler.CekiciId, Cekiciler.Plaka, Soforler.SoforId, Soforler.SoforAdi, Cekiciler.Aktif, Cekiciler.DorseId
                FROM Cekiciler 
                LEFT JOIN Soforler ON Cekiciler.SoforId = Soforler.SoforId";
            
                var araclar = new List<(int cekiciId, string plaka, int? soforId, string soforAdi, bool aktif, int? dorseId)>();
            
                using var connection = new SqliteConnection(DatabaseService.ConnectionString);
                connection.Open();
                Debug.WriteLine("[MainWindow.xaml.cs:55] Veritabanı bağlantısı açıldı.");
                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();
                Debug.WriteLine("[MainWindow.xaml.cs:58] Araçlar sorgusu çalıştırıldı.");
            
                while (reader.Read())
                {
                    int cekiciId = reader.GetInt32(0);
                    string plaka = reader.GetString(1);
                    int? soforId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                    string soforAdi = reader.IsDBNull(3) ? "Bilinmiyor" : reader.GetString(3);
                    bool aktif = reader.GetInt32(4) == 1;
                    int? dorseId = reader.IsDBNull(5) ? null : reader.GetInt32(5);

                    araclar.Add((cekiciId, plaka, soforId, soforAdi, aktif, dorseId));
                    Debug.WriteLine($"[MainWindow.xaml.cs:66] Araç eklendi: Plaka={plaka}, Şoför={soforAdi}, Aktif={aktif}");
                }

            foreach (var (_, plaka, _, soforAdi, _, _) in araclar)
            {
                vm.AraclarMenu.Add($"{plaka} - {soforAdi}"); // ✅ sadece string ekle
            }            
        }
               
        private static void EnsureDatabaseTables()
        {
            Debug.WriteLine("[MainWindow.xaml.cs:104] EnsureDatabaseTables metodu çağrıldı.");
            using var connection = new SqliteConnection(DatabaseService.ConnectionString);
            connection.Open();
            Debug.WriteLine("[MainWindow.xaml.cs:107] Veritabanı bağlantısı açıldı.");

            string cekicilerTableCheck = @"
                CREATE TABLE IF NOT EXISTS Cekiciler (
                    Plaka TEXT NOT NULL,
                    SoforId INTEGER,
                    Aktif INTEGER NOT NULL
                );";
            using var cekicilerCommand = new SqliteCommand(cekicilerTableCheck, connection);
            cekicilerCommand.ExecuteNonQuery();
            Debug.WriteLine("[MainWindow.xaml.cs:115] Cekiciler tablosu kontrol edildi ve oluşturuldu.");

            string soforlerTableCheck = @"
                CREATE TABLE IF NOT EXISTS Soforler (
                    SoforId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SoforAdi TEXT NOT NULL
                );";
            using var soforlerCommand = new SqliteCommand(soforlerTableCheck, connection);
            soforlerCommand.ExecuteNonQuery();
            Debug.WriteLine("[MainWindow.xaml.cs:122] Soforler tablosu kontrol edildi ve oluşturuldu.");
        }

        private void BtnGeriDon_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:127] BtnGeriDon butonuna tıklandı.");
        }

        private void BtnAraclar_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:132] BtnAraclar butonuna tıklandı.");
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.BtnAraclarCommand.Execute(null);
                Debug.WriteLine("[MainWindow.xaml.cs:136] BtnAraclarCommand çalıştırıldı.");
            }
        }

        private void BtnTanimlar_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:141] BtnTanimlar butonuna tıklandı.");
        }
        private void BtnTanimla_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:146] BtnTanimla butonuna tıklandı.");
        }
        private void BtnSeferler_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:151] BtnSeferler butonuna tıklandı.");
        }
        private void BtnGiderler_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:156] BtnGiderler butonuna tıklandı.");
        }
        private void BtnKar_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:161] BtnKar butonuna tıklandı.");
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:166] MenuItem butonuna tıklandı.");
            if (sender is MenuItem menuItem)
            {
                string selectedService = menuItem.Header?.ToString() ?? "Bilinmiyor (Menü başlığı tanımlı değil)";
                Debug.WriteLine($"[MainWindow.xaml.cs:170] Seçilen Hizmet: {selectedService}");
                MessageBox.Show($"Seçilen Hizmet: {selectedService}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:177] SearchButton butonuna tıklandı.");
            MessageBox.Show("Arama işlemi başlatıldı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:183] TopMenuItem butonuna tıklandı.");
            if (sender is MenuItem menuItem)
            {
                string selectedMenu = menuItem.Header?.ToString() ?? "Bilinmiyor";
                Debug.WriteLine($"[MainWindow.xaml.cs:187] Seçilen Menü: {selectedMenu}");
                MessageBox.Show($"Seçilen Menü: {selectedMenu}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AA_MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:194] AA_MenuItem_MouseEnter metodu çağrıldı.");
        }

        private void AA_MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("[MainWindow.xaml.cs:198] AA_MenuItem_MouseLeave metodu çağrıldı.");
        }

        public void StatusBarBilgisiGoster(string mesaj)
        {
            if (DataContext is MainViewModel vm)
                vm.StatusText = mesaj;
        }
    }
}