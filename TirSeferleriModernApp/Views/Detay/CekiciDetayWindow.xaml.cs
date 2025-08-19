using Microsoft.Data.Sqlite;
using System;
using System.Windows;

namespace TirSeferleriModernApp.Views.Detay
{
    public partial class CekiciDetayWindow : Window
    {
        private readonly int _cekiciId;
        private const string Conn = "Data Source=TirSeferleri.db";

        public CekiciDetayWindow(int cekiciId)
        {
            InitializeComponent();
            _cekiciId = cekiciId;
            LoadData();
        }

        private void LoadData()
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand(@"SELECT C.Plaka, IFNULL(C.Aktif,0), S.SoforAdi, D.Plaka
                                               FROM Cekiciler C
                                               LEFT JOIN Soforler S ON C.SoforId=S.SoforId
                                               LEFT JOIN Dorseler D ON C.DorseId=D.DorseId
                                               WHERE C.CekiciId=@id", conn);
            cmd.Parameters.AddWithValue("@id", _cekiciId);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                txtPlaka.Text = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                chkAktif.IsChecked = !rdr.IsDBNull(1) && rdr.GetInt32(1) == 1;
                txtSofor.Text = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                txtDorse.Text = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Cekiciler SET Plaka=@p, Aktif=@a WHERE CekiciId=@id", conn);
            cmd.Parameters.AddWithValue("@p", txtPlaka.Text?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@a", chkAktif.IsChecked == true ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", _cekiciId);
            cmd.ExecuteNonQuery();
            MessageBox.Show("Kaydedildi");
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
