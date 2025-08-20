using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views.Shared
{
    // Ortak arka-plan (behind) yardýmcý sýnýfý: Her iki kar ekraný buradan veri çeker
    public static class KarHesapShared
    {
        public static void EnsureAllTables()
        {
            DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
            DatabaseService.CheckAndCreateOrUpdateYakitGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateSanaiGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateGenelGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdatePersonelGiderTablosu();
            DatabaseService.CheckAndCreateOrUpdateVergiAracTablosu();
        }

        public static List<string> GetActivePlakalar()
        {
            var list = new List<string>();
            try
            {
                using var con = new SqliteConnection(DatabaseService.ConnectionString);
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT Plaka FROM Cekiciler WHERE IFNULL(Arsivli,0)=0 ORDER BY Plaka";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var p = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    if (!string.IsNullOrWhiteSpace(p)) list.Add(p);
                }
            }
            catch { }
            return list;
        }

        public static KarOzet Hesapla(string? plaka, DateTime? baslangic, DateTime? bitis)
        {
            // Tüm hesaplama ProfitService üzerinden geçer; tek noktadan eriþim
            return Services.ProfitService.Hesapla(plaka, baslangic, bitis);
        }
    }
}
