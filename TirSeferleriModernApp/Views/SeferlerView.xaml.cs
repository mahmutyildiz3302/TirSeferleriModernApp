using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;

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
    }
}
