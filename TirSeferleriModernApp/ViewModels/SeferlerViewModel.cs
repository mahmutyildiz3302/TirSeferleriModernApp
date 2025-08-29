using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Extensions;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class SeferlerViewModel(SnackbarMessageQueue messageQueue, DatabaseService databaseService) : ObservableObject
    {
        private Sefer? _seciliSefer; // lazy init
        private bool _routeDefaultsApplied;          // bu sefer için otomatik varsayılanlar uygulandı mı?
        private bool _hadBothEndpointsAtStart;       // sefer seçildiğinde her iki nokta zaten dolu miydi?
        private bool _haveBothEndpoints;             // mevcut durumda iki uç da dolu mu? (ilk kez dolu olduğunda tetiklemek için)

        // Tüm verilerin önbelleği (seçilen plaka filtresine göre)
        private List<Sefer> _allSeferlerCache = new();

        // Senkron durum metni (opsiyonel gösterim için)
        private string? _senkronDurumu;
        public string? SenkronDurumu
        {
            get => _senkronDurumu;
            set => SetProperty(ref _senkronDurumu, value);
        }

        // Yıl/ay filtreleme
        public ObservableCollection<int> YilSecenekleri { get; } = new();

        private int _seciliYil = DateTime.Today.Year;
        public int SeciliYil
        {
            get => _seciliYil;
            set
            {
                if (SetProperty(ref _seciliYil, value))
                {
                    ApplyDateFilterAndUpdate();
                }
            }
        }

        // 0 veya null => tüm aylar; 1..12 seçili ay
        private int? _seciliAy = 0;
        public int? SeciliAy
        {
            get => _seciliAy;
            set
            {
                if (SetProperty(ref _seciliAy, value))
                {
                    ApplyDateFilterAndUpdate();
                }
            }
        }

        partial void OnSeciliAyChanged(int? value);

        // Depo/ekstra/bos-dolu seçim listeleri
        public ObservableCollection<string> DepoAdlari { get; } = [];
        public ObservableCollection<string> EkstraAdlari { get; } = [" ", "SODA", "EMANET"]; // "EKSTRA YOK" -> tek boşluk
        public ObservableCollection<string> BosDoluSecenekleri { get; } = ["Boş", "Dolu"];

        public Sefer? SeciliSefer
        {
            get
            {
                if (_seciliSefer == null)
                {
                    _seciliSefer = new Sefer { Tarih = DateTime.Today };
                    _seciliSefer.PropertyChanged += SeciliSefer_PropertyChanged;
                    _routeDefaultsApplied = false;
                    _hadBothEndpointsAtStart = false;
                    _haveBothEndpoints = false;
                }
                return _seciliSefer;
            }
            set
            {
                if (_seciliSefer != value)
                {
                    if (_seciliSefer != null)
                        _seciliSefer.PropertyChanged -= SeciliSefer_PropertyChanged;

                    _seciliSefer = value;

                    if (_seciliSefer != null)
                    {
                        _seciliSefer.PropertyChanged += SeciliSefer_PropertyChanged;
                        // yeni sefer için bayrakları sıfırla
                        _routeDefaultsApplied = false;
                        _hadBothEndpointsAtStart = !string.IsNullOrWhiteSpace(_seciliSefer.YuklemeYeri) && !string.IsNullOrWhiteSpace(_seciliSefer.BosaltmaYeri);
                        _haveBothEndpoints = _hadBothEndpointsAtStart;
                    }

                    OnPropertyChanged(nameof(SeciliSefer));
                    OnPropertyChanged(nameof(KaydetButonMetni));
                    OnPropertyChanged(nameof(TemizleButonMetni));
                    RecalcFiyat();
                }
            }
        }

        public string KaydetButonMetni => SeciliSefer?.SeferId > 0 ? "Seçimi Güncelle" : "Yeni Sefer Kaydet";
        public static string TemizleButonMetni => "Temizle";

        public ObservableCollection<Sefer> SeferListesi { get; set; } = [];

        public ISnackbarMessageQueue MessageQueue { get; } = messageQueue;

        private readonly DatabaseService _databaseService = databaseService;

        [RelayCommand]
        private async Task KaydetVeyaGuncelle()
        {
            SeciliSefer ??= new Sefer { Tarih = DateTime.Today };

            // Seçimden gelen bilgileri (ID'ler dahil) tamamla
            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
            {
                var info = DatabaseService.GetVehicleInfoByCekiciPlaka(SeciliCekiciPlaka);
                SeciliSefer.CekiciId = info.cekiciId;
                SeciliSefer.DorseId = info.dorseId;
                SeciliSefer.SoforId = info.soforId;
                SeciliSefer.SoforAdi = info.soforAdi;
                SeciliSefer.CekiciPlaka = SeciliCekiciPlaka;
                SeciliDorsePlaka = info.dorsePlaka; // üst şerit güncellensin
                SeciliSoforAdi = info.soforAdi;      // üst şerit güncellensin
            }

            // Her kaydet/güncelle öncesi fiyatı otomatik hesapla
            RecalcFiyat();

            // Kopyayı al: Seferler SQLite kaydından sonra Record eşlemesi için kullanılacak
            var seferToPersist = SeciliSefer;

            if (SeciliSefer.SeferId <= 0)
            {
                SeferEkle(SeciliSefer);
            }
            else
            {
                SeferGuncelle(SeciliSefer);
            }

            // 1) Yerelde Record olarak kaydet (is_dirty=1, updated_at=now). Senkron ajanı zaten arka planda çalışacak.
            try
            {
                if (seferToPersist != null)
                {
                    var (remoteId, createdAt) = DatabaseService.TryGetRecordMeta(seferToPersist.SeferId);
                    var rec = new Record
                    {
                        id = seferToPersist.SeferId,
                        remote_id = remoteId,
                        deleted = false,
                        containerNo = seferToPersist.KonteynerNo,
                        loadLocation = seferToPersist.YuklemeYeri,
                        unloadLocation = seferToPersist.BosaltmaYeri,
                        size = seferToPersist.KonteynerBoyutu,
                        status = seferToPersist.BosDolu,
                        nightOrDay = null,
                        truckPlate = seferToPersist.CekiciPlaka,
                        notes = seferToPersist.Aciklama,
                        createdByUserId = null,
                        createdAt = createdAt > 0 ? createdAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await DatabaseService.RecordKaydetAsync(rec);
                    SenkronDurumu = "Senkron: Bekliyor";
                }
            }
            catch (Exception ex)
            {
                SenkronDurumu = $"Senkron: Yerel kayıt hatası ({ex.Message})";
            }

            // Listeyi, filtre korunarak yenile (SQLite'tan okunur)
            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                RefreshFromDatabaseByPlaka(SeciliCekiciPlaka);
            else
                RefreshFromDatabaseAll();
        }

        [RelayCommand]
        private void SelectMonth(object? parameter)
        {
            int? month = null;
            if (parameter != null)
            {
                var s = parameter.ToString();
                if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var m) && m >= 1 && m <= 12)
                    month = m;
            }
            SeciliAy = month;
        }

        private void EnsureYilSecenekleri()
        {
            if (YilSecenekleri.Count == 0)
            {
                var y = DateTime.Today.Year;
                for (int i = y - 5; i <= y + 1; i++) YilSecenekleri.Add(i);
                _seciliYil = y;
                _seciliAy = 0;
            }
        }

        private void RefreshFromDatabaseAll()
        {
            EnsureYilSecenekleri();
            _allSeferlerCache = DatabaseService.GetSeferler();
            ApplyDateFilterAndUpdate();
        }

        private void RefreshFromDatabaseByPlaka(string plaka)
        {
            EnsureYilSecenekleri();
            _allSeferlerCache = DatabaseService.GetSeferlerByCekiciPlaka(plaka);
            ApplyDateFilterAndUpdate();
        }

        private void ApplyDateFilterAndUpdate()
        {
            IEnumerable<Sefer> filtered = _allSeferlerCache;
            if (SeciliYil > 0)
                filtered = filtered.Where(s => s.Tarih.Year == SeciliYil);
            if (SeciliAy.HasValue && SeciliAy.Value >= 1 && SeciliAy.Value <= 12)
                filtered = filtered.Where(s => s.Tarih.Month == SeciliAy.Value);

            SetSeferListWithTotals(filtered);
        }

        private void SeferGuncelle(Sefer guncellenecekSefer)
        {
            if (!ValidateSefer(guncellenecekSefer)) return;

            DatabaseService.SeferGuncelle(guncellenecekSefer);

            MessageQueue.Enqueue($"{guncellenecekSefer.KonteynerNo} numaralı konteyner seferi başarıyla güncellendi!");
            SeciliSefer = new Sefer { Tarih = DateTime.Today };
        }

        private void SeferEkle(Sefer yeniSefer)
        {
            if (!ValidateSefer(yeniSefer)) return;

            var newId = DatabaseService.SeferEkle(yeniSefer);
            if (newId > 0)
            {
                yeniSefer.SeferId = newId;
                MessageQueue.Enqueue($"{yeniSefer.KonteynerNo} numaralı konteyner seferi başarıyla eklendi!");
            }
            else
            {
                MessageQueue.Enqueue("Sefer kaydedilemedi.");
            }
            SeciliSefer = new Sefer { Tarih = DateTime.Today };
        }

        // Soldaki menüden gelen bilgiler (bildirimli özellikler)
        private string? _seciliCekiciPlaka;
        public string? SeciliCekiciPlaka
        {
            get => _seciliCekiciPlaka;
            set => SetProperty(ref _seciliCekiciPlaka, value);
        }

        private string? _seciliSoforAdi;
        public string? SeciliSoforAdi
        {
            get => _seciliSoforAdi;
            set => SetProperty(ref _seciliSoforAdi, value);
        }

        private string? _seciliDorsePlaka;
        public string? SeciliDorsePlaka
        {
            get => _seciliDorsePlaka;
            set => SetProperty(ref _seciliDorsePlaka, value);
        }

        public void UpdateSelection(string? cekiciPlaka, string? soforAdi)
        {
            SeciliCekiciPlaka = cekiciPlaka;
            SeciliSoforAdi = soforAdi;
            SeciliDorsePlaka = string.IsNullOrWhiteSpace(cekiciPlaka) ? null : DatabaseService.GetDorsePlakaByCekiciPlaka(cekiciPlaka);

            // Seçilen çekici plakasına göre listeyi filtrele
            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                RefreshFromDatabaseByPlaka(SeciliCekiciPlaka);
            else
                RefreshFromDatabaseAll();
        }

        public void LoadSeferler()
        {
            RefreshFromDatabaseAll();
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            // EkstraAdlari sabit; DB'den doldurulmayacak
        }

        public void LoadSeferler(string cekiciPlaka)
        {
            RefreshFromDatabaseByPlaka(cekiciPlaka);
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            // EkstraAdlari sabit; DB'den doldurulmayacak
        }

        private void SetSeferListWithTotals(IEnumerable<Sefer> data)
        {
            var list = data?.ToList() ?? new List<Sefer>();
            var header = BuildToplamSatir(list);
            var footer = BuildToplamSatir(list);
            var withTotals = new List<Sefer>(list.Count + 2) { header };
            withTotals.AddRange(list);
            withTotals.Add(footer);
            SeferListesi.ReplaceAll(withTotals);
        }

        private static Sefer BuildToplamSatir(IEnumerable<Sefer> list)
        {
            var src = list?.Where(x => x != null) ?? Enumerable.Empty<Sefer>();
            return new Sefer
            {
                SeferId = 0,
                KonteynerNo = string.Empty,
                Tarih = DateTime.Today,
                Fiyat = src.Sum(x => x.Fiyat),
                Kdv = src.Sum(x => x.Kdv),
                Tevkifat = src.Sum(x => x.Tevkifat),
                KdvDahilTutar = src.Sum(x => x.KdvDahilTutar),
                Aciklama = "Toplam",
                CekiciPlaka = string.Empty
            };
        }

        private void RecalcFiyat()
        {
            if (SeciliSefer == null) return;

            // 1) Ekstra kontrol (Emanet/Soda -> her durumda 1000)
            var ekstra = SeciliSefer.Ekstra?.Trim();
            bool isEmanetSoda = !string.IsNullOrWhiteSpace(ekstra) &&
                                 (string.Equals(ekstra, "EMANET", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(ekstra, "SODA", StringComparison.OrdinalIgnoreCase));

            var bosDolu = NormalizeBosDoluForDb(SeciliSefer.BosDolu);
            var boyut = SeciliSefer.KonteynerBoyutu?.Trim();

            if (isEmanetSoda)
            {
                // Boş/Dolu ve boyuttan bağımsız sabit fiyat
                SeciliSefer.Fiyat = 1000m;
                SeciliSefer.Kdv = Math.Round(SeciliSefer.Fiyat > 0 ? SeciliSefer.Fiyat * 0.20m : 0m, 2);
                SeciliSefer.Tevkifat = Math.Round(SeciliSefer.Kdv > 0 ? SeciliSefer.Kdv * 0.20m : 0m, 2);
                SeciliSefer.KdvDahilTutar = Math.Round((SeciliSefer.Fiyat + SeciliSefer.Kdv) - SeciliSefer.Tevkifat, 2);
                return;
            }

            // 2) Temel fiyat: güzergah tanımlarından
            var basePrice = DatabaseService.GetUcretForRoute(SeciliSefer.YuklemeYeri, SeciliSefer.BosaltmaYeri, null, null) ?? 0m;
            decimal result = basePrice;

            // 3) Boş/Dolu kuralı
            if (string.Equals(bosDolu, "Bos", StringComparison.OrdinalIgnoreCase))
            {
                // Boş: 100 TL düş
                result = basePrice - 100m;

                // 4) Boyut kuralı (boş + 20'lik => yarısı)
                if (string.Equals(boyut, "20", StringComparison.OrdinalIgnoreCase))
                {
                    result = result / 2m; // boşta 20'lik ise yarısı
                }
            }
            else
            {
                // Dolu: boyut bağımsız, temel fiyatı kullan
                result = basePrice;
            }

            SeciliSefer.Fiyat = result < 0 ? 0 : result;
            SeciliSefer.Kdv = Math.Round(SeciliSefer.Fiyat > 0 ? SeciliSefer.Fiyat * 0.20m : 0m, 2);
            SeciliSefer.Tevkifat = Math.Round(SeciliSefer.Kdv > 0 ? SeciliSefer.Kdv * 0.20m : 0m, 2);
            SeciliSefer.KdvDahilTutar = Math.Round((SeciliSefer.Fiyat + SeciliSefer.Kdv) - SeciliSefer.Tevkifat, 2);
        }

        private void SeciliSefer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Sefer.YuklemeYeri) ||
                e.PropertyName == nameof(Sefer.BosaltmaYeri))
            {
                // Yükleme ve Boşaltma ilk kez birlikte seçildiğinde varsayılanları uygula
                var nowBoth = !string.IsNullOrWhiteSpace(SeciliSefer?.YuklemeYeri) && !string.IsNullOrWhiteSpace(SeciliSefer?.BosaltmaYeri);
                if (!_routeDefaultsApplied && !_hadBothEndpointsAtStart && !_haveBothEndpoints && nowBoth)
                {
                    _routeDefaultsApplied = true;
                    SeciliSefer!.KonteynerBoyutu = "40";
                    SeciliSefer!.BosDolu = "Dolu";
                    SeciliSefer!.Ekstra = " "; // varsayılan: boşluk
                }
                _haveBothEndpoints = nowBoth;
            }

            if (e.PropertyName == nameof(Sefer.YuklemeYeri) ||
                e.PropertyName == nameof(Sefer.BosaltmaYeri) ||
                e.PropertyName == nameof(Sefer.Ekstra) ||
                e.PropertyName == nameof(Sefer.BosDolu) ||
                e.PropertyName == nameof(Sefer.KonteynerBoyutu) ||
                e.PropertyName == nameof(Sefer.Fiyat))
            {
                RecalcFiyat();
            }
        }

        private static string? NormalizeBosDoluForDb(string? bd)
        {
            if (string.IsNullOrWhiteSpace(bd)) return null;
            if (string.Equals(bd, "Boş", StringComparison.OrdinalIgnoreCase) || string.Equals(bd, "BOS", StringComparison.OrdinalIgnoreCase) || string.Equals(bd, "Bos", StringComparison.OrdinalIgnoreCase)) return "Bos";
            if (string.Equals(bd, "Dolu", StringComparison.OrdinalIgnoreCase)) return "Dolu";
            return bd;
        }

        private bool ValidateSefer(Sefer sefer)
        {
            var eksikAlanlar = new List<string>();
            if (string.IsNullOrWhiteSpace(sefer.KonteynerNo)) eksikAlanlar.Add("Konteyner No");
            if (string.IsNullOrWhiteSpace(sefer.KonteynerBoyutu)) eksikAlanlar.Add("Konteyner Boyutu");
            if (string.IsNullOrWhiteSpace(sefer.YuklemeYeri)) eksikAlanlar.Add("Yükleme Yeri");
            if (string.IsNullOrWhiteSpace(sefer.BosaltmaYeri)) eksikAlanlar.Add("Boşaltma Yeri");
            if (sefer.Tarih == DateTime.MinValue) eksikAlanlar.Add("Tarih");

            if (eksikAlanlar.Count != 0)
            {
                MessageQueue.Enqueue($"Lütfen tüm zorunlu alanları doldurun: {string.Join(", ", eksikAlanlar)}");
                return false;
            }

            if (sefer.KonteynerBoyutu != "20" && sefer.KonteynerBoyutu != "40")
            {
                MessageQueue.Enqueue("Konteyner boyutu yalnızca '20' veya '40' olabilir.");
                return false;
            }

            return true;
        }
    }
}