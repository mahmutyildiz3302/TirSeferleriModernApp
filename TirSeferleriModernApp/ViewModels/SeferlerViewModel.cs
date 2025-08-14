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

        public string KaydetButonMetni => SeciliSefer?.SeferId > 0 ? "Seçimi Güncelle" : "Yeni Sefer Kaydet";
        public string TemizleButonMetni => SeciliSefer != null ? "Seçimi Bırak" : "Temizle";

        public ObservableCollection<Sefer> SeferListesi { get; set; } = [];

        public ISnackbarMessageQueue MessageQueue { get; } = messageQueue;

        private readonly DatabaseService _databaseService = databaseService;

        [RelayCommand]
        private void KaydetVeyaGuncelle()
        {
            SeciliSefer ??= new Sefer();

            if (SeciliSefer.SeferId <= 0)
            {
                SeferEkle(SeciliSefer);
            }
            else
            {
                SeferGuncelle(SeciliSefer);
            }
        }

        private void SeferGuncelle(Sefer guncellenecekSefer)
        {
            if (!ValidateSefer(guncellenecekSefer)) return;

            SeferGuncelle(guncellenecekSefer);

            var mevcutSefer = SeferListesi.FirstOrDefault(s => s.SeferId == guncellenecekSefer.SeferId);
            if (mevcutSefer != null)
            {
                mevcutSefer.KonteynerNo = guncellenecekSefer.KonteynerNo;
                mevcutSefer.KonteynerBoyutu = guncellenecekSefer.KonteynerBoyutu;
                mevcutSefer.YuklemeYeri = guncellenecekSefer.YuklemeYeri;
                mevcutSefer.BosaltmaYeri = guncellenecekSefer.BosaltmaYeri;
                mevcutSefer.Tarih = guncellenecekSefer.Tarih;
                mevcutSefer.Saat = guncellenecekSefer.Saat;
                mevcutSefer.Fiyat = guncellenecekSefer.Fiyat;
                mevcutSefer.Aciklama = guncellenecekSefer.Aciklama;
            }

            MessageQueue.Enqueue($"{guncellenecekSefer.KonteynerNo} numaralı konteyner seferi başarıyla güncellendi!");
            SeciliSefer = null;
        }

        private void SeferEkle(Sefer yeniSefer)
        {
            if (!ValidateSefer(yeniSefer)) return;

            DatabaseService.SeferEkle(yeniSefer);
            SeferListesi.Add(yeniSefer);

            MessageQueue.Enqueue($"{yeniSefer.KonteynerNo} numaralı konteyner seferi başarıyla eklendi!");
            SeciliSefer = null;
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
            SeciliSefer = null;
            MessageQueue.Enqueue("Seçim temizlendi.");
        }

        public int? SelectedVehicleId { get; set; }

        public void LoadSeferler()
        {
            string query = "SELECT * FROM Seferler WHERE CekiciId = @SelectedVehicleId";
            using var connection = new SqliteConnection(DatabaseService.ConnectionString);
            connection.Open();
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@SelectedVehicleId", SelectedVehicleId ?? (object)DBNull.Value);
            using var reader = command.ExecuteReader();

            var seferler = new List<Sefer>();
            while (reader.Read())
            {
                seferler.Add(new Sefer
                {
                    SeferId = reader.GetInt32(0),
                    CekiciId = reader.GetInt32(1),
                    // Diğer alanlar...
                });
            }

            SeferListesi.ReplaceAll(seferler);
        }
    }
}