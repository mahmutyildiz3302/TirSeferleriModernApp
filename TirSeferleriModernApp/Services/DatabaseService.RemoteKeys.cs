// PATCH: Records.remote_id var olan yerel id’leri getir (deterministik eþleþtirme için yardýmcý metot).
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TirSeferleriModernApp.Models;
using static TirSeferleriModernApp.Views.VergilerAracView;

namespace TirSeferleriModernApp.Services
{
    public partial class DatabaseService
    {
        public static HashSet<int> GetLocalIdsHavingRemote()
        {
            var set = new HashSet<int>();
            try
            {
                EnsureDatabaseFileStatic();
                using var conn = new SqliteConnection(ConnectionString); conn.Open();
                using var cmd = conn.CreateCommand();
                // PATCH: Sadece senkronu tamamlanmýþ (is_dirty=0) kayýtlarý FS olarak iþaretle
                cmd.CommandText = "SELECT id FROM Records WHERE IFNULL(TRIM(remote_id),'')<>'' AND IFNULL(is_dirty,0)=0";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    if (!rdr.IsDBNull(0)) set.Add(rdr.GetInt32(0));
                }
            }
            catch (Exception ex) { Debug.WriteLine("[GetLocalIdsHavingRemote] " + ex.Message); }
            return set;
        }
    }
}
