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
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class SeferlerViewModel(SnackbarMessageQueue messageQueue, DatabaseService databaseService) : ObservableObject
    {
        private Sefer? _seciliSefer; // lazy init
        private bool _routeDefaultsApplied;          // bu sefer için otomatik varsayılanlar uygulandı mı?
        private bool _hadBothEndpointsAtStart;       // sefer seçildiğinde her iki nokta zaten dolu miydi?
        private bool _haveBothEndpoints;             // mevcut durumda iki uç da dolu mu? (ilk kez dolu olduğunda tetiklemek için)

        // UI: Kaynak göster/gizle
        private bool _kaynakGoster;
        public bool KaynakGoster
        {
            get => _kaynakGoster;
            set => SetProperty(ref _kaynakGoster, value);
        }

        // Durum çubuğu özetleri
        private int _sqliteSayisi;
        public int SQLiteSayisi
        {
            get => _sqliteSayisi;
            set => SetProperty(ref _sqliteSayisi, value);
        }

        private int _firestoreSayisi;
        public int FirestoreSayisi
        {
            get => _firestoreSayisi;
            set => SetProperty(ref _firestoreSayisi, value);
        }

        private DateTime? _sonYerelOkumaZamani;
        public DateTime? SonYerelOkumaZamani
        {
            get => _sonYerelOkumaZamani;
            set => SetProperty(ref _sonYerelOkumaZamani, value);
        }

        private DateTime? _sonUzakGuncellemeZamani;
        public DateTime? SonUzakGuncellemeZamani
        {
            get => _sonUzakGuncellemeZamani;
            set => SetProperty(ref _sonUzakGuncellemeZamani, value);
        }

        // Tüm verilerin önbelleği (seçilen plaka filtresine göre)
        private List<Sefer> _allSeferlerCache = new();

        // Senkron durum metni (opsiyonel gösterim için)
        private string? _senkronDurumu = "Kapalı";
        public string? SenkronDurumu
        {
            get => _senkronDurumu;
            set => SetProperty(ref _senkronDurumu, value);
        }

        // PATCH: Senkron durum sayaç kontrolü
        private readonly int _defaultIntervalSeconds = Math.Max(5, (AppSettingsHelper.Current?.GetType().GetProperty("SeferlerRefreshSeconds")?.GetValue(AppSettingsHelper.Current) as int?) ?? 60);
        private int _refreshIntervalSeconds;
        private int _countdownRemaining;
        private CancellationTokenSource? _statusTimerCts;
        private Task? _statusTimerTask;
        private string _baseStatus = ""; // Sayaçsız metin
        private volatile bool _pauseCountdown;

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

        // Üst bilgi alanları (seçilen araç/şoför)
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

        public SeferlerViewModel(SnackbarMessageQueue mq, DatabaseService databaseService, bool subscribeStatus = true) : this(mq, databaseService)
        {
            if (subscribeStatus)
            {
                // Global senkron durum değişimlerini dinle ve ekrana yansıt
                _ = SyncStatusHub.Subscribe(status =>
                {
                    _baseStatus = status ?? string.Empty;
                    // Hata/Geçici durumlarında kısa "Uzak: -" ekle
                    var showOffline = !string.IsNullOrWhiteSpace(_baseStatus) && (_baseStatus.Contains("Hata", StringComparison.OrdinalIgnoreCase) || _baseStatus.Contains("Geçici", StringComparison.OrdinalIgnoreCase));
                    var offlineHint = showOffline ? " • Uzak: -" : string.Empty;

                    if (_baseStatus.Contains("Bekliyor", StringComparison.OrdinalIgnoreCase) ||
                        _baseStatus.Contains("Çalışıyor", StringComparison.OrdinalIgnoreCase))
                    {
                        _pauseCountdown = true; // senkron sırasında sayaç duraklasın
                        Application.Current?.Dispatcher?.Invoke(() => SenkronDurumu = _baseStatus + offlineHint);
                    }
                    else if (_baseStatus.Contains("Dinleniyor", StringComparison.OrdinalIgnoreCase) ||
                             _baseStatus.Contains("Güncel", StringComparison.OrdinalIgnoreCase) ||
                             _baseStatus.Contains("Bağlandı", StringComparison.OrdinalIgnoreCase))
                    {
                        _pauseCountdown = false;
                        RestartCountdown();
                        Application.Current?.Dispatcher?.Invoke(() => SenkronDurumu = ComposeCountdownStatus(_baseStatus + offlineHint));
                    }
                    else
                    {
                        Application.Current?.Dispatcher?.Invoke(() => SenkronDurumu = _baseStatus + offlineHint);
                    }
                });

                // Firestore değişikliklerini dinle (UI'yi anında güncelle)
                FirestoreServisi.RecordChangedFromFirestore += id =>
                {
                    Application.Current?.Dispatcher?.Invoke(() => OnRecordChangedFromFirestore(id));
                };

                // Sayaç başlat
                _refreshIntervalSeconds = _defaultIntervalSeconds;
                RestartCountdown();
                EnsureStatusTimer();
            }
        }

#if DEBUG
        // Mini debug senaryosu izleme alanları
        private int _dbgPrevSqlite;
        private int _dbgPrevFs;
        private bool _dbgInitLogged;
        private DateTime? _dbgLastFsEvent;
#endif

        private void OnRecordChangedFromFirestore(int localId)
        {
            var inCache = _allSeferlerCache.FirstOrDefault(x => x.SeferId == localId);
            if (inCache != null) inCache.DataKaynak = DataKaynakTuru.Firestore;

            var inUi = SeferListesi.FirstOrDefault(x => x.SeferId == localId);
            if (inUi != null) inUi.DataKaynak = DataKaynakTuru.Firestore;

            SonUzakGuncellemeZamani = DateTime.Now;
#if DEBUG
            _dbgLastFsEvent = SonUzakGuncellemeZamani;
#endif
            UpdateSummaryUsingDisplayed();
        }

        public void UpdateSelection(string? cekiciPlaka, string? soforAdi)
        {
            SeciliCekiciPlaka = cekiciPlaka;
            SeciliSoforAdi = soforAdi;
            SeciliDorsePlaka = string.IsNullOrWhiteSpace(cekiciPlaka) ? null : DatabaseService.GetDorsePlakaByCekiciPlaka(cekiciPlaka);

            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                RefreshFromDatabaseByPlaka(SeciliCekiciPlaka);
            else
                RefreshFromDatabaseAll();

            RestartCountdown(); // PATCH: manuel seçim de sayaç sıfırlar
        }

        public void LoadSeferler()
        {
            RefreshFromDatabaseAll();
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            RestartCountdown(); // PATCH
        }

        public void LoadSeferler(string cekiciPlaka)
        {
            RefreshFromDatabaseByPlaka(cekiciPlaka);
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            RestartCountdown(); // PATCH
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
            MarkSourcesOnList(_allSeferlerCache); // PATCH: FS/DB rozetleri
            SonYerelOkumaZamani = DateTime.Now;
            ApplyDateFilterAndUpdate();
        }

        private void RefreshFromDatabaseByPlaka(string plaka)
        {
            EnsureYilSecenekleri();
            _allSeferlerCache = DatabaseService.GetSeferlerByCekiciPlaka(plaka);
            MarkSourcesOnList(_allSeferlerCache); // PATCH: FS/DB rozetleri
            SonYerelOkumaZamani = DateTime.Now;
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

        // Son filtre anahtarı (yıl|ay|plaka) ve son gerçek öğe sayısı takip edilir
        private string _lastFilterKey = string.Empty;
        private int _lastRealCount = -1;

        private string BuildFilterKey()
        {
            var plaka = SeciliCekiciPlaka ?? "*";
            var yil = SeciliYil;
            var ay = SeciliAy?.ToString() ?? "*";
            return $"{plaka}|{yil}|{ay}";
        }

        private void SetSeferListWithTotals(IEnumerable<Sefer> data)
        {
            try
            {
                var list = data?.ToList() ?? new List<Sefer>();
                var realCount = list.Count;
                var currentKey = BuildFilterKey();

                // Özet güncelle (sayım değerleri burada set edilir)
                UpdateSummary(list);

                var header = BuildToplamSatir(list);
                var footer = BuildToplamSatir(list);
                var withTotals = new List<Sefer>(list.Count + 2) { header };
                withTotals.AddRange(list);
                withTotals.Add(footer);
                SeferListesi.ReplaceAll(withTotals);

                // Log: tek satırlık özet (başarılı)
                LogService.Info($"Sefer listesi güncellendi — SQLite={SQLiteSayisi}, Firestore={FirestoreSayisi}");

#if DEBUG
                // Beklenmeyen azalış uyarısını filtre değişimi veya ilk ölçümde verme
                if (_dbgInitLogged)
                {
                    var realDiff = realCount - _lastRealCount;
                    if (realDiff < 0 && string.Equals(currentKey, _lastFilterKey, StringComparison.Ordinal))
                    {
                        LogService.Warn($"MiniTest: Beklenmeyen azalış — GerçekΔ={realDiff}");
                    }
                }
                _lastRealCount = realCount;
                _lastFilterKey = currentKey;
                DebugScenarioCheck();
#endif
            }
            catch (System.Exception ex)
            {
                // Hata: ayrı Warning/Error
                LogService.Error("Sefer listesi güncellenemedi", ex);
            }
        }

        private void UpdateSummary(IEnumerable<Sefer> list)
        {
            var real = list?.Where(x => x != null) ?? Enumerable.Empty<Sefer>();
            SQLiteSayisi = real.Count(x => x.DataKaynak == DataKaynakTuru.SQLite);
            FirestoreSayisi = real.Count(x => x.DataKaynak == DataKaynakTuru.Firestore);
        }

        private void UpdateSummaryUsingDisplayed()
        {
            var real = SeferListesi?.Where(x => x != null && !string.Equals(x.Aciklama, "Toplam", System.StringComparison.OrdinalIgnoreCase))
                                    ?? Enumerable.Empty<Sefer>();
            SQLiteSayisi = real.Count(x => x.DataKaynak == DataKaynakTuru.SQLite);
            FirestoreSayisi = real.Count(x => x.DataKaynak == DataKaynakTuru.Firestore);
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

        // PATCH: FS/DB rozetlerini belirle — deterministik: önce id->Records.remote_id
        private void MarkSourcesOnList(List<Sefer> list)
        {
            try
            {
                var idsWithRemote = DatabaseService.GetLocalIdsHavingRemote();
                foreach (var s in list)
                {
                    if (s == null) continue;
                    if (s.SeferId > 0 && idsWithRemote.Contains(s.SeferId))
                        s.DataKaynak = DataKaynakTuru.Firestore;
                    else
                        s.DataKaynak = DataKaynakTuru.SQLite;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MarkSourcesOnList] " + ex.Message);
            }
        }

        // PATCH: Sayaç ve tetikleyici
        private void EnsureStatusTimer()
        {
            if (_statusTimerTask != null) return;
            _statusTimerCts = CancellationTokenSource.CreateLinkedTokenSource(App.AppCts.Token);
            var token = _statusTimerCts.Token;
            _statusTimerTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(1000, token).ConfigureAwait(false);
                        if (_pauseCountdown) continue;
                        if (_countdownRemaining > 0)
                        {
                            _countdownRemaining--;
                            var composed = ComposeCountdownStatus(_baseStatus);
                            Application.Current?.Dispatcher?.Invoke(() => SenkronDurumu = composed);
                        }
                        if (_countdownRemaining <= 0)
                        {
                            // Tetikle: listeyi yeniden oku ve sayaç resetle (UI thread üstünden yap)
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                SenkronDurumu = _baseStatus;
                                if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                                    RefreshFromDatabaseByPlaka(SeciliCekiciPlaka);
                                else
                                    RefreshFromDatabaseAll();
                                RestartCountdown();
                            });
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        Debug.WriteLine("[SeferlerVM] Status timer canceled");
                        break;
                    }
                    catch (TaskCanceledException)
                    {
                        Debug.WriteLine("[SeferlerVM] Status timer task canceled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[SeferlerVM] Status timer error: " + ex.Message);
                    }
                }
            }, token);
        }

        private void RestartCountdown()
        {
            _refreshIntervalSeconds = _defaultIntervalSeconds;
            _countdownRemaining = _refreshIntervalSeconds;
        }

        private string ComposeCountdownStatus(string baseText)
        {
            if (string.IsNullOrWhiteSpace(baseText)) baseText = "";
            if (baseText.Contains("Dinleniyor", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseText} • {_countdownRemaining} sn";
            }
            return baseText;
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

            // Kaydetmeden önce fiyatı hesapla
            RecalcFiyat();

            var seferToPersist = SeciliSefer;

            if (SeciliSefer.SeferId <= 0)
                SeferEkle(SeciliSefer);
            else
                SeferGuncelle(SeciliSefer);

            // Records tablosuna da yazarak senkronu tetikle
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

            // Listeyi yenile (aynı plaka filtresi korunur)
            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                RefreshFromDatabaseByPlaka(SeciliCekiciPlaka);
            else
                RefreshFromDatabaseAll();

            RestartCountdown(); // PATCH: işlem sonrası sayaç reset
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
            RestartCountdown(); // PATCH
        }

        [RelayCommand]
        private void SecimiTemizle()
        {
            SeciliSefer = new Sefer { Tarih = DateTime.Today };
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

#if DEBUG
        private void DebugScenarioCheck()
        {
            // 1) Uygulama açılışı — ilk yüklemede başlangıç sayıları logla
            if (!_dbgInitLogged)
            {
                LogService.Info($"MiniTest: Başlangıç — SQLite={SQLiteSayisi}, Firestore={FirestoreSayisi}");
                _dbgPrevSqlite = SQLiteSayisi;
                _dbgPrevFs = FirestoreSayisi;
                _dbgInitLogged = true;
                return;
            }

            // 2) Değişim analizi
            var sqliteDiff = SQLiteSayisi - _dbgPrevSqlite;
            var fsDiff = FirestoreSayisi - _dbgPrevFs;

            if (sqliteDiff > 0 && fsDiff == 0)
            {
                // İnternet kapalıyken sadece yerel artış beklenir
                LogService.Info($"MiniTest: Yerel artış — SQLite={SQLiteSayisi}, Firestore={FirestoreSayisi}");
            }
            else if (fsDiff > 0)
            {
                // İnternet açılınca dinleyici tetiklenmeli ve Firestore sayısı artmalı
                var recentFs = _dbgLastFsEvent.HasValue && (DateTime.Now - _dbgLastFsEvent.Value).TotalSeconds <= 10;
                if (recentFs)
                    LogService.Info($"MiniTest: Firestore tetiklendi — SQLite={SQLiteSayisi}, Firestore={FirestoreSayisi}");
                else
                    LogService.Warn($"MiniTest: Firestore artışı görüldü ancak tetikleyici doğrulanamadı — SQLite={SQLiteSayisi}, Firestore={FirestoreSayisi}");
            }
            else if (sqliteDiff < 0 || fsDiff < 0)
            {
                // Beklenmeyen azalışlar
                LogService.Warn($"MiniTest: Beklenmeyen azalış — SQLiteΔ={sqliteDiff}, FirestoreΔ={fsDiff}");
            }

            // 3) Değerleri güncelle
            _dbgPrevSqlite = SQLiteSayisi;
            _dbgPrevFs = FirestoreSayisi;
        }
#endif
    }
}