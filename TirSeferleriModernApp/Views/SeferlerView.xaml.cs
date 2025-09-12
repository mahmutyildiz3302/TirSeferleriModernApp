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
        private bool _isHandlingEdit; // reentrancy guard

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

        // Enter ile hücreyi commit et ve ortak kaydetme akışını tetikle
        private async void dgSeferler_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (dgSeferler.CommitEdit(DataGridEditingUnit.Cell, true))
                    dgSeferler.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;

                if (DataContext is SeferlerViewModel vm && dgSeferler.SelectedItem is Sefer s)
                {
                    await vm.SaveSeferAsync(s);
                }
            }
        }

        private async void dgSeferler_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_isHandlingEdit) return; // reentrancy guard
            if (e.EditAction != DataGridEditAction.Commit) return;

            if (DataContext is SeferlerViewModel vm && e.Row?.Item is Sefer sefer && sefer.SeferId > 0)
            {
                try
                {
                    _isHandlingEdit = true;
                    await vm.SaveSeferAsync(sefer);
                }
                finally
                {
                    _isHandlingEdit = false;
                }
            }

            // Yükleme/Boşaltma/Ekstra/BoşDolu alanı değiştiyse fiyatı yeniden hesapla ve kaydet (SaveSeferAsync zaten çağrıldı)
            // Eski lokal DB yazımları kaldırıldı; tek kaynak: VM.SaveSeferAsync
        }
    }
}
