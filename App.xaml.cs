using System;
using System.IO;
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
            // Log the crash to a file for debugging
            try
            {
                var logPath = Path.Combine(SettingsManager.GetAppDataFolder(), "crash.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch { /* Best effort logging */ }

            MessageBox.Show($"Unhandled Exception:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }
    }
}
