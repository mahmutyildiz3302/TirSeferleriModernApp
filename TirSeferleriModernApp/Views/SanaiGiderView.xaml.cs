using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views
{
    public partial class SanaiGiderView : UserControl
    {
        private class CekiciItem { public int CekiciId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private SanaiGider? _secili;
        private List<SanaiGider> _sonListe = new();

        public SanaiGiderView()
        {
            InitializeComponent();
            DatabaseService.CheckAndCreateOrUpdateSanaiGiderTablosu();
            LoadCekiciler();
            dpTarih.SelectedDate = DateTime.Today;
            dpBas.SelectedDate = DateTime.Today.AddDays(-30);
            dpBit.SelectedDate = DateTime.Today;
            LoadListe();
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
            DateTime? bas = dpBas.SelectedDate;
            DateTime? bit = dpBit.SelectedDate;
            _sonListe = DatabaseService.GetSanaiGiderleri(cekiciId, bas, bit);

            var headerToplam = BuildToplamSatir(_sonListe);
            var footerToplam = BuildToplamSatir(_sonListe);
            var list = new List<SanaiGider> { headerToplam };
            list.AddRange(_sonListe);
            list.Add(footerToplam);
            dgSanai.ItemsSource = list;
        }

        private SanaiGider BuildToplamSatir(IEnumerable<SanaiGider> list)
        {
            return new SanaiGider
            {
                SanaiId = 0,
                Plaka = string.Empty,
                Tarih = DateTime.Today,
                Kalem = string.Empty,
                Tutar = list.Sum(x => x.Tutar),
                Km = null,
                Aciklama = "Toplam"
            };
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseForm(out var s)) return;
            var id = DatabaseService.SanaiGiderEkle(s);
            if (id > 0)
            {
                ClearForm();
                LoadListe();
            }
        }

        private void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Güncellenecek kayýt seçin"); return; }
            if (!TryParseForm(out var s)) return;
            s.SanaiId = _secili.SanaiId;
            DatabaseService.SanaiGiderGuncelle(s);
            ClearForm();
            LoadListe();
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Silinecek kayýt seçin"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DatabaseService.SanaiGiderSil(_secili.SanaiId);
                ClearForm();
                LoadListe();
            }
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _secili = null;
            cmbCekici.SelectedIndex = -1;
            txtKalem.Text = string.Empty;
            txtTutar.Text = string.Empty;
            txtKm.Text = string.Empty;
            txtAciklama.Text = string.Empty;
            dpTarih.SelectedDate = DateTime.Today;
        }

        private bool TryParseForm(out SanaiGider s)
        {
            s = new SanaiGider();
            if (cmbCekici.SelectedItem is CekiciItem item)
            {
                s.CekiciId = item.CekiciId;
                s.Plaka = item.Plaka;
            }
            s.Tarih = dpTarih.SelectedDate ?? DateTime.Today;
            s.Kalem = txtKalem.Text?.Trim();
            if (!decimal.TryParse(txtTutar.Text, out var tutar)) tutar = 0;
            if (!int.TryParse(txtKm.Text, out var km)) km = 0;
            s.Tutar = tutar;
            s.Km = km;
            s.Aciklama = txtAciklama.Text?.Trim();
            return true;
        }

        private void dgSanai_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is SanaiGider row)
            {
                if (row.Aciklama == "Toplam" && row.SanaiId == 0) return;
                _secili = row;
                var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                dpTarih.SelectedDate = row.Tarih;
                txtKalem.Text = row.Kalem;
                txtTutar.Text = row.Tutar.ToString("0.00");
                txtKm.Text = row.Km?.ToString();
                txtAciklama.Text = row.Aciklama;
            }
        }

        private void OnFiltreChanged(object sender, RoutedEventArgs e) => LoadListe();

        private void BtnFiltreTum_Click(object sender, RoutedEventArgs e)
        {
            cmbFiltreCekici.SelectedIndex = -1;
            dpBas.SelectedDate = null;
            dpBit.SelectedDate = null;
            LoadListe();
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"sanai_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    nfi.NumberDecimalSeparator = "."; // csv için nokta

                    var sb = new StringBuilder();
                    sb.AppendLine("Id,Plaka,Tarih,Kalem,Tutar,Km,Aciklama");
                    foreach (var r in _sonListe)
                    {
                        string line = string.Join(",", new[]
                        {
                            r.SanaiId.ToString(),
                            Quote(r.Plaka),
                            r.Tarih.ToString("yyyy-MM-dd"),
                            Quote(r.Kalem),
                            r.Tutar.ToString(nfi),
                            (r.Km ?? 0).ToString(),
                            Quote(r.Aciklama)
                        });
                        sb.AppendLine(line);
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("CSV oluþturuldu.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("CSV hatasý: " + ex.Message);
            }
        }

        private static string Quote(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return '"' + s + '"';
            return s;
        }
    }
}
