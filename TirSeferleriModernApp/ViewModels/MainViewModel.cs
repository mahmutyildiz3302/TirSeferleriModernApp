#pragma warning disable IDE0290 // Birincil oluşturucuyu kullan önerisini bastır
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

        // Araçlar menüsü (plaka listesi)
        [ObservableProperty]
        private ObservableCollection<string> _araclarMenu = [];

        // Araçlar üst menü açık/kapalı bilgisi (başlangıçta kapalı)
        private bool _araclarMenuAcik;
        public bool AraclarMenuAcik
        {
            get => _araclarMenuAcik;
            set => SetProperty(ref _araclarMenuAcik, value);
        }

        // STATUS BAR — manuel property (generator bağımlılığını kaldırdık)
        private string _statusText = "Hazır.";

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        // Seçilen plaka (alt menünün hangi plakada açılacağını belirler)
        private string? _selectedPlaka;
        public string? SelectedPlaka
        {
            get => _selectedPlaka;
            set => SetProperty(ref _selectedPlaka, value);
        }

        // Alt menüde hangi öğenin aktif olduğunu takip eder ("📋 Seferler" / "💸 Giderler" / "📊 Kar Hesap")
        private string? _aktifAltMenu;
        public string? AktifAltMenu
        {
            get => _aktifAltMenu;
            set => SetProperty(ref _aktifAltMenu, value);
        }

        // Geri Dön butonunun görünürlüğü
        private Visibility _geriDonVisibility = Visibility.Collapsed;

        public Visibility GeriDonVisibility
        {
            get => _geriDonVisibility;
            set => SetProperty(ref _geriDonVisibility, value);
        }

        // Seçilen plakanın altında gösterilecek ikinci seviye menü öğeleri (DataBinding için)
        [ObservableProperty]
        private ObservableCollection<AltMenuOgesi> _seciliPlakaAltMenu = [];

        // Alt menü öğesi (View’de Button template’ine bağlanacak)
        public class AltMenuOgesi
        {
            public string Baslik { get; }
            public ICommand Komut { get; }

            public AltMenuOgesi(string baslik, ICommand komut)
            {
                Baslik = baslik;
                Komut = komut;
            }
        }

        public ICommand BtnGeriDonCommand { get; }
        public ICommand BtnAraclarCommand  { get; }
        public ICommand ToggleAraclarMenuCommand { get; }
        public ICommand BtnTanimlarCommand { get; }
        public ICommand BtnSeferlerCommand { get; }
        public ICommand BtnGiderlerCommand { get; }
        public ICommand BtnKarCommand      { get; }
        public ICommand DebugListesiKomutu { get; }
        public ICommand SelectAracCommand  { get; }

        // Parametresiz constructor
        public MainViewModel() : this(new SecimTakibi(), "TirSeferleri.db")
        {
            Debug.WriteLine("[MainViewModel.cs] Parametresiz constructor çağrıldı.");
        }

        // Parametreli constructor
        public MainViewModel(SecimTakibi secimTakibi, string dbFile)
        {
            Debug.WriteLine("[MainViewModel.cs:20] MainViewModel constructor çağrıldı.");
            _secimTakibi = secimTakibi;
            _databaseService = new DatabaseService(dbFile);

            // Başlangıçta araçlar üst menü kapalı
            _araclarMenuAcik = false;

            BtnGeriDonCommand = new RelayCommand(ExecuteGeriDon);
            BtnAraclarCommand  = new RelayCommand(ExecuteAraclar);            // veri yükleme
            ToggleAraclarMenuCommand = new RelayCommand(ExecuteToggleAraclar); // aç/kapa
            BtnTanimlarCommand = new RelayCommand(ExecuteTanimlar);
            BtnSeferlerCommand = new RelayCommand(ExecuteSeferler);
            BtnGiderlerCommand = new RelayCommand(ExecuteGiderler);
            BtnKarCommand      = new RelayCommand(ExecuteKar);
            DebugListesiKomutu = new RelayCommand(ExecuteDebugListesi);
            SelectAracCommand  = new RelayCommand<string>(ExecuteSelectArac);
            
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
            Debug.WriteLine("[MainViewModel.cs:41] Araçlar menüsü verileri yükleniyor.");
            LoadAraclarMenu();
            Debug.WriteLine("[MainViewModel.cs:43] Araçlar menüsü verileri yüklendi (görünürlük: " + AraclarMenuAcik + ").");
        }

        private void ExecuteToggleAraclar()
        {
            AraclarMenuAcik = !AraclarMenuAcik;
            Debug.WriteLine($"[MainViewModel.cs] Araçlar menüsü {(AraclarMenuAcik ? "açıldı" : "kapandı")}.");
            if (AraclarMenuAcik && AraclarMenu.Count == 0)
            {
                LoadAraclarMenu();
            }
        }

        private void ExecuteTanimlar()
        {
            Debug.WriteLine("[MainViewModel.cs:48] Tanımlar menüsü işlemleri başlatıldı.");
        }

        private void ExecuteSeferler()
        {
            Debug.WriteLine("[MainViewModel.cs:53] Seferler menüsü işlemleri başlatıldı.");
            AktifAltMenu = "📋 Seferler";
        }

        private void ExecuteGiderler()
        {
            Debug.WriteLine("[MainViewModel.cs:58] Giderler menüsü işlemleri başlatıldı.");
            AktifAltMenu = "💸 Giderler";
        }

        private void ExecuteKar()
        {
            Debug.WriteLine("[MainViewModel.cs:63] Kar hesap menüsü işlemleri başlatıldı.");
            AktifAltMenu = "📊 Kar Hesap";
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
            // "PLAKA - Şoför" formatından plakayı ayır ve alt menüyü hazırla
            var parts = arac.Split(" - ");
            var plaka = parts.Length > 0 ? parts[0].Trim() : arac.Trim();
            AltMenuyuGoster(plaka);
        }

        // Tıklanan plakaya ait ikinci seviye alt menüyü oluşturur
        public void AltMenuyuGoster(string? plaka)
        {
            Debug.WriteLine($"[MainViewModel.cs] AltMenuyuGoster çağrıldı. Plaka: {plaka}");

            // Toggle davranışı: Aynı plaka tekrar tıklanırsa kapat
            if (!string.IsNullOrWhiteSpace(SelectedPlaka) && string.Equals(SelectedPlaka, plaka, System.StringComparison.OrdinalIgnoreCase))
            {
                SelectedPlaka = null;
                AktifAltMenu = null;
                SeciliPlakaAltMenu.Clear();
                Debug.WriteLine("[MainViewModel.cs] Aynı plaka tekrar tıklandı, alt menü kapatıldı.");
                return;
            }

            // Seçimi güncelle
            SelectedPlaka = string.IsNullOrWhiteSpace(plaka) ? null : plaka;

            // Geçersiz parametre geldiyse alt menüyü temizle
            if (string.IsNullOrWhiteSpace(plaka))
            {
                SeciliPlakaAltMenu.Clear();
                return;
            }

            // Alt menüyü yeniden oluştur (her tıklamada güncel ve tekil tut)
            SeciliPlakaAltMenu.Clear();

            // Mevcut komutlar kullanılarak alt menü butonları tanımlanır
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("📋 Seferler", BtnSeferlerCommand));
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("💸 Giderler", BtnGiderlerCommand));
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("📊 Kar Hesap", BtnKarCommand));

            Debug.WriteLine($"[MainViewModel.cs] {plaka} için alt menü oluşturuldu. Öğe sayısı: {SeciliPlakaAltMenu.Count}");
        }
    }
}
#pragma warning restore IDE0290 // Birincil oluşturucuyu kullan önerisini geri aç
