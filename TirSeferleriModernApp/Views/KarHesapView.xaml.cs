using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Views.Shared;

namespace TirSeferleriModernApp.Views
{
    public partial class KarHesapView : UserControl
    {
        private class PlakaItem
        {
            public string Plaka { get; set; } = string.Empty;
            public bool IsAll { get; set; }
            public override string ToString() => Plaka;
        }

        public KarHesapView()
        {
            InitializeComponent();
            KarHesapShared.EnsureAllTables();
            LoadPlakalar();
            dpBas.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpBit.SelectedDate = DateTime.Today;
            HesaplaVeGoster();
        }

        private void LoadPlakalar()
        {
            var plakalar = KarHesapShared.GetActivePlakalar();
            var items = new List<PlakaItem>();
            // "Tümü" seçeneği en başa ekle
            items.Add(new PlakaItem { Plaka = "Tümü", IsAll = true });
            items.AddRange(plakalar.Select(p => new PlakaItem { Plaka = p }));
            cmbPlaka.ItemsSource = items;
            cmbPlaka.SelectedIndex = 0; // varsayılan: Tümü
        }

        private (string? plaka, bool isAll) GetSelectedPlakaOrAll()
        {
            if (cmbPlaka.SelectedItem is PlakaItem sel)
            {
                return (sel.IsAll ? null : sel.Plaka, sel.IsAll);
            }
            var text = cmbPlaka.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "Tümü", StringComparison.OrdinalIgnoreCase))
                return (text, false);
            return (null, true);
        }

        private void HesaplaVeGoster()
        {
            var (plaka, isAll) = GetSelectedPlakaOrAll();
            var ozet = KarHesapShared.Hesapla(plaka, dpBas.SelectedDate, dpBit.SelectedDate);
            txtGelir.Text = ozet.Gelir.ToString("N2", CultureInfo.CurrentCulture);
            txtGider.Text = ozet.ToplamGider.ToString("N2", CultureInfo.CurrentCulture);
            txtKar.Text   = ozet.Kar.ToString("N2", CultureInfo.CurrentCulture);
            dgKalemler.ItemsSource = ozet.Kalemler;

            // Gelir listesi
            var gelirler = KarHesapShared.GetGelirler(plaka, dpBas.SelectedDate, dpBit.SelectedDate);
            dgGelirler.ItemsSource = gelirler;
        }

        private void cmbPlaka_SelectionChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void cmbPlaka_LostFocus(object sender, RoutedEventArgs e) => HesaplaVeGoster();
        private void dpBas_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void dpBit_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
    }
}