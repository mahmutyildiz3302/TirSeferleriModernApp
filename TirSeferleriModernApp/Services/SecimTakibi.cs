using TirSeferleriModernApp.Models;
using System.Windows; // Visibility i�in gerekli
using System; // Console i�in gerekli
using System.Diagnostics; // Trace i�in gerekli

namespace TirSeferleriModernApp.Services
{
    public class SecimTakibi
    {
        // Ara� se�imi bilgileri  
        public int? SelectedVehicleId { get; set; }
        public string SecilenAracPlaka { get; set; } = string.Empty;
        public string SecilenAracSofor { get; set; } = string.Empty;

        // Datagrid se�imi bilgileri  
        public Sefer? SecilenSefer { get; set; }

        // Men� se�imi bilgileri  
        public string? AktifMenu { get; set; }

        // Ara� se�imini g�ncelle  
        public void GuncelleAracSecimi(string plaka, string soforAdi)
        {
            SecilenAracPlaka = plaka;
            SecilenAracSofor = soforAdi;
            Trace.WriteLine($"[SecimTakibi] Ara� se�imi g�ncellendi: Plaka={plaka}, �of�r={soforAdi}");
        }

        public void GuncelleSecilenArac(int? vehicleId, string plaka, string sofor)
        {
            SelectedVehicleId = vehicleId;
            SecilenAracPlaka = plaka;
            SecilenAracSofor = sofor;
            Trace.WriteLine($"[SecimTakibi] Se�ilen ara� g�ncellendi: VehicleId={vehicleId}, Plaka={plaka}, �of�r={sofor}");
        }

        // Datagrid se�imini g�ncelle  
        public void GuncelleSeferSecimi(Sefer sefer)
        {
            SecilenSefer = sefer;
            Trace.WriteLine($"[SecimTakibi] Sefer se�imi g�ncellendi: SeferId={sefer.SeferId}, KonteynerNo={sefer.KonteynerNo}, Tarih={sefer.Tarih}");
        }

        // Men� se�imini g�ncelle  
        public void GuncelleMenuSecimi(string menuAdi)
        {
            AktifMenu = menuAdi;
            Trace.WriteLine($"[SecimTakibi] Men� se�imi g�ncellendi: AktifMenu={menuAdi}");
        }

        // Geri d�nme i�lemi  
        public void GeriDon()
        {
            AktifMenu = null;
            Trace.WriteLine("[SecimTakibi] Geri d�nme i�lemi ger�ekle�tirildi. AktifMenu s�f�rland�.");
        }

        // Men� se�imini s�f�rla  
        public void MenuSeciminiSifirla()
        {
            AktifMenu = null;
            Trace.WriteLine("[SecimTakibi] Men� se�imi s�f�rland�.");
        }

        public static void SagPaneliKapat()
        {
            // Sa� paneli kapatma i�lemleri  
            Trace.WriteLine("[SecimTakibi] Sa� panel kapatma i�lemi ger�ekle�tirildi.");
        }
    }
}