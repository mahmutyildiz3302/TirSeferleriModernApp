using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Extensions;
using MaterialDesignThemes.Wpf;
using System.ComponentModel;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class SeferlerViewModel(SnackbarMessageQueue messageQueue, DatabaseService databaseService) : ObservableObject
    {
        private Sefer? _seciliSefer; // lazy init
        public Sefer? SeciliSefer
        {
            get
            {
                if (_seciliSefer == null)
                {
                    _seciliSefer = new Sefer { Tarih = DateTime.Today };
                    _seciliSefer.PropertyChanged += SeciliSefer_PropertyChanged;
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
                        _seciliSefer.PropertyChanged += SeciliSefer_PropertyChanged;

                    OnPropertyChanged(nameof(SeciliSefer));
                    OnPropertyChanged(nameof(KaydetButonMetni));
                    OnPropertyChanged(nameof(TemizleButonMetni));
                    RecalcFiyat();
                }
            }
        }

        // Depo/ekstra/bos-dolu seçim listeleri
        public ObservableCollection<string> DepoAdlari { get; } = [];
        public ObservableCollection<string> EkstraAdlari { get; } = ["EKSTRA YOK", "SODA", "EMANET"];
        public ObservableCollection<string> BosDoluSecenekleri { get; } = ["Boş", "Dolu"];

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
                LoadSeferler(SeciliCekiciPlaka);
            else
                LoadSeferler();
        }

        public string KaydetButonMetni => SeciliSefer?.SeferId > 0 ? "Seçimi Güncelle" : "Yeni Sefer Kaydet";
        public string TemizleButonMetni => "Temizle";

        public ObservableCollection<Sefer> SeferListesi { get; set; } = [];

        public ISnackbarMessageQueue MessageQueue { get; } = messageQueue;

        private readonly DatabaseService _databaseService = databaseService;

        [RelayCommand]
        private void KaydetVeyaGuncelle()
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

            // Her kaydet/güncelle öncesi fiyatı güzergah tablosuna göre güncelle
            RecalcFiyat();

            if (SeciliSefer.SeferId <= 0)
            {
                SeferEkle(SeciliSefer);
            }
            else
            {
                SeferGuncelle(SeciliSefer);
            }
            // Listeyi, filtre korunarak yenile
            if (!string.IsNullOrWhiteSpace(SeciliCekiciPlaka))
                SeferListesi.ReplaceAll(DatabaseService.GetSeferlerByCekiciPlaka(SeciliCekiciPlaka));
            else
                SeferListesi.ReplaceAll(DatabaseService.GetSeferler());
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

        [RelayCommand]
        private void SecimiTemizle()
        {
            // Formu gerçekten temizlemek için yeni boş bir Sefer atıyoruz
            SeciliSefer = new Sefer { Tarih = DateTime.Today };
            MessageQueue.Enqueue("Form temizlendi.");
        }

        public int? SelectedVehicleId { get; set; }

        public void LoadSeferler()
        {
            SeferListesi.ReplaceAll(DatabaseService.GetSeferler());
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            // EkstraAdlari sabit; DB'den doldurulmayacak
        }

        public void LoadSeferler(string cekiciPlaka)
        {
            SeferListesi.ReplaceAll(DatabaseService.GetSeferlerByCekiciPlaka(cekiciPlaka));
            DepoAdlari.ReplaceAll(DatabaseService.GetDepoAdlari());
            // EkstraAdlari sabit; DB'den doldurulmayacak
        }

        private void RecalcFiyat()
        {
            if (SeciliSefer == null) return;
            var bosDoluParam = NormalizeBosDoluForDb(SeciliSefer.BosDolu);
            var ekstra = SeciliSefer.Ekstra;
            var u = DatabaseService.GetUcretForRoute(SeciliSefer.YuklemeYeri, SeciliSefer.BosaltmaYeri, ekstra, bosDoluParam) ?? 0m;

            // Kural: Boş/Dolu seçimi yapılmamışsa ve boyut 20 ise fiyatı ikiye böl
            if (string.IsNullOrWhiteSpace(SeciliSefer.BosDolu) && string.Equals(SeciliSefer.KonteynerBoyutu, "20", StringComparison.OrdinalIgnoreCase))
            {
                SeciliSefer.Fiyat = u / 2m;
            }
            else
            {
                SeciliSefer.Fiyat = u;
            }
        }

        private void SeciliSefer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Sefer.YuklemeYeri) ||
                e.PropertyName == nameof(Sefer.BosaltmaYeri) ||
                e.PropertyName == nameof(Sefer.Ekstra) ||
                e.PropertyName == nameof(Sefer.BosDolu) ||
                e.PropertyName == nameof(Sefer.KonteynerBoyutu))
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
    }
}