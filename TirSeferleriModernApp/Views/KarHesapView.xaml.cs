using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views
{
    public partial class KarHesapView : UserControl
    {
        private class PlakaItem { public string Plaka { get; set; } = string.Empty; public override string ToString() => Plaka; }

        private readonly bool _fixedMode;
        private readonly string? _fixedPlaka;

        public KarHesapView() : this(null) { }

        public KarHesapView(string? fixedPlaka)
        {
            InitializeComponent();
            _fixedMode = !string.IsNullOrWhiteSpace(fixedPlaka);
            _fixedPlaka = fixedPlaka;

            // İlgili tabloların garanti altına alınması
            DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
            DatabaseService.CheckAndCreateOrUpdateYakitGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateSanaiGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateGenelGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdatePersonelGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateVergiAracTablosu();

            LoadPlakalar();

            if (_fixedMode)
            {
                // Plaka seçim alanını gizle ve sabitle
                try { spPlaka.Visibility = Visibility.Collapsed; } catch { }
                if (!string.IsNullOrWhiteSpace(_fixedPlaka))
                {
                    // Listedeyse seç, yoksa metin olarak ata
                    var list = cmbPlaka.ItemsSource as IEnumerable<PlakaItem>;
                    var item = list?.FirstOrDefault(i => string.Equals(i.Plaka, _fixedPlaka, StringComparison.OrdinalIgnoreCase));
                    if (item != null) cmbPlaka.SelectedItem = item; else cmbPlaka.Text = _fixedPlaka;
                }
            }

            dpBas.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpBit.SelectedDate = DateTime.Today;

            // İlk yüklemede hesapla
            HesaplaVeGoster();
        }

        private void LoadPlakalar()
        {
            try
            {
                var items = new List<PlakaItem>();
                using var con = new SqliteConnection(DatabaseService.ConnectionString);
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT Plaka FROM Cekiciler WHERE IFNULL(Arsivli,0)=0 ORDER BY Plaka";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var p = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(p)) items.Add(new PlakaItem { Plaka = p });
                }
                cmbPlaka.ItemsSource = items;
                if (!_fixedMode && items.Count > 0) cmbPlaka.SelectedIndex = 0;
            }
            catch
            {
                // ignore
            }
        }

        private void HesaplaVeGoster()
        {
            string? plaka;
            if (_fixedMode)
            {
                plaka = _fixedPlaka;
            }
            else
            {
                plaka = (cmbPlaka.SelectedItem as PlakaItem)?.Plaka ?? (cmbPlaka.Text?.Trim() ?? string.Empty);
            }
            DateTime? bas = dpBas.SelectedDate;
            DateTime? bit = dpBit.SelectedDate;

            var ozet = ProfitService.Hesapla(string.IsNullOrWhiteSpace(plaka) ? null : plaka, bas, bit);
            txtGelir.Text = ozet.Gelir.ToString("N2", CultureInfo.CurrentCulture);
            txtGider.Text = ozet.ToplamGider.ToString("N2", CultureInfo.CurrentCulture);
            txtKar.Text   = ozet.Kar.ToString("N2", CultureInfo.CurrentCulture);
            dgKalemler.ItemsSource = ozet.Kalemler;
        }

        private void cmbPlaka_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_fixedMode) return;
            HesaplaVeGoster();
        }
        private void cmbPlaka_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_fixedMode) return;
            HesaplaVeGoster();
        }
        private void dpBas_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void dpBit_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
    }
}