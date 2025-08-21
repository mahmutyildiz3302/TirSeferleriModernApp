// MainWindow.xaml.cs
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Views;
using TirSeferleriModernApp.ViewModels;
using MaterialDesignThemes.Wpf;
using TirSeferleriModernApp.Services;
using System.IO;
using System.Windows.Input;

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
            DataContext = new MainViewModel(_secimTakibi, dbFile);
            Loaded += MainWindow_Loaded;
        }

        private void BtnToggleLeftMenu_Click(object sender, RoutedEventArgs e)
        {
            // 300 px genişliğe sahip sol menu, 48 px'e daraltılır ya da geri açılır
            if (LeftMenuPanel.Width > 60)
            {
                LeftMenuPanel.Width = 48;
                // buton ok yönünü değiştir
                if (sender is Button b) b.Content = "❯";
            }
            else
            {
                LeftMenuPanel.Width = 300;
                if (sender is Button b) b.Content = "❮";
            }
            // DockPanel.LastChildFill olduğu için sağ içerik otomatik genişleyecek/daralacak
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs] MainWindow_Loaded çağrıldı.");

            try
            {
                DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
                DatabaseService.CheckAndCreateOrUpdateGiderlerTablosu();
                DatabaseService.CheckAndCreateOrUpdateKarHesapTablosu();
                DatabaseService.CheckAndCreateOrUpdateYakitGiderTablosu();

                var databaseService = new DatabaseService(dbFile);
                databaseService.Initialize();
                Trace.WriteLine("[MainWindow.xaml.cs] DatabaseService başlatıldı.");

                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.BtnAraclarCommand.Execute(null);
                    Trace.WriteLine("[MainWindow.xaml.cs] Araçlar menüsü yüklendi.");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MainWindow.xaml.cs] Hata: {ex.Message}");
                MessageBox.Show($"Başlatma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGeriDon_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:127] BtnGeriDon butonuna tıklandı.");
        }

        private void BtnAraclar_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:132] BtnAraclar butonuna tıklandı.");
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.BtnAraclarCommand.Execute(null);
                Trace.WriteLine("[MainWindow.xaml.cs:136] BtnAraclarCommand çalıştırıldı.");
            }
        }

        private void BtnTanimlar_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:141] BtnTanimlar butonuna tıklandı.");
        }
        private void BtnTanimla_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:146] BtnTanimla butonuna tıklandı.");
        }
        private void BtnSeferler_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:151] BtnSeferler butonuna tıklandı.");
        }
        private void BtnGiderler_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:156] BtnGiderler butonuna tıklandı.");
        }
        private void BtnKar_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:161] BtnKar butonuna tıklandı.");
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:166] MenuItem butonuna tıklandı.");
            if (sender is MenuItem menuItem)
            {
                string selectedService = menuItem.Header?.ToString() ?? "Bilinmiyor (Menü başlığı tanımlı değil)";
                Trace.WriteLine($"[MainWindow.xaml.cs:170] Seçilen Hizmet: {selectedService}");
                MessageBox.Show($"Seçilen Hizmet: {selectedService}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:177] SearchButton butonuna tıklandı.");
            MessageBox.Show("Arama işlemi başlatıldı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:183] TopMenuItem butonuna tıklandı.");
            if (sender is MenuItem menuItem)
            {
                string selectedMenu = menuItem.Header?.ToString() ?? "Bilinmiyor";
                Trace.WriteLine($"[MainWindow.xaml.cs:187] Seçilen Menü: {selectedMenu}");
                MessageBox.Show($"Seçilen Menü: {selectedMenu}", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AA_MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:194] AA_MenuItem_MouseEnter metodu çağrıldı.");
        }

        private void AA_MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            Trace.WriteLine("[MainWindow.xaml.cs:198] AA_MenuItem_MouseLeave metodu çağrıldı.");
        }

        public void StatusBarBilgisiGoster(string mesaj)
        {
            if (DataContext is MainViewModel vm)
                vm.StatusText = mesaj;
        }

        private void BtnDepoGuzergahTanim_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CurrentContent = new DepoGuzergahTanimView();
                vm.StatusText = "Depo ve Güzergah Tanımı açıldı.";
                vm.AktifAltMenu = "📋 Depo ve Güzergah Tanımı";
            }
        }
    }
}