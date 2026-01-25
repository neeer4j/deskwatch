using System.Windows;

namespace DeskWatch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check if this is first run BEFORE Initialize() sets it to false
            bool isFirstRun = SettingsManager.Settings.FirstRun;

            // Initialize settings (this sets FirstRun to false)
            SettingsManager.Initialize();

            if (isFirstRun)
            {
                // Show welcome window for first-time users
                var welcome = new WelcomeWindow();
                welcome.Show();
            }
            else
            {
                // Show main window for returning users
                var main = new MainWindow();
                main.Show();
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled Exception:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }
    }
}
