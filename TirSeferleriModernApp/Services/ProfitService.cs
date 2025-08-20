using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using TirSeferleriModernApp.Models;

namespace TirSeferleriModernApp.Services
{
    public static class ProfitService
    {
        public static KarOzet Hesapla(string? plaka, DateTime? baslangic, DateTime? bitis)
        {
            int? cekiciId = null;
            if (!string.IsNullOrWhiteSpace(plaka))
            {
                var info = DatabaseService.GetVehicleInfoByCekiciPlaka(plaka);
                cekiciId = info.cekiciId;
            }

            var gelir = SumGelir(cekiciId, plaka, baslangic, bitis);

            var kalemler = new List<(string ad, decimal tutar)>
            {
                ("Yakýt",      SumGider("YakitGider",     "Tutar", cekiciId, plaka, baslangic, bitis)),
                ("Sanayi",     SumGider("SanaiGider",     "Tutar", cekiciId, plaka, baslangic, bitis)),
                ("Genel",      SumGider("GenelGider",     "Tutar", cekiciId, plaka, baslangic, bitis)),
                ("Personel",   SumGider("PersonelGider",  "Tutar", cekiciId, plaka, baslangic, bitis)),
                ("Araç Vergi", SumGider("VergiArac",      "Tutar", cekiciId, plaka, baslangic, bitis))
            };

            decimal toplamGider = 0m;
            var kalemDto = new List<KarKalem>();
            foreach (var k in kalemler)
            {
                toplamGider += k.tutar;
                kalemDto.Add(new KarKalem { Ad = k.ad, Tutar = k.tutar });
            }

            return new KarOzet
            {
                Gelir = gelir,
                ToplamGider = toplamGider,
                Kalemler = kalemDto
            };
        }

        private static decimal SumGelir(int? cekiciId, string? plaka, DateTime? baslangic, DateTime? bitis)
        {
            using var con = new SqliteConnection(DatabaseService.ConnectionString);
            con.Open();

            var sql = "SELECT SUM(Fiyat) FROM Seferler WHERE 1=1";
            using var cmd = con.CreateCommand();

            if (cekiciId.HasValue)
            {
                sql += " AND CekiciId = @CekiciId";
                cmd.Parameters.AddWithValue("@CekiciId", cekiciId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(plaka))
            {
                sql += " AND CekiciPlaka = @Plaka";
                cmd.Parameters.AddWithValue("@Plaka", plaka);
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

            cmd.CommandText = sql;
            var val = cmd.ExecuteScalar();
            return (val == null || val is DBNull) ? 0m : Convert.ToDecimal(val);
        }

        private static decimal SumGider(string table, string amountCol, int? cekiciId, string? plaka, DateTime? baslangic, DateTime? bitis)
        {
            using var con = new SqliteConnection(DatabaseService.ConnectionString);
            con.Open();
            var sql = $"SELECT SUM({amountCol}) FROM {table} WHERE 1=1";
            using var cmd = con.CreateCommand();

            if (cekiciId.HasValue)
            {
                sql += " AND (CekiciId = @CekiciId OR CekiciId IS NULL)"; // bazý tablolarda null olabilir
                cmd.Parameters.AddWithValue("@CekiciId", cekiciId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(plaka))
            {
                sql += " AND (Plaka = @Plaka OR Plaka IS NULL)";
                cmd.Parameters.AddWithValue("@Plaka", plaka);
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

            cmd.CommandText = sql;
            var val = cmd.ExecuteScalar();
            return (val == null || val is DBNull) ? 0m : Convert.ToDecimal(val);
        }
    }
}
