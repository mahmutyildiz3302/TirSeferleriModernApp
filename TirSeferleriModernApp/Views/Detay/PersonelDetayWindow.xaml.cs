using Microsoft.Data.Sqlite;
using System.Windows;

namespace TirSeferleriModernApp.Views.Detay
{
    public partial class PersonelDetayWindow : Window
    {
        private readonly int _soforId;
        private const string Conn = "Data Source=TirSeferleri.db";

        public PersonelDetayWindow(int soforId)
        {
            InitializeComponent();
            _soforId = soforId;
            LoadData();
        }

        private void LoadData()
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand("SELECT SoforAdi, IFNULL(Telefon,'') FROM Soforler WHERE SoforId=@id", conn);
            cmd.Parameters.AddWithValue("@id", _soforId);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                txtAd.Text = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                txtTelefon.Text = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Soforler SET SoforAdi=@a, Telefon=@t WHERE SoforId=@id", conn);
            cmd.Parameters.AddWithValue("@a", txtAd.Text?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@t", txtTelefon.Text?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@id", _soforId);
            cmd.ExecuteNonQuery();
            MessageBox.Show("Kaydedildi");
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
