using Microsoft.Data.Sqlite;
using System.Windows;

namespace TirSeferleriModernApp.Views.Detay
{
    public partial class DorseDetayWindow : Window
    {
        private readonly int _dorseId;
        private const string Conn = "Data Source=TirSeferleri.db";

        public DorseDetayWindow(int dorseId)
        {
            InitializeComponent();
            _dorseId = dorseId;
            LoadData();
        }

        private void LoadData()
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand("SELECT Plaka, IFNULL(Tip,'') FROM Dorseler WHERE DorseId=@id", conn);
            cmd.Parameters.AddWithValue("@id", _dorseId);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                txtPlaka.Text = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                txtTip.Text = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
            }
        }

        private void BtnKaydet_Click(object sender, RoutedEventArgs e)
        {
            using var conn = new SqliteConnection(Conn); conn.Open();
            using var cmd = new SqliteCommand("UPDATE Dorseler SET Plaka=@p, Tip=@t WHERE DorseId=@id", conn);
            cmd.Parameters.AddWithValue("@p", txtPlaka.Text?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@t", txtTip.Text?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@id", _dorseId);
            cmd.ExecuteNonQuery();
            MessageBox.Show("Kaydedildi");
        }

        private void BtnKapat_Click(object sender, RoutedEventArgs e) => Close();
    }
}
