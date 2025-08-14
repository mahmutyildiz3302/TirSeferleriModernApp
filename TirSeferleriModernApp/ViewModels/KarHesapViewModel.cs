using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Data.Sqlite;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Extensions;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class KarHesapViewModel(SnackbarMessageQueue messageQueue) : ObservableObject
    {
        public int? SelectedVehicleId { get; set; }
        public ObservableCollection<KarHesap> KarHesapListesi { get; set; } = [];
        public ISnackbarMessageQueue MessageQueue { get; } = messageQueue;

        public void LoadKarHesap()
        {
            string query = "SELECT * FROM KarHesap WHERE CekiciId = @SelectedVehicleId";
            using var connection = new SqliteConnection(DatabaseService.ConnectionString);
            connection.Open();
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@SelectedVehicleId", SelectedVehicleId ?? (object)DBNull.Value);
            using var reader = command.ExecuteReader();

            var karHesaplar = new List<KarHesap>();
            while (reader.Read())
            {
                karHesaplar.Add(new KarHesap
                {
                    KarHesapId = reader.GetInt32(0),
                    CekiciId = reader.GetInt32(1),
                    // Diğer alanlar...
                });
            }

            KarHesapListesi.ReplaceAll(karHesaplar);
        }
    }
}
