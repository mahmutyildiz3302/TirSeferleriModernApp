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
    public partial class VergilerAracView : UserControl
    {
        // Bu view i�inde kullan�lan model (DatabaseService bu i�i s�n�f� kullan�r)
        public class VergiArac
        {
            public int VergiId { get; set; }
            public int? CekiciId { get; set; }
            public string? Plaka { get; set; }
            public DateTime Tarih { get; set; }
            public string? VergiTuru { get; set; }
            public string? Donem { get; set; }
            public string? VarlikTipi { get; set; }
            public int? DorseId { get; set; }
            public string? DorsePlaka { get; set; }
            public decimal Tutar { get; set; }
            public string? Aciklama { get; set; }
        }

        private class CekiciItem { public int CekiciId { get; set; } public string Plaka { get; set; } = string.Empty; }
        private VergiArac? _secili;
        private List<VergiArac> _sonListe = new();

        private readonly bool _fixedMode;
        private readonly string? _fixedPlaka;
        private int? _fixedCekiciId;

        public VergilerAracView() : this(null) { }

        public VergilerAracView(string? fixedPlaka)
        {
            InitializeComponent();
            _fixedMode = !string.IsNullOrWhiteSpace(fixedPlaka);
            _fixedPlaka = fixedPlaka;

            DatabaseService.CheckAndCreateOrUpdateVergiAracTablosu();
            LoadCekiciler();

            if (_fixedMode)
            {
                var txtFilt = (TextBlock)FindName("txtSeciliPlakaFilt");
                var cmbFilt = (ComboBox)FindName("cmbFiltreCekici");
                if (txtFilt != null && cmbFilt != null)
                {
                    txtFilt.Visibility = Visibility.Visible;
                    cmbFilt.Visibility = Visibility.Collapsed;
                    txtFilt.Text = _fixedPlaka;
                }
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
            _sonListe = DatabaseService.GetVergiAraclari(cekiciId, bas, bit);

            var headerToplam = BuildToplamSatir(_sonListe);
            var footerToplam = BuildToplamSatir(_sonListe);
            List<VergiArac> list = [headerToplam, .._sonListe, footerToplam];
            dgVergiler.ItemsSource = list;
        }

        private VergiArac BuildToplamSatir(IEnumerable<VergiArac> list)
        {
            return new VergiArac
            {
                VergiId = 0,
                Plaka = _fixedMode ? _fixedPlaka : string.Empty,
                Tarih = DateTime.Today,
                VergiTuru = string.Empty,
                Donem = string.Empty,
                VarlikTipi = string.Empty,
                DorseId = null,
                DorsePlaka = string.Empty,
                Tutar = list.Sum(x => x.Tutar),
                Aciklama = "Toplam"
            };
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseForm(out var g)) return;
            var id = DatabaseService.VergiAracEkle(g);
            if (id > 0)
            {
                ClearForm();
                LoadListe();
            }
        }

        private void BtnGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("G�ncellenecek kay�t se�in"); return; }
            if (!TryParseForm(out var g)) return;
            g.VergiId = _secili.VergiId;
            DatabaseService.VergiAracGuncelle(g);
            ClearForm();
            LoadListe();
        }

        private void BtnSil_Click(object sender, RoutedEventArgs e)
        {
            if (_secili == null) { MessageBox.Show("Silinecek kay�t se�in"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DatabaseService.VergiAracSil(_secili.VergiId);
                ClearForm();
                LoadListe();
            }
        }

        private void BtnTemizle_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            _secili = null;
            if (!_fixedMode) cmbCekici.SelectedIndex = -1;
            cmbVergiTuru.SelectedIndex = -1;
            txtDonem.Text = string.Empty;
            txtTutar.Text = string.Empty;
            txtAciklama.Text = string.Empty;
            dpTarih.SelectedDate = DateTime.Today;
        }

        private bool TryParseForm(out VergiArac g)
        {
            g = new VergiArac();
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
            g.VergiTuru = (cmbVergiTuru.SelectedItem as ComboBoxItem)?.Content?.ToString();
            g.Donem = txtDonem.Text?.Trim();
            g.VarlikTipi = "Cekici"; // varsay�lan
            g.DorseId = null;
            g.DorsePlaka = null;
            if (!decimal.TryParse(txtTutar.Text, out var tutar)) tutar = 0;
            g.Tutar = tutar;
            g.Aciklama = txtAciklama.Text?.Trim();
            return true;
        }

        private void dgVergiler_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is VergiArac row)
            {
                if (row.Aciklama == "Toplam" && row.VergiId == 0) return;
                _secili = row;
                if (!_fixedMode)
                {
                    var cekiciler = (IEnumerable<CekiciItem>)cmbCekici.ItemsSource;
                    cmbCekici.SelectedItem = cekiciler.FirstOrDefault(x => x.CekiciId == row.CekiciId) ?? cekiciler.FirstOrDefault(x => x.Plaka == row.Plaka);
                }
                dpTarih.SelectedDate = row.Tarih;
                foreach (var item in cmbVergiTuru.Items)
                {
                    if (item is ComboBoxItem ci && string.Equals(ci.Content?.ToString(), row.VergiTuru, StringComparison.OrdinalIgnoreCase))
                    { cmbVergiTuru.SelectedItem = item; break; }
                }
                txtDonem.Text = row.Donem;
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
                    FileName = $"vergiler_arac_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                    nfi.NumberDecimalSeparator = "."; // csv i�in nokta

                    var sb = new StringBuilder();
                    sb.AppendLine("Id,Plaka,Tarih,VergiTuru,Donem,VarlikTipi,DorseId,DorsePlaka,Tutar,Aciklama");
                    foreach (var r in _sonListe)
                    {
                        string line = string.Join(",", [
                            r.VergiId.ToString(),
                            Quote(r.Plaka),
                            r.Tarih.ToString("yyyy-MM-dd"),
                            Quote(r.VergiTuru),
                            Quote(r.Donem),
                            Quote(r.VarlikTipi),
                            (r.DorseId ?? 0).ToString(),
                            Quote(r.DorsePlaka),
                            r.Tutar.ToString(nfi),
                            Quote(r.Aciklama)
                        ]);
                        sb.AppendLine(line);
                    }
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("CSV olu�turuldu.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("CSV hatas�: " + ex.Message);
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
