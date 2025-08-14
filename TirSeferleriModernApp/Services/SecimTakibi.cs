using TirSeferleriModernApp.Models;
using System.Windows; // Visibility için gerekli
using System; // Console için gerekli
using System.Diagnostics; // Debug için gerekli

namespace TirSeferleriModernApp.Services
{
    public class SecimTakibi
    {
        // Araç seçimi bilgileri  
        public int? SelectedVehicleId { get; set; }
        public string SecilenAracPlaka { get; set; } = string.Empty;
        public string SecilenAracSofor { get; set; } = string.Empty;

        // Datagrid seçimi bilgileri  
        public Sefer? SecilenSefer { get; set; }

        // Menü seçimi bilgileri  
        public string? AktifMenu { get; set; }

        // Araç seçimini güncelle  
        public void GuncelleAracSecimi(string plaka, string soforAdi)
        {
            SecilenAracPlaka = plaka;
            SecilenAracSofor = soforAdi;
            Debug.WriteLine($"[SecimTakibi] Araç seçimi güncellendi: Plaka={plaka}, Þoför={soforAdi}");
        }

        public void GuncelleSecilenArac(int? vehicleId, string plaka, string sofor)
        {
            SelectedVehicleId = vehicleId;
            SecilenAracPlaka = plaka;
            SecilenAracSofor = sofor;
            Debug.WriteLine($"[SecimTakibi] Seçilen araç güncellendi: VehicleId={vehicleId}, Plaka={plaka}, Þoför={sofor}");
        }

        // Datagrid seçimini güncelle  
        public void GuncelleSeferSecimi(Sefer sefer)
        {
            SecilenSefer = sefer;
            Debug.WriteLine($"[SecimTakibi] Sefer seçimi güncellendi: SeferId={sefer.SeferId}, KonteynerNo={sefer.KonteynerNo}, Tarih={sefer.Tarih}");
        }

        // Menü seçimini güncelle  
        public void GuncelleMenuSecimi(string menuAdi)
        {
            AktifMenu = menuAdi;
            Debug.WriteLine($"[SecimTakibi] Menü seçimi güncellendi: AktifMenu={menuAdi}");
        }

        // Geri dönme iþlemi  
        public void GeriDon()
        {
            AktifMenu = null;
            Debug.WriteLine("[SecimTakibi] Geri dönme iþlemi gerçekleþtirildi. AktifMenu sýfýrlandý.");
        }

        // Menü seçimini sýfýrla  
        public void MenuSeciminiSifirla()
        {
            AktifMenu = null;
            Debug.WriteLine("[SecimTakibi] Menü seçimi sýfýrlandý.");
        }

        public static void SagPaneliKapat()
        {
            // Sað paneli kapatma iþlemleri  
            Debug.WriteLine("[SecimTakibi] Sað panel kapatma iþlemi gerçekleþtirildi.");
        }
    }
}