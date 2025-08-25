using Microsoft.Data.Sqlite;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;

namespace TirSeferleriModernApp.Views
{
    public partial class DepoGuzergahTanimView : UserControl
    {
        private const string ConnectionString = "Data Source=TirSeferleri.db";
        private DataRowView? _seciliDepo;
        private DataRowView? _seciliGuzergah;

        public DepoGuzergahTanimView()
        {
            InitializeComponent();
            LoadDepolar();
            LoadDepoCombos();
            LoadGuzergahlar();
        }

        private void LoadDepolar()
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("SELECT DepoId, DepoAdi, Aciklama FROM Depolar ORDER BY DepoAdi", conn);
            using var rdr = cmd.ExecuteReader();
            var dt = new DataTable(); dt.Load(rdr);
            dgDepolar.ItemsSource = dt.DefaultView;
        }

        private void LoadDepoCombos()
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("SELECT DepoId, DepoAdi FROM Depolar ORDER BY DepoAdi", conn);
            using var rdr = cmd.ExecuteReader();
            var list = new List<KeyValuePair<int, string>>();
            while (rdr.Read()) list.Add(new KeyValuePair<int, string>(rdr.GetInt32(0), rdr.GetString(1)));
            cmbCikisDepo.ItemsSource = list; cmbCikisDepo.DisplayMemberPath = "Value"; cmbCikisDepo.SelectedValuePath = "Key";
            cmbVarisDepo.ItemsSource = new List<KeyValuePair<int, string>>(list); cmbVarisDepo.DisplayMemberPath = "Value"; cmbVarisDepo.SelectedValuePath = "Key";
        }

        private void LoadGuzergahlar()
        {
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand(@"SELECT g.GuzergahId,
                                                       g.CikisDepoId,
                                                       cd.DepoAdi AS CikisDepoAdi,
                                                       g.VarisDepoId,
                                                       vd.DepoAdi AS VarisDepoAdi,
                                                       g.Fiyat,
                                                       g.Aciklama
                                                FROM Guzergahlar g
                                                LEFT JOIN Depolar cd ON cd.DepoId = g.CikisDepoId
                                                LEFT JOIN Depolar vd ON vd.DepoId = g.VarisDepoId
                                                ORDER BY cd.DepoAdi, vd.DepoAdi", conn);
            using var rdr = cmd.ExecuteReader();
            var dt = new DataTable(); dt.Load(rdr);
            // Bind to grouped CollectionViewSource
            var cvs = (CollectionViewSource)FindResource("GuzergahGrouped");
            cvs.Source = dt.DefaultView;
        }

        private void dgDepolar_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is DataRowView row)
            {
                _seciliDepo = row;
                txtDepoAdi.Text = row["DepoAdi"]?.ToString() ?? string.Empty;
                txtDepoAciklama.Text = row["Aciklama"]?.ToString() ?? string.Empty;
            }
        }

        private void BtnDepoKaydet_Click(object sender, RoutedEventArgs e)
        {
            var ad = txtDepoAdi.Text?.Trim();
            var ack = txtDepoAciklama.Text?.Trim();
            if (string.IsNullOrWhiteSpace(ad)) { MessageBox.Show("Depo adi girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("INSERT INTO Depolar (DepoAdi, Aciklama) VALUES (@a, @c)", conn);
            cmd.Parameters.AddWithValue("@a", ad);
            cmd.Parameters.AddWithValue("@c", (object?)ack ?? System.DBNull.Value);
            cmd.ExecuteNonQuery();
            txtDepoAdi.Text = string.Empty; txtDepoAciklama.Text = string.Empty; _seciliDepo = null; LoadDepolar(); LoadDepoCombos();
            LoadGuzergahlar();
        }

        private void BtnDepoGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliDepo == null) { MessageBox.Show("Guncellenecek depo secin"); return; }
            var ad = txtDepoAdi.Text?.Trim();
            var ack = txtDepoAciklama.Text?.Trim();
            if (string.IsNullOrWhiteSpace(ad)) { MessageBox.Show("Depo adi girin"); return; }
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Depolar SET DepoAdi=@a, Aciklama=@c WHERE DepoId=@id", conn);
            cmd.Parameters.AddWithValue("@a", ad);
            cmd.Parameters.AddWithValue("@c", (object?)ack ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@id", _seciliDepo.Row["DepoId"]);
            cmd.ExecuteNonQuery();
            txtDepoAdi.Text = string.Empty; txtDepoAciklama.Text = string.Empty; _seciliDepo = null; LoadDepolar(); LoadDepoCombos(); LoadGuzergahlar();
        }

        private void BtnDepoSil_Click(object sender, RoutedEventArgs e)
        {
            if (dgDepolar.SelectedItem is not DataRowView row) { MessageBox.Show("Silinecek depoyu secin"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("DELETE FROM Depolar WHERE DepoId=@id", conn);
            cmd.Parameters.AddWithValue("@id", row["DepoId"]);
            cmd.ExecuteNonQuery();
            if (_seciliDepo != null && Equals(_seciliDepo.Row["DepoId"], row["DepoId"])) _seciliDepo = null;
            LoadDepolar(); LoadDepoCombos(); LoadGuzergahlar();
        }

        private void dgGuzergah_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as DataGrid)?.SelectedItem is DataRowView row)
            {
                _seciliGuzergah = row;
                cmbCikisDepo.SelectedValue = row["CikisDepoId"];
                cmbVarisDepo.SelectedValue = row["VarisDepoId"];
                txtFiyat.Text = row["Fiyat"]?.ToString() ?? string.Empty;
                txtGuzergahAciklama.Text = row["Aciklama"]?.ToString() ?? string.Empty;
            }
        }

        private void BtnGuzergahKaydet_Click(object sender, RoutedEventArgs e)
        {
            int? cikisId = cmbCikisDepo.SelectedValue is int c ? c : null;
            int? varisId = cmbVarisDepo.SelectedValue is int v ? v : null;
            if (!cikisId.HasValue || !varisId.HasValue) { MessageBox.Show("Cikis ve Varis deposunu secin"); return; }
            _ = decimal.TryParse(txtFiyat.Text, out var fiyat);
            var ack = txtGuzergahAciklama.Text?.Trim();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand(@"INSERT INTO Guzergahlar (CikisDepoId, VarisDepoId, Fiyat, Aciklama)
                                               VALUES (@c, @v, @f, @a)", conn);
            cmd.Parameters.AddWithValue("@c", cikisId.Value);
            cmd.Parameters.AddWithValue("@v", varisId.Value);
            cmd.Parameters.AddWithValue("@f", (double)fiyat);
            cmd.Parameters.AddWithValue("@a", (object?)ack ?? System.DBNull.Value);
            cmd.ExecuteNonQuery();
            ClearGuzergahForm(); LoadGuzergahlar();
        }

        private void BtnGuzergahGuncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_seciliGuzergah == null) { MessageBox.Show("Guncellenecek guzergahi secin"); return; }
            int? cikisId = cmbCikisDepo.SelectedValue is int c ? c : null;
            int? varisId = cmbVarisDepo.SelectedValue is int v ? v : null;
            if (!cikisId.HasValue || !varisId.HasValue) { MessageBox.Show("Cikis ve Varis deposunu secin"); return; }
            _ = decimal.TryParse(txtFiyat.Text, out var fiyat);
            var ack = txtGuzergahAciklama.Text?.Trim();
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand(@"UPDATE Guzergahlar SET CikisDepoId=@c, VarisDepoId=@v, Fiyat=@f, Aciklama=@a WHERE GuzergahId=@id", conn);
            cmd.Parameters.AddWithValue("@c", cikisId.Value);
            cmd.Parameters.AddWithValue("@v", varisId.Value);
            cmd.Parameters.AddWithValue("@f", (double)fiyat);
            cmd.Parameters.AddWithValue("@a", (object?)ack ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@id", _seciliGuzergah.Row["GuzergahId"]);
            cmd.ExecuteNonQuery();
            ClearGuzergahForm(); LoadGuzergahlar();
        }

        private void BtnGuzergahSil_Click(object sender, RoutedEventArgs e)
        {
            if (dgGuzergah.SelectedItem is not DataRowView row) { MessageBox.Show("Silinecek guzergahi secin"); return; }
            if (MessageBox.Show("Silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            using var conn = new SqliteConnection(ConnectionString); conn.Open();
            using var cmd = new SqliteCommand("DELETE FROM Guzergahlar WHERE GuzergahId=@id", conn);
            cmd.Parameters.AddWithValue("@id", row["GuzergahId"]);
            cmd.ExecuteNonQuery();
            if (_seciliGuzergah != null && Equals(_seciliGuzergah.Row["GuzergahId"], row["GuzergahId"])) _seciliGuzergah = null;
            LoadGuzergahlar(); ClearGuzergahForm();
        }

        private void ClearGuzergahForm()
        {
            _seciliGuzergah = null;
            cmbCikisDepo.SelectedIndex = -1;
            cmbVarisDepo.SelectedIndex = -1;
            txtFiyat.Text = string.Empty;
            txtGuzergahAciklama.Text = string.Empty;
        }

        private void BtnTumunuAc_Click(object sender, RoutedEventArgs e)
        {
            ToggleAllGroupsSafe(true);
        }

        private void BtnTumunuKapat_Click(object sender, RoutedEventArgs e)
        {
            ToggleAllGroupsSafe(false);
        }

        private void ToggleAllGroupsSafe(bool expand)
        {
            if (dgGuzergah == null) return;
            // Snapshot list of expanders first to avoid visual tree mutation during traversal
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                var snapshot = FindVisualChildren<Expander>(dgGuzergah).ToList();
                foreach (var exp in snapshot)
                {
                    exp.IsExpanded = expand;
                }
            }), DispatcherPriority.Background);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }
    }
}
