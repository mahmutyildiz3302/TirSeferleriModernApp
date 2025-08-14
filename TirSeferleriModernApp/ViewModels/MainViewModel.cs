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

        // STATUS BAR — manuel property (generator bağımlılığını kaldırdık)
        private string _statusText = "Hazır.";

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        // Geri Dön butonunun görünürlüğü
        private Visibility _geriDonVisibility = Visibility.Collapsed;

        public Visibility GeriDonVisibility
        {
            get => _geriDonVisibility;
            set => SetProperty(ref _geriDonVisibility, value);
        }

        public ICommand BtnGeriDonCommand { get; }
        public ICommand BtnAraclarCommand { get; }
        public ICommand BtnTanimlarCommand { get; }
        public ICommand BtnSeferlerCommand { get; }
        public ICommand BtnGiderlerCommand { get; }
        public ICommand BtnKarCommand { get; }
        public ICommand DebugListesiKomutu { get; }
        public ICommand SelectAracCommand { get; set; }

        // ✅ Yeni parametresiz constructor buraya eklenecek
        public MainViewModel() : this(new SecimTakibi(), "TirSeferleri.db")
        {
            Debug.WriteLine("[MainViewModel.cs] Parametresiz constructor çağrıldı.");
        }

        public MainViewModel(SecimTakibi secimTakibi, string dbFile)
        {
            Debug.WriteLine("[MainViewModel.cs:20] MainViewModel constructor çağrıldı.");
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
            
            Debug.WriteLine("[MainViewModel.cs:28] ViewModel oluşturuldu.");
        }

        private void ExecuteGeriDon()
        {
            Debug.WriteLine("[MainViewModel.cs:33] BtnGeriDon butonuna tıklandı.");
            Debug.WriteLine("[MainViewModel.cs:34] Geri dönme işlemi başlatıldı.");
            _secimTakibi.GeriDon();
            Debug.WriteLine("[MainViewModel.cs:36] Geri dönme işlemi tamamlandı.");
        }

        private void ExecuteAraclar()
        {
            Debug.WriteLine("[MainViewModel.cs:41] Araçlar menüsü yükleniyor.");
            LoadAraclarMenu();
            Debug.WriteLine("[MainViewModel.cs:43] Araçlar menüsü yüklendi.");
        }

        private void ExecuteTanimlar()
        {
            Debug.WriteLine("[MainViewModel.cs:48] Tanımlar menüsü işlemleri başlatıldı.");
        }

        private void ExecuteSeferler()
        {
            Debug.WriteLine("[MainViewModel.cs:53] Seferler menüsü işlemleri başlatıldı.");
        }

        private void ExecuteGiderler()
        {
            Debug.WriteLine("[MainViewModel.cs:58] Giderler menüsü işlemleri başlatıldı.");
        }

        private void ExecuteKar()
        {
            Debug.WriteLine("[MainViewModel.cs:63] Kar hesap menüsü işlemleri başlatıldı.");
        }

        private void ExecuteDebugListesi()
        {
            Debug.WriteLine("[MainViewModel.cs:68] Debug listesi yeni bir pencere olarak açılıyor.");

            var debugWindow = new Window
            {
                Content = new DebugListesiView(),
                Width = 800,
                Height = 600,
                Title = "Debug Listesi",
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            debugWindow.Show();
            Debug.WriteLine("[MainViewModel.cs:76] Debug listesi yeni bir pencere olarak açıldı.");
        }

        private void LoadAraclarMenu()
        {
            Debug.WriteLine("[MainViewModel.cs:76.1] Araçlar menüsü verileri yükleniyor. GetAraclar metodu çağrılacak.");
            try
            {
                var items = DatabaseService.GetAraclar()
                                           .Select(a => $"{a.Plaka} - {a.SoforAdi}");

                AraclarMenu.ReplaceAll(items);
                Debug.WriteLine("[MainViewModel.cs:76.3] Araçlar menüsü verileri yüklendi.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel.cs:76.4] Araçlar menüsü yüklenirken hata oluştu: {ex.Message}");
            }
        }
        private void ExecuteSelectArac(string? arac)
        {
            Debug.WriteLine("[MainViewModel.cs:120] ExecuteSelectArac metodu çağrıldı.");
            if (arac == null)
            {
                Debug.WriteLine("[MainViewModel.cs:122] Seçilen araç null.");
                return;
            }

            Debug.WriteLine($"[MainViewModel.cs:125] Seçilen araç: {arac}");
            // Seçilen araç işlemleri
        }
    }
}
