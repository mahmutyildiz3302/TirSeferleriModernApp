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
    public partial class PersonelGiderView : UserControl
    {
        private class CekiciItem { public int CekiciId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private PersonelGider? _secili;
        private List<PersonelGider> _sonListe = new();

        public PersonelGiderView()
        {
            InitializeComponent();
            DatabaseService.CheckAndCreateOrUpdatePersonelGiderTablosu();
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
            _sonListe = DatabaseService.GetPersonelGiderleri(cekiciId, bas, bit);

            var headerToplam = BuildToplamSatir(_sonListe);
            var footerToplam = BuildToplamSatir(_sonListe);
            var list = new List<PersonelGider> { headerToplam };
            list.AddRange(_sonListe);
            list.Add(footerToplam);
            dgPersonel.ItemsSource = list;
        }

        private PersonelGider BuildToplamSatir(IEnumerable<PersonelGider> list)
        {
            return new PersonelGider
            {
                PersonelGiderId = 0,
                Plaka = string.Empty,
                Tarih = DateTime.Today,
                PersonelAdi = string.Empty,
                Tutar = list.Sum(x => x.Tutar),
                Aciklama = "Toplam"
            };
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseForm(out var g)) return;
            var id = DatabaseService.PersonelGiderEkle(g);
            if (id > 0)
            {
                ClearForm();
                LoadListe();
            }
        }

        private void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Güncellenecek kayýt seçin"); return; }
            if (!TryParseForm(out var g)) return;
            g.PersonelGiderId = _secili.PersonelGiderId;
            DatabaseService.PersonelGiderGuncelle(g);
            ClearForm();
            LoadListe();
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Silinecek kayýt seçin"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DatabaseService.PersonelGiderSil(_secili.PersonelGiderId);
                ClearForm();
                LoadListe();
            }
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _secili = null;
            cmbCekici.SelectedIndex = -1;
            txtPersonel.Text = string.Empty;
            txtTutar.Text = string.Empty;
            txtAciklama.Text = string.Empty;
            dpTarih.SelectedDate = DateTime.Today;
        }

        private bool TryParseForm(out PersonelGider g)
        {
            g = new PersonelGider();
            if (cmbCekici.SelectedItem is CekiciItem item)
            {
                g.CekiciId = item.CekiciId;
                g.Plaka = item.Plaka;
            }
            g.Tarih = dpTarih.SelectedDate ?? DateTime.Today;
            g.PersonelAdi = txtPersonel.Text?.Trim();
            if (!decimal.TryParse(txtTutar.Text, out var tutar)) tutar = 0;
            g.Tutar = tutar;
            g.Aciklama = txtAciklama.Text?.Trim();
            return true;
        }

        private void dgPersonel_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is PersonelGider row)
            {
                if (row.Aciklama == "Toplam" && row.PersonelGiderId == 0) return;
                _secili = row;
                var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                dpTarih.SelectedDate = row.Tarih;
                txtPersonel.Text = row.PersonelAdi;
                txtTutar.Text = row.Tutar.ToString("0.00");
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
                    FileName = $"personel_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    nfi.NumberDecimalSeparator = "."; // csv için nokta

                    var sb = new StringBuilder();
                    sb.AppendLine("Id,Plaka,Tarih,Personel,Tutar,Aciklama");
                    foreach (var r in _sonListe)
                    {
                        string line = string.Join(",", new[]
                        {
                            r.PersonelGiderId.ToString(),
                            Quote(r.Plaka),
                            r.Tarih.ToString("yyyy-MM-dd"),
                            Quote(r.PersonelAdi),
                            r.Tutar.ToString(nfi),
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
