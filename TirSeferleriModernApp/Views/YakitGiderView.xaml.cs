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
    public partial class YakitGiderView : UserControl
    {
        private class CekiciItem { public int CekiciId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private YakitGider? _secili; // seçili kayýt
        private List<YakitGider> _sonListe = new(); // sadece veri kayýtlarý

        private readonly bool _fixedMode;
        private readonly string? _fixedPlaka;
        private int? _fixedCekiciId;

        public YakitGiderView() : this(null) { }

        public YakitGiderView(string? fixedPlaka)
        {
            InitializeComponent();
            _fixedMode = !string.IsNullOrWhiteSpace(fixedPlaka);
            _fixedPlaka = fixedPlaka;

            DatabaseService.CheckAndCreateOrUpdateYakitGiderTablosu();
            LoadCekiciler();

            if (_fixedMode)
            {
                var txtFilt = (TextBlock)FindName("txtSeciliPlakaFilt");
                var cmbFilt = (ComboBox)FindName("cmbFiltreCekici");
                var txtForm = (TextBlock)FindName("txtSeciliPlakaForm");
                var cmbForm = (ComboBox)FindName("cmbCekici");
                if (txtFilt != null && cmbFilt != null)
                {
                    txtFilt.Visibility = Visibility.Visible;
                    cmbFilt.Visibility = Visibility.Collapsed;
                    txtFilt.Text = _fixedPlaka;
                }
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
            _sonListe = DatabaseService.GetYakitGiderleri(cekiciId, bas, bit);

            // Tek grid: [Toplam] + veri + [Toplam]
            var headerToplam = BuildToplamSatir(_sonListe);
            var footerToplam = BuildToplamSatir(_sonListe);
            var list = new List<YakitGider> { headerToplam };
            list.AddRange(_sonListe);
            list.Add(footerToplam);
            dgYakit.ItemsSource = list;
        }

        private YakitGider BuildToplamSatir(IEnumerable<YakitGider> list)
        {
            return new YakitGider
            {
                YakitId = 0,
                Plaka = _fixedMode ? _fixedPlaka : "",
                Tarih = DateTime.Today,
                Istasyon = "",
                Litre = list.Sum(x => x.Litre),
                BirimFiyat = 0,
                Tutar = list.Sum(x => x.Tutar),
                Km = null,
                Aciklama = "Toplam"
            };
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
            if (!_fixedMode) cmbCekici.SelectedIndex = -1;
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
            if (_fixedMode)
            {
                y.CekiciId = _fixedCekiciId;
                y.Plaka = _fixedPlaka;
            }
            else if (cmbCekici.SelectedItem is CekiciItem item)
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
            if ((sender as DataGrid)?.SelectedItem is YakitGider row)
            {
                if (row.Aciklama == "Toplam" && row.YakitId == 0) return; // toplam satýrý açma
                _secili = row;
                if (!_fixedMode)
                {
                    var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                    cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                }
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

        private void OnFiltreChanged(object sender, RoutedEventArgs e) => LoadListe();

        private void BtnFiltreTum_Click(object sender, RoutedEventArgs e)
        {
            if (_fixedMode)
            {
                // sabit modda tümü butonu sadece tarih filtrelerini temizlesin
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
                    FileName = $"yakit_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    char decSep = '.';
                    if (cmbOndalik.SelectedItem is ComboBoxItem ci && ci.Tag is string tag && tag == ",")
                        decSep = ',';
                    var nfi = (NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
                    nfi.NumberDecimalSeparator = decSep.ToString();

                    var exportList = _sonListe; // sadece veri kayýtlarýný yaz
                    var sb = new StringBuilder();
                    sb.AppendLine("Id,Plaka,Tarih,Istasyon,Litre,BirimFiyat,Tutar,Km,Aciklama");
                    foreach (var r in exportList)
                    {
                        string litre = r.Litre.ToString(nfi);
                        string bf = r.BirimFiyat.ToString(nfi);
                        string tutar = r.Tutar.ToString(nfi);
                        string line = string.Join(",", new[]
                        {
                            r.YakitId.ToString(),
                            Quote(r.Plaka),
                            r.Tarih.ToString("yyyy-MM-dd"),
                            Quote(r.Istasyon),
                            litre,
                            bf,
                            tutar,
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
