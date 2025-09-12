using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TirSeferleriModernApp.Models;
using TirSeferleriModernApp.Services;
using TirSeferleriModernApp.ViewModels;
using System.Threading.Tasks;

// bu dosya XAML'in arkasındaki code-behind dosyasıdır. 
// DataGrid'e sağ tıklanınca açılan sütun görünürlüğü menüsünü (ContextMenu) burada tanımlanır.
// Görsel olaylara özel mantık burada yazılır.

namespace TirSeferleriModernApp.Views
{
    public partial class SeferlerView : UserControl
    {
        public SeferlerView()
        {
            InitializeComponent();
        }

        // ContextMenuOpening olayını işleyen metot
        private void dgSeferler_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // DataGrid'in ContextMenu'sünü oluştur
            ContextMenu contextMenu = new();

            // DataGrid'deki sütunları döngüyle gez
            foreach (var column in dgSeferler.Columns)
            {
                // Her sütun için bir MenuItem oluştur
                MenuItem menuItem = new()
                {
                    Header = column.Header?.ToString(),
                    IsCheckable = true, // İşaretlenebilir hale getir
                    IsChecked = column.Visibility == Visibility.Visible // Sütunun görünürlüğüne göre işaret durumu
                };

                // MenuItem'ın tıklama olayını bağla
                menuItem.Checked += (s, args) => column.Visibility = Visibility.Visible; // İşaretlendiğinde sütunu göster
                menuItem.Unchecked += (s, args) => column.Visibility = Visibility.Collapsed; // İşareti kaldırıldığında sütunu gizle

                // MenuItem'ı ContextMenu'ye ekle
                contextMenu.Items.Add(menuItem);
            }

            // DataGrid'e ContextMenu'yu ata
            dgSeferler.ContextMenu = contextMenu;
        }

        // Enter ile hücreyi commit et ve DB'ye yaz
        private void dgSeferler_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (dgSeferler.CommitEdit(DataGridEditingUnit.Cell, true))
                    dgSeferler.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;
            }
        }

        private async void dgSeferler_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row?.Item is Sefer sefer && sefer.SeferId > 0)
            {
                try
                {
                    // 1) Yerel: Seferler güncelle
                    DatabaseService.SeferGuncelle(sefer);

                    // 2) Records satırını güncelle/is_dirty=1 yap (senkron tetiklemek için)
                    var (remoteId, createdAt) = DatabaseService.TryGetRecordMeta(sefer.SeferId);
                    var rec = new Record
                    {
                        id = sefer.SeferId,
                        remote_id = remoteId,
                        deleted = false,
                        containerNo = sefer.KonteynerNo,
                        loadLocation = sefer.YuklemeYeri,
                        unloadLocation = sefer.BosaltmaYeri,
                        size = sefer.KonteynerBoyutu,
                        status = sefer.BosDolu,
                        nightOrDay = null,
                        truckPlate = sefer.CekiciPlaka,
                        notes = sefer.Aciklama,
                        createdByUserId = null,
                        createdAt = createdAt > 0 ? createdAt : System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await DatabaseService.RecordKaydetAsync(rec);

                    // 3) Durumu bildir
                    SyncStatusHub.Set("Senkron: Bekliyor");

                    // 4) Online ise buluta hemen yazmayı dene (başarısızsa SyncAgent zaten deneyecek)
                    try
                    {
                        var fs = new FirestoreServisi();
                        _ = await fs.BulutaYazOrGuncelle(rec);
                    }
                    catch { /* background agent will retry */ }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Güncelleme hatası: {ex.Message}");
                }
            }

            // Yükleme/Boşaltma/Ekstra/BoşDolu alanı değiştiyse fiyatı yeniden hesapla ve kaydet
            if (e.Row?.Item is Sefer s && (e.Column.Header?.ToString() == "Yükleme" || e.Column.Header?.ToString() == "Boşaltma" || e.Column.Header?.ToString() == "Emanet/Soda" || e.Column.Header?.ToString() == "Boş/Dolu"))
            {
                var u = DatabaseService.GetUcretForRoute(s.YuklemeYeri, s.BosaltmaYeri, null, s.BosDolu);
                if (u.HasValue)
                {
                    s.Fiyat = u.Value;
                    try { DatabaseService.SeferGuncelle(s); } catch { }
                }
            }

            // 5) Listeyi ve sayaçları tazele (FS/DB rozetleri dahil)
            if (DataContext is SeferlerViewModel vm)
            {
                if (!string.IsNullOrWhiteSpace(vm.SeciliCekiciPlaka))
                    vm.LoadSeferler(vm.SeciliCekiciPlaka);
                else
                    vm.LoadSeferler();
            }
        }
    }
}
