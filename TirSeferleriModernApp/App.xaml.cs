using System.Configuration;
using System.Data;
using System.Windows;
using TirSeferleriModernApp.Services;

namespace TirSeferleriModernApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Veritabanı ve tablolar uygulama açılışında kontrol edilir/oluşturulur
            DatabaseService.CheckAndCreateOrUpdateSeferlerTablosu();
        }
    }
}

