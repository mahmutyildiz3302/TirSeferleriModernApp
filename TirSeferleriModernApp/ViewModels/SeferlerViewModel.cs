using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Extensions;
using MaterialDesignThemes.Wpf;
using Microsoft.Data.Sqlite;
using System.ComponentModel;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class SeferlerViewModel(SnackbarMessageQueue messageQueue, DatabaseService databaseService) : ObservableObject
    {
        private Sefer? _seciliSefer = new Sefer { Tarih = DateTime.Today }; // İlk açılışta null olmasın
        public Sefer? SeciliSefer
        {
            get => _seciliSefer;
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
        public ObservableCollection<string> DepoAdlari { get; } = new();
        public ObservableCollection<string> EkstraAdlari { get; } = new() { "EKSTRA YOK", "SODA", "EMANET" };
        public ObservableCollection<string> BosDoluSecenekleri { get; } = new() { "Boş", "Dolu" };

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
            var ekstraParam = NormalizeEkstraForDb(SeciliSefer.Ekstra);
            var bosDoluParam = NormalizeBosDoluForDb(SeciliSefer.BosDolu);
            var u = DatabaseService.GetUcretForRoute(SeciliSefer.YuklemeYeri, SeciliSefer.BosaltmaYeri, ekstraParam, bosDoluParam);
            if (u.HasValue) SeciliSefer.Fiyat = u.Value;
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

        private static string? NormalizeEkstraForDb(string? ekstra)
        {
            if (string.IsNullOrWhiteSpace(ekstra)) return null;
            if (string.Equals(ekstra, "EKSTRA YOK", StringComparison.OrdinalIgnoreCase)) return null;
            if (string.Equals(ekstra, "SODA", StringComparison.OrdinalIgnoreCase)) return "Soda";
            if (string.Equals(ekstra, "EMANET", StringComparison.OrdinalIgnoreCase)) return "Emanet";
            return ekstra;
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