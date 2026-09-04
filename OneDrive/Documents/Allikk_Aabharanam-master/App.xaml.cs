using System;
using System.Windows;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Application entry point for the Desktop Keychain Accessory application.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
