using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views
{
    public partial class YakitGiderView : UserControl
    {
        private class CekiciItem { public int CekiciId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private YakitGider? _secili; // seçili kayýt

        public YakitGiderView()
        {
            InitializeComponent();
            DatabaseService.CheckAndCreateOrUpdateYakitGiderTablosu();
            LoadCekiciler();
            LoadListe();
            dpTarih.SelectedDate = DateTime.Today;
        }

        private void LoadCekiciler()
        {
            var list = new List<CekiciItem>();
            try
            {
                using var conn = new SqliteConnection(DatabaseService.ConnectionString);
                conn.Open();
                using var cmd = new SqliteCommand("SELECT CekiciId, Plaka FROM Cekiciler WHERE IFNULL(Arsivli,0)=0 ORDER BY Plaka", conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    list.Add(new CekiciItem { CekiciId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0), Plaka = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1) });
            }
            catch { }
            cmbCekici.ItemsSource = list;
            cmbFiltreCekici.ItemsSource = list.ToList();
        }

        private void LoadListe()
        {
            int? cekiciId = cmbFiltreCekici.SelectedValue as int?;
            var data = DatabaseService.GetYakitGiderleri(cekiciId);
            dgYakit.ItemsSource = data;
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseForm(out var y)) return;
            var id = DatabaseService.YakitEkle(y);
            if (id > 0)
            {
                ClearForm();
                LoadListe();
            }
        }

        private void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Güncellenecek kayýt seçin"); return; }
            if (!TryParseForm(out var y)) return;
            y.YakitId = _secili.YakitId;
            DatabaseService.YakitGuncelle(y);
            ClearForm();
            LoadListe();
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Silinecek kayýt seçin"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DatabaseService.YakitSil(_secili.YakitId);
                ClearForm();
                LoadListe();
            }
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _secili = null;
            cmbCekici.SelectedIndex = -1;
            txtIstasyon.Text = string.Empty;
            txtLitre.Text = string.Empty;
            txtBirimFiyat.Text = string.Empty;
            txtTutar.Text = string.Empty;
            txtKm.Text = string.Empty;
            txtAciklama.Text = string.Empty;
            dpTarih.SelectedDate = DateTime.Today;
        }

        private bool TryParseForm(out YakitGider y)
        {
            y = new YakitGider();
            if (cmbCekici.SelectedItem is CekiciItem item)
            {
                y.CekiciId = item.CekiciId;
                y.Plaka = item.Plaka;
            }
            y.Tarih = dpTarih.SelectedDate ?? DateTime.Today;
            y.Istasyon = txtIstasyon.Text?.Trim();
            if (!decimal.TryParse(txtLitre.Text, out var litre)) litre = 0;
            if (!decimal.TryParse(txtBirimFiyat.Text, out var bf)) bf = 0;
            if (!int.TryParse(txtKm.Text, out var km)) km = 0;
            y.Litre = litre;
            y.BirimFiyat = bf;
            y.Tutar = Math.Round(litre * bf, 2);
            y.Km = km;
            y.Aciklama = txtAciklama.Text?.Trim();
            txtTutar.Text = y.Tutar.ToString("0.00");
            return true;
        }

        private void dgYakit_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgYakit.SelectedItem is YakitGider row)
            {
                _secili = row;
                // form doldur
                var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                dpTarih.SelectedDate = row.Tarih;
                txtIstasyon.Text = row.Istasyon;
                txtLitre.Text = row.Litre.ToString();
                txtBirimFiyat.Text = row.BirimFiyat.ToString();
                txtTutar.Text = row.Tutar.ToString("0.00");
                txtKm.Text = row.Km?.ToString();
                txtAciklama.Text = row.Aciklama;
            }
        }

        private void OnLitreBirimFiyatChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(txtLitre.Text, out var litre) && decimal.TryParse(txtBirimFiyat.Text, out var bf))
                txtTutar.Text = (litre * bf).ToString("0.00");
            else
                txtTutar.Text = string.Empty;
        }

        private void cmbFiltreCekici_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadListe();
        private void BtnFiltreTum_Click(object sender, RoutedEventArgs e) { cmbFiltreCekici.SelectedIndex = -1; LoadListe(); }
    }
}
