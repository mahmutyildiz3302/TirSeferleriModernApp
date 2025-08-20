using System;
using System.Globalization;
using System.Windows.Controls;
using TirSeferleriModernApp.Views.Shared;

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
            txtGelir.Text = ozet.Gelir.ToString("N2", CultureInfo.CurrentCulture);
            txtGider.Text = ozet.ToplamGider.ToString("N2", CultureInfo.CurrentCulture);
            txtKar.Text   = ozet.Kar.ToString("N2", CultureInfo.CurrentCulture);
            dgKalemler.ItemsSource = ozet.Kalemler;
        }

        private void dpBas_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void dpBit_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
    }
}
