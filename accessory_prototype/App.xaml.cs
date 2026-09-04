using System.Windows;

namespace AccessoryPrototype
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new PrototypeWindow().Show();
        }
    }
}
