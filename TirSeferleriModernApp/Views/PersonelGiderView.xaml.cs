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

        private readonly bool _fixedMode;
        private readonly string? _fixedPlaka;
        private int? _fixedCekiciId;

        public PersonelGiderView() : this(null) { }

        public PersonelGiderView(string? fixedPlaka)
        {
            InitializeComponent();
            _fixedMode = !string.IsNullOrWhiteSpace(fixedPlaka);
            _fixedPlaka = fixedPlaka;

            DatabaseService.CheckAndCreateOrUpdatePersonelGiderTablosu();
            LoadCekiciler();

            if (_fixedMode)
            {
                // üst filtrede
                var txtFilt = (TextBlock)FindName("txtSeciliPlakaFilt");
                var cmbFilt = (ComboBox)FindName("cmbFiltreCekici");
                if (txtFilt != null && cmbFilt != null)
                {
                    txtFilt.Visibility = Visibility.Visible;
                    cmbFilt.Visibility = Visibility.Collapsed;
                    txtFilt.Text = _fixedPlaka;
                }
                // formda
                var txtForm = (TextBlock)FindName("txtSeciliPlakaForm");
                var cmbForm = (ComboBox)FindName("cmbCekici");
                if (txtForm != null && cmbForm != null)
                {
                    txtForm.Visibility = Visibility.Visible;
                    cmbForm.Visibility = Visibility.Collapsed;
                    txtForm.Text = _fixedPlaka;
                }

                var info = DatabaseService.GetVehicleInfoByCekiciPlaka(_fixedPlaka!);
                _fixedCekiciId = info.cekiciId;
            }

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
            int? cekiciId = _fixedMode ? _fixedCekiciId : cmbFiltreCekici.SelectedValue as int?;
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
                Plaka = _fixedMode ? _fixedPlaka : string.Empty,
                Tarih = DateTime.Today,
                PersonelAdi = string.Empty,
                OdemeTuru = "",
                SgkDonem = "",
                VergiTuru = "",
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
            if (!_fixedMode) cmbCekici.SelectedIndex = -1;
            txtPersonel.Text = string.Empty;
            cmbOdemeTuru.SelectedIndex = -1;
            txtSgkDonem.Text = string.Empty;
            txtVergiTuru.Text = string.Empty;
            txtTutar.Text = string.Empty;
            txtAciklama.Text = string.Empty;
            dpTarih.SelectedDate = DateTime.Today;
        }

        private bool TryParseForm(out PersonelGider g)
        {
            g = new PersonelGider();
            if (_fixedMode)
            {
                g.CekiciId = _fixedCekiciId;
                g.Plaka = _fixedPlaka;
            }
            else if (cmbCekici.SelectedItem is CekiciItem item)
            {
                g.CekiciId = item.CekiciId;
                g.Plaka = item.Plaka;
            }
            g.Tarih = dpTarih.SelectedDate ?? DateTime.Today;
            g.PersonelAdi = txtPersonel.Text?.Trim();
            g.OdemeTuru = (cmbOdemeTuru.SelectedItem as ComboBoxItem)?.Content?.ToString();
            g.SgkDonem = txtSgkDonem.Text?.Trim();
            g.VergiTuru = txtVergiTuru.Text?.Trim();
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
                if (!_fixedMode)
                {
                    var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                    cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                }
                dpTarih.SelectedDate = row.Tarih;
                txtPersonel.Text = row.PersonelAdi;
                foreach (var item in cmbOdemeTuru.Items)
                {
                    if (item is ComboBoxItem ci && string.Equals(ci.Content?.ToString(), row.OdemeTuru, StringComparison.OrdinalIgnoreCase))
                    { cmbOdemeTuru.SelectedItem = item; break; }
                }
                txtSgkDonem.Text = row.SgkDonem;
                txtVergiTuru.Text = row.VergiTuru;
                txtTutar.Text = row.Tutar.ToString("0.00");
                txtAciklama.Text = row.Aciklama;
            }
        }

        private void OnFiltreChanged(object sender, RoutedEventArgs e) => LoadListe();

        private void BtnFiltreTum_Click(object sender, RoutedEventArgs e)
        {
            if (_fixedMode)
            {
                dpBas.SelectedDate = null;
                dpBit.SelectedDate = null;
            }
            else
            {
                cmbFiltreCekici.SelectedIndex = -1;
                dpBas.SelectedDate = null;
                dpBit.SelectedDate = null;
            }
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
                    sb.AppendLine("Id,Plaka,Tarih,Personel,OdemeTuru,SgkDonem,VergiTuru,Tutar,Aciklama");
                    foreach (var r in _sonListe)
                    {
                        string line = string.Join(",", new[]
                        {
                            r.PersonelGiderId.ToString(),
                            Quote(r.Plaka),
                            r.Tarih.ToString("yyyy-MM-dd"),
                            Quote(r.PersonelAdi),
                            Quote(r.OdemeTuru),
                            Quote(r.SgkDonem),
                            Quote(r.VergiTuru),
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
