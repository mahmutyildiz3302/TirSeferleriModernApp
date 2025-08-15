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

namespace TirSeferleriModernApp.ViewModels
{
    public partial class SeferlerViewModel(SnackbarMessageQueue messageQueue, DatabaseService databaseService) : ObservableObject
    {
        private Sefer? _seciliSefer;
        public Sefer? SeciliSefer
        {
            get => _seciliSefer;
            set
            {
                if (_seciliSefer != value)
                {
                    _seciliSefer = value;
                    OnPropertyChanged(nameof(SeciliSefer));
                    OnPropertyChanged(nameof(KaydetButonMetni));
                    OnPropertyChanged(nameof(TemizleButonMetni));
                }
            }
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

            if (SeciliSefer.SeferId <= 0)
            {
                SeferEkle(SeciliSefer);
            }
            else
            {
                SeferGuncelle(SeciliSefer);
            }
            // Listeyi yenile
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
        }
    }
}