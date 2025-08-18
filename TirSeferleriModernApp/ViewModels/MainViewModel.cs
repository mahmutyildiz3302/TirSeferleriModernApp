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
using MaterialDesignThemes.Wpf; // SnackbarMessageQueue için

namespace TirSeferleriModernApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SecimTakibi _secimTakibi;         // IDE0044: readonly
        private readonly DatabaseService _databaseService;  // IDE0044: readonly

        [ObservableProperty]
        private ObservableCollection<string> _araclarMenu = [];

        private SeferlerViewModel? _aktifSeferlerVm;

        // İçerik alanında gösterilecek mevcut görünüm (UserControl)
        private object? _currentContent;
        public object? CurrentContent
        {
            get => _currentContent;
            set => SetProperty(ref _currentContent, value);
        }

        private bool _araclarMenuAcik;
        public bool AraclarMenuAcik
        {
            get => _araclarMenuAcik;
            set => SetProperty(ref _araclarMenuAcik, value);
        }

        // Tanımlar butonunun görünürlüğü
        private bool _showTanimlar = true;
        public bool ShowTanimlar
        {
            get => _showTanimlar;
            set => SetProperty(ref _showTanimlar, value);
        }

        private string _statusText = "Hazır.";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string? _selectedPlaka;
        public string? SelectedPlaka
        {
            get => _selectedPlaka;
            set => SetProperty(ref _selectedPlaka, value);
        }

        private string? _aktifAltMenu;
        public string? AktifAltMenu
        {
            get => _aktifAltMenu;
            set => SetProperty(ref _aktifAltMenu, value);
        }

        private Visibility _geriDonVisibility = Visibility.Collapsed;
        public Visibility GeriDonVisibility
        {
            get => _geriDonVisibility;
            set => SetProperty(ref _geriDonVisibility, value);
        }

        [ObservableProperty]
        private ObservableCollection<AltMenuOgesi> _seciliPlakaAltMenu = [];

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
        public ICommand BtnSeferlerCommand { get; }
        public ICommand BtnGiderlerCommand { get; }
        public ICommand BtnKarCommand      { get; }
        public ICommand DebugListesiKomutu { get; }
        public ICommand SelectAracCommand  { get; }
        public ICommand ToggleTanimlarMenuCommand { get; }
        public ICommand AcTanimlarCommand { get; }

        public MainViewModel() : this(new SecimTakibi(), "TirSeferleri.db")
        {
            Trace.WriteLine("[MainViewModel.cs] Parametresiz constructor çağrıldı.");
        }

        public MainViewModel(SecimTakibi secimTakibi, string dbFile)
        {
            Trace.WriteLine("[MainViewModel.cs:20] MainViewModel constructor çağrıldı.");
            _secimTakibi = secimTakibi;
            _databaseService = new DatabaseService(dbFile);

            _araclarMenuAcik = false;

            BtnGeriDonCommand = new RelayCommand(ExecuteGeriDon);
            BtnAraclarCommand  = new RelayCommand(ExecuteAraclar);
            ToggleAraclarMenuCommand = new RelayCommand(ExecuteToggleAraclar);
            BtnSeferlerCommand = new RelayCommand(ExecuteSeferler);
            BtnGiderlerCommand = new RelayCommand(ExecuteGiderler);
            BtnKarCommand      = new RelayCommand(ExecuteKar);
            DebugListesiKomutu = new RelayCommand(ExecuteDebugListesi);
            SelectAracCommand  = new RelayCommand<string>(ExecuteSelectArac);
            ToggleTanimlarMenuCommand = new RelayCommand(ExecuteTanimlar);
            AcTanimlarCommand = new RelayCommand(ExecuteTanimlar);
            
            Trace.WriteLine("[MainViewModel.cs:28] ViewModel oluşturuldu.");
        }

        private void ExecuteTanimlar()
        {
            // Tanımlar ekranını aç
            CurrentContent = new TanimlamaView();
            StatusText = "Tanımlar açıldı.";
            AktifAltMenu = "📋 Tanımlar";
        }

        private void ExecuteGeriDon()
        {
            Trace.WriteLine("[MainViewModel.cs:33] BtnGeriDon butonuna tıklandı.");
            Trace.WriteLine("[MainViewModel.cs:34] Geri dönme işlemi başlatıldı.");
            _secimTakibi.GeriDon();
            Trace.WriteLine("[MainViewModel.cs:36] Geri dönme işlemi tamamlandı.");
        }

        private void ExecuteAraclar()
        {
            Trace.WriteLine("[MainViewModel.cs:41] Araçlar menüsü verileri yükleniyor.");
            LoadAraclarMenu();
            Trace.WriteLine("[MainViewModel.cs:43] Araçlar menüsü verileri yüklendi (görünürlük: " + AraclarMenuAcik + ").");
        }

        private void ExecuteToggleAraclar()
        {
            AraclarMenuAcik = !AraclarMenuAcik;
            Trace.WriteLine($"[MainViewModel.cs] Araçlar menüsü {(AraclarMenuAcik ? "açıldı" : "kapandı")}.");
            if (AraclarMenuAcik && AraclarMenu.Count == 0)
            {
                LoadAraclarMenu();
            }
        }

        private void ExecuteSeferler()
        {
            Trace.WriteLine("[MainViewModel.cs:53] Seferler menüsü işlemleri başlatıldı.");
            AktifAltMenu = "📋 Seferler";
            _aktifSeferlerVm = new SeferlerViewModel(new SnackbarMessageQueue(TimeSpan.FromSeconds(3)), _databaseService);
            _aktifSeferlerVm.LoadSeferler();
            // Mevcut seçili plaka varsa VM'ye aktar
            if (!string.IsNullOrWhiteSpace(SelectedPlaka))
            {
                var sofor = AraclarMenu.FirstOrDefault(x => x.StartsWith(SelectedPlaka + " "))?.Split(" - ").ElementAtOrDefault(1);
                _aktifSeferlerVm.UpdateSelection(SelectedPlaka, sofor);
            }
            var view = new SeferlerView { DataContext = _aktifSeferlerVm };
            CurrentContent = view;
            StatusText = "Seferler açıldı.";
        }

        private void ExecuteGiderler()
        {
            Trace.WriteLine("[MainViewModel.cs:58] Giderler menüsü işlemleri başlatıldı.");
            AktifAltMenu = "💸 Giderler";
            CurrentContent = new GiderlerView();
            StatusText = "Giderler açıldı.";
        }

        private void ExecuteKar()
        {
            Trace.WriteLine("[MainViewModel.cs:63] Kar hesap menüsü işlemleri başlatıldı.");
            AktifAltMenu = "📊 Kar Hesap";
            CurrentContent = new KarHesapView();
            StatusText = "Kar Hesap açıldı.";
        }

        private void ExecuteDebugListesi()
        {
            // İçeriği debug listesi ile doldur
            CurrentContent = new DebugListesiView();
            StatusText = "Debug listesi açıldı.";
            Trace.WriteLine("[MainViewModel.cs] Debug listesi ana içerikte gösteriliyor.");
        }

        private void LoadAraclarMenu()
        {
            Trace.WriteLine("[MainViewModel.cs:76.1] Araçlar menüsü verileri yükleniyor. GetAraclar metodu çağrılacak.");
            try
            {
                var items = DatabaseService.GetAraclar()
                                           .Select(a => $"{a.Plaka} - {a.SoforAdi}");
                AraclarMenu.ReplaceAll(items);
                Trace.WriteLine("[MainViewModel.cs:76.3] Araçlar menüsü verileri yüklendi.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MainViewModel.cs:76.4] Araçlar menüsü yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void ExecuteSelectArac(string? arac)
        {
            Trace.WriteLine("[MainViewModel.cs:120] ExecuteSelectArac metodu çağrıldı.");
            if (arac == null)
            {
                Trace.WriteLine("[MainViewModel.cs:122] Seçilen araç null.");
                return;
            }
            Trace.WriteLine($"[MainViewModel.cs:125] Seçilen araç: {arac}");
            var parts = arac.Split(" - ");
            var plaka = parts.Length > 0 ? parts[0].Trim() : arac.Trim();
            var sofor = parts.Length > 1 ? parts[1].Trim() : null;
            AltMenuyuGoster(plaka);

            // Eğer Seferler ekranı açıksa üstteki bilgileri güncelle
            _aktifSeferlerVm?.UpdateSelection(plaka, sofor);
        }

        public void AltMenuyuGoster(string? plaka)
        {
            Trace.WriteLine($"[MainViewModel.cs] AltMenuyuGoster çağrıldı. Plaka: {plaka}");

            if (!string.IsNullOrWhiteSpace(SelectedPlaka) && string.Equals(SelectedPlaka, plaka, System.StringComparison.OrdinalIgnoreCase))
            {
                SelectedPlaka = null;
                AktifAltMenu = null;
                SeciliPlakaAltMenu.Clear();
                Trace.WriteLine("[MainViewModel.cs] Aynı plaka tekrar tıklandı, alt menü kapatıldı.");
                return;
            }

            SelectedPlaka = string.IsNullOrWhiteSpace(plaka) ? null : plaka;

            if (string.IsNullOrWhiteSpace(plaka))
            {
                SeciliPlakaAltMenu.Clear();
                return;
            }

            SeciliPlakaAltMenu.Clear();
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("📋 Seferler", BtnSeferlerCommand));
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("💸 Giderler", BtnGiderlerCommand));
            SeciliPlakaAltMenu.Add(new AltMenuOgesi("📊 Kar Hesap", BtnKarCommand));
            Trace.WriteLine($"[MainViewModel.cs] {plaka} için alt menü oluşturuldu. Öğe sayısı: {SeciliPlakaAltMenu.Count}");
        }
    }
}
#pragma warning restore IDE0290 // Birincil oluşturucuyu kullan önerisini geri aç
