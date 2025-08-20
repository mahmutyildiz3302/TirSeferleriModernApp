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
        private class PlakaItem { public string Plaka { get; set; } = string.Empty; public override string ToString() => Plaka; }

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
            var items = plakalar.Select(p => new PlakaItem { Plaka = p }).ToList();
            cmbPlaka.ItemsSource = items;
            if (items.Count > 0) cmbPlaka.SelectedIndex = 0;
        }

        private void HesaplaVeGoster()
        {
            string? plaka = (cmbPlaka.SelectedItem as PlakaItem)?.Plaka ?? (cmbPlaka.Text?.Trim() ?? string.Empty);
            var ozet = KarHesapShared.Hesapla(string.IsNullOrWhiteSpace(plaka) ? null : plaka, dpBas.SelectedDate, dpBit.SelectedDate);
            txtGelir.Text = ozet.Gelir.ToString("N2", CultureInfo.CurrentCulture);
            txtGider.Text = ozet.ToplamGider.ToString("N2", CultureInfo.CurrentCulture);
            txtKar.Text   = ozet.Kar.ToString("N2", CultureInfo.CurrentCulture);
            dgKalemler.ItemsSource = ozet.Kalemler;
        }

        private void cmbPlaka_SelectionChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void cmbPlaka_LostFocus(object sender, RoutedEventArgs e) => HesaplaVeGoster();
        private void dpBas_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
        private void dpBit_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => HesaplaVeGoster();
    }
}