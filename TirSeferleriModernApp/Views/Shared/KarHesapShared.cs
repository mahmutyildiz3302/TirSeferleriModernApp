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

        public static List<Sefer> GetGelirler(string? plaka, DateTime? baslangic, DateTime? bitis)
        {
            var result = new List<Sefer>();
            try
            {
                using var con = new SqliteConnection(DatabaseService.ConnectionString);
                con.Open();
                using var cmd = con.CreateCommand();
                var sql = "SELECT SeferId, KonteynerNo, Tarih, Fiyat, Aciklama, CekiciPlaka FROM Seferler WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(plaka))
                {
                    sql += " AND CekiciPlaka = @p";
                    cmd.Parameters.AddWithValue("@p", plaka);
                }
                if (baslangic.HasValue)
                {
                    sql += " AND Tarih >= @Bas";
                    cmd.Parameters.AddWithValue("@Bas", baslangic.Value.ToString("yyyy-MM-dd"));
                }
                if (bitis.HasValue)
                {
                    sql += " AND Tarih <= @Bit";
                    cmd.Parameters.AddWithValue("@Bit", bitis.Value.ToString("yyyy-MM-dd"));
                }
                sql += " ORDER BY Tarih DESC, SeferId DESC";
                cmd.CommandText = sql;
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    result.Add(new Sefer
                    {
                        SeferId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                        KonteynerNo = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                        Tarih = DateTime.TryParse(rdr.IsDBNull(2) ? null : rdr.GetString(2), out var d) ? d : DateTime.Today,
                        Fiyat = rdr.IsDBNull(3) ? 0 : Convert.ToDecimal(rdr.GetDouble(3)),
                        Aciklama = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                        CekiciPlaka = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                    });
                }
            }
            catch { }
            return result;
        }
    }
}
