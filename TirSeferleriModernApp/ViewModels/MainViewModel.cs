using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TirSeferleriModernApp.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Views;
using System.Linq; // IDE0028 için eklendi
using TirSeferleriModernApp.Extensions; // ReplaceAll() için eklendi

namespace TirSeferleriModernApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SecimTakibi _secimTakibi;         // IDE0044: readonly
        private readonly DatabaseService _databaseService;  // IDE0044: readonly

        // Araclar menüsü (generator ile devam edebilir)
        [ObservableProperty]
        private ObservableCollection<string> _araclarMenu = new();

        // STATUS BAR — manuel property (generator baðýmlýlýðýný kaldýrdýk)
        private string _statusText = "Hazýr.";
        

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ICommand BtnGeriDonCommand { get; }
        public ICommand BtnAraclarCommand { get; }
        public ICommand BtnTanimlarCommand { get; }
        public ICommand BtnSeferlerCommand { get; }
        public ICommand BtnGiderlerCommand { get; }
        public ICommand BtnKarCommand { get; }
        public ICommand DebugListesiKomutu { get; }
        public ICommand SelectAracCommand { get; set; }

        public MainViewModel(SecimTakibi secimTakibi, string dbFile)
        {
            Debug.WriteLine("[MainViewModel.cs:20] MainViewModel constructor çaðrýldý.");
            _secimTakibi = secimTakibi;
            _databaseService = new DatabaseService(dbFile);

            BtnGeriDonCommand = new RelayCommand(ExecuteGeriDon);
            BtnAraclarCommand = new RelayCommand(ExecuteAraclar);
            BtnTanimlarCommand = new RelayCommand(ExecuteTanimlar);
            BtnSeferlerCommand = new RelayCommand(ExecuteSeferler);
            BtnGiderlerCommand = new RelayCommand(ExecuteGiderler);
            BtnKarCommand = new RelayCommand(ExecuteKar);
            DebugListesiKomutu = new RelayCommand(ExecuteDebugListesi);
            SelectAracCommand = new RelayCommand<string>(ExecuteSelectArac);
            
            Debug.WriteLine("[MainViewModel.cs:28] ViewModel oluþturuldu.");
        }

        private void ExecuteGeriDon()
        {
            Debug.WriteLine("[MainViewModel.cs:33] BtnGeriDon butonuna týklandý.");
            Debug.WriteLine("[MainViewModel.cs:34] Geri dönme iþlemi baþlatýldý.");
            _secimTakibi.GeriDon();
            Debug.WriteLine("[MainViewModel.cs:36] Geri dönme iþlemi tamamlandý.");
        }

        private void ExecuteAraclar()
        {
            Debug.WriteLine("[MainViewModel.cs:41] Araçlar menüsü yükleniyor.");
            LoadAraclarMenu();
            Debug.WriteLine("[MainViewModel.cs:43] Araçlar menüsü yüklendi.");
        }

        private void ExecuteTanimlar()
        {
            Debug.WriteLine("[MainViewModel.cs:48] Tanýmlar menüsü iþlemleri baþlatýldý.");
        }

        private void ExecuteSeferler()
        {
            Debug.WriteLine("[MainViewModel.cs:53] Seferler menüsü iþlemleri baþlatýldý.");
        }

        private void ExecuteGiderler()
        {
            Debug.WriteLine("[MainViewModel.cs:58] Giderler menüsü iþlemleri baþlatýldý.");
        }

        private void ExecuteKar()
        {
            Debug.WriteLine("[MainViewModel.cs:63] Kar hesap menüsü iþlemleri baþlatýldý.");
        }

        private void ExecuteDebugListesi()
        {
            Debug.WriteLine("[MainViewModel.cs:68] Debug listesi yeni bir pencere olarak açýlýyor.");

            var debugWindow = new Window
            {
                Content = new DebugListesiView(),
                Width = 800,
                Height = 600,
                Title = "Debug Listesi",
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            debugWindow.Show();
            Debug.WriteLine("[MainViewModel.cs:76] Debug listesi yeni bir pencere olarak açýldý.");
        }

        private void LoadAraclarMenu()
        {
            Debug.WriteLine("[MainViewModel.cs:76.1] Araçlar menüsü verileri yükleniyor. GetAraclar metodu çaðrýlacak.");
            try
            {
                var items = DatabaseService.GetAraclar()
                                           .Select(a => $"{a.Plaka} - {a.SoforAdi}");

                AraclarMenu.ReplaceAll(items);
                Debug.WriteLine("[MainViewModel.cs:76.3] Araçlar menüsü verileri yüklendi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel.cs:76.4] Araçlar menüsü yüklenirken hata oluþtu: {ex.Message}");
            }
        }
        private void ExecuteSelectArac(string? arac)
        {
            Debug.WriteLine("[MainViewModel.cs:120] ExecuteSelectArac metodu çaðrýldý.");
            if (arac == null)
            {
                Debug.WriteLine("[MainViewModel.cs:122] Seçilen araç null.");
                return;
            }

            Debug.WriteLine($"[MainViewModel.cs:125] Seçilen araç: {arac}");
            // Seçilen araç iþlemleri
        }
    }
}
