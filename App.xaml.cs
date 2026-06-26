using System;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace OBudsManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();

            if (e.Args.Contains("--minimized"))
            {
                mainWindow.Visibility = Visibility.Hidden;
                // MainWindow setup will put the app in the tray
            }
            else
            {
                mainWindow.Show();
            }
        }
    }
}
