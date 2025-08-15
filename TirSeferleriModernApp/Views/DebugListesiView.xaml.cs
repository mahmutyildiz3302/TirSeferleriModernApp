using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp.Views
{
    /// <summary>
    /// DebugListesiView.xaml etkileşim mantığı
    /// </summary>
    public partial class DebugListesiView : UserControl
    {
        public DebugListesiView()
        {
            InitializeComponent();
            lstLogs.ItemsSource = LogService.Entries; // Gerçek debug verilerini bağlama

            // KeyDown olayını dinle
            this.KeyDown += DebugListesiView_KeyDown;
        }

        private void DebugListesiView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                // Debug ekranındaki verileri temizle
                LogService.Entries.Clear();
                Trace.WriteLine("[DebugListesiView.xaml.cs] Debug listesi temizlendi.");
            }
            else if (e.Key == Key.Escape)
            {
                // Debug ekranını kapat (gömülü view olduğundan pencereyi kapatmak yerine sadece bilgi loglayalım)
                Trace.WriteLine("[DebugListesiView.xaml.cs] Escape tuşuna basıldı (gömülü görünüm).");
            }
        }
    }
}
