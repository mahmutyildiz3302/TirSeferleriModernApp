using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.Extensions;

namespace TirSeferleriModernApp.ViewModels
{
    public partial class GiderlerViewModel(SnackbarMessageQueue messageQueue) : ObservableObject
    {
        public int? SelectedVehicleId { get; set; }
        public ObservableCollection<Gider> GiderListesi { get; set; } = [];
        public ISnackbarMessageQueue MessageQueue { get; } = messageQueue;

        public void LoadGiderler()
        {
            string query = "SELECT * FROM Giderler WHERE CekiciId = @SelectedVehicleId";
            using var connection = new SqliteConnection(DatabaseService.ConnectionString); // Orijinal tanıma yönlendirildi
            connection.Open();
            using var command = new SqliteCommand(query, connection);
            command.Parameters.AddWithValue("@SelectedVehicleId", SelectedVehicleId ?? (object)DBNull.Value);
            using var reader = command.ExecuteReader();

            var giderler = new List<Gider>();
            while (reader.Read())
            {
                giderler.Add(new Gider
                {
                    GiderId = reader.GetInt32(0),
                    CekiciId = reader.GetInt32(1),
                    // Diğer alanlar...
                });
            }

            GiderListesi.ReplaceAll(giderler);
        }
    }
}
