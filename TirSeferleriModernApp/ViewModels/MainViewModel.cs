using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TirSeferleriModernApp.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Views;
using System.Linq; // IDE0028 i�in eklendi
using TirSeferleriModernApp.Extensions; // ReplaceAll() i�in eklendi

namespace TirSeferleriModernApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SecimTakibi _secimTakibi;         // IDE0044: readonly
        private readonly DatabaseService _databaseService;  // IDE0044: readonly

        // Araclar men�s� (generator ile devam edebilir)
        [ObservableProperty]
        private ObservableCollection<string> _araclarMenu = new();

        // STATUS BAR � manuel property (generator ba��ml�l���n� kald�rd�k)
        private string _statusText = "Haz�r.";
        

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
            Debug.WriteLine("[MainViewModel.cs:20] MainViewModel constructor �a�r�ld�.");
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
            
            Debug.WriteLine("[MainViewModel.cs:28] ViewModel olu�turuldu.");
        }

        private void ExecuteGeriDon()
        {
            Debug.WriteLine("[MainViewModel.cs:33] BtnGeriDon butonuna t�kland�.");
            Debug.WriteLine("[MainViewModel.cs:34] Geri d�nme i�lemi ba�lat�ld�.");
            _secimTakibi.GeriDon();
            Debug.WriteLine("[MainViewModel.cs:36] Geri d�nme i�lemi tamamland�.");
        }

        private void ExecuteAraclar()
        {
            Debug.WriteLine("[MainViewModel.cs:41] Ara�lar men�s� y�kleniyor.");
            LoadAraclarMenu();
            Debug.WriteLine("[MainViewModel.cs:43] Ara�lar men�s� y�klendi.");
        }

        private void ExecuteTanimlar()
        {
            Debug.WriteLine("[MainViewModel.cs:48] Tan�mlar men�s� i�lemleri ba�lat�ld�.");
        }

        private void ExecuteSeferler()
        {
            Debug.WriteLine("[MainViewModel.cs:53] Seferler men�s� i�lemleri ba�lat�ld�.");
        }

        private void ExecuteGiderler()
        {
            Debug.WriteLine("[MainViewModel.cs:58] Giderler men�s� i�lemleri ba�lat�ld�.");
        }

        private void ExecuteKar()
        {
            Debug.WriteLine("[MainViewModel.cs:63] Kar hesap men�s� i�lemleri ba�lat�ld�.");
        }

        private void ExecuteDebugListesi()
        {
            Debug.WriteLine("[MainViewModel.cs:68] Debug listesi yeni bir pencere olarak a��l�yor.");

            var debugWindow = new Window
            {
                Content = new DebugListesiView(),
                Width = 800,
                Height = 600,
                Title = "Debug Listesi",
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            debugWindow.Show();
            Debug.WriteLine("[MainViewModel.cs:76] Debug listesi yeni bir pencere olarak a��ld�.");
        }

        private void LoadAraclarMenu()
        {
            Debug.WriteLine("[MainViewModel.cs:76.1] Ara�lar men�s� verileri y�kleniyor. GetAraclar metodu �a�r�lacak.");
            try
            {
                var items = DatabaseService.GetAraclar()
                                           .Select(a => $"{a.Plaka} - {a.SoforAdi}");

                AraclarMenu.ReplaceAll(items);
                Debug.WriteLine("[MainViewModel.cs:76.3] Ara�lar men�s� verileri y�klendi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel.cs:76.4] Ara�lar men�s� y�klenirken hata olu�tu: {ex.Message}");
            }
        }
        private void ExecuteSelectArac(string? arac)
        {
            Debug.WriteLine("[MainViewModel.cs:120] ExecuteSelectArac metodu �a�r�ld�.");
            if (arac == null)
            {
                Debug.WriteLine("[MainViewModel.cs:122] Se�ilen ara� null.");
                return;
            }

            Debug.WriteLine($"[MainViewModel.cs:125] Se�ilen ara�: {arac}");
            // Se�ilen ara� i�lemleri
        }
    }
}
