using System;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using TirSeferleriModernApp.Views.Shared;
using TirSeferleriModernApp.Models;

namespace TirSeferleriModernApp.Views
{
    public partial class KarHesapSeciliView : UserControl
    {
        private readonly string _plaka;
        public KarHesapSeciliView(string plaka)
        {
            InitializeComponent();
            _plaka = plaka;

            KarHesapShared.EnsureAllTables();

            txtPlaka.Text = plaka;
            dpBas.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpBit.SelectedDate = DateTime.Today;

            HesaplaVeGoster();
        }

        private void HesaplaVeGoster()
        {
            var ozet = KarHesapShared.Hesapla(_plaka, dpBas.SelectedDate, dpBit.SelectedDate);
            txtGider.Text = ozet.ToplamGider.ToString("N2", CultureInfo.CurrentCulture);
            txtKar.Text   = ozet.Kar.ToString("N2", CultureInfo.CurrentCulture);

            var gelirler = KarHesapShared.GetGelirler(_plaka, dpBas.SelectedDate, dpBit.SelectedDate);
            var toplamGelir = gelirler.Sum(x => x.Fiyat);
            txtGelir.Text = toplamGelir.ToString("N2", CultureInfo.CurrentCulture);

            var giderHeader = new KarKalem { Ad = "Toplam", Tutar = ozet.Kalemler.Sum(x => x.Tutar) };
            var giderFooter = new KarKalem { Ad = "Toplam", Tutar = ozet.Kalemler.Sum(x => x.Tutar) };
            var giderList = new System.Collections.Generic.List<KarKalem> { giderHeader };
            giderList.AddRange(ozet.Kalemler);
            giderList.Add(giderFooter);
            dgKalemler.ItemsSource = giderList;

            var gelirHeader = new Sefer { SeferId = 0, KonteynerNo = string.Empty, Tarih = DateTime.Today, Fiyat = toplamGelir, Aciklama = "Toplam", CekiciPlaka = string.Empty };
            var gelirFooter = new Sefer { SeferId = 0, KonteynerNo = string.Empty, Tarih = DateTime.Today, Fiyat = toplamGelir, Aciklama = "Toplam", CekiciPlaka = string.Empty };
            var gelirList = new System.Collections.Generic.List<Sefer> { gelirHeader };
            gelirList.AddRange(gelirler);
            gelirList.Add(gelirFooter);
            dgGelirler.ItemsSource = gelirList;
        }

        private void dpBas_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void dpBit_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
    }
}
