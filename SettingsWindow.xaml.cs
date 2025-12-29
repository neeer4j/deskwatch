using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskWatch
{
    public partial class SettingsWindow : Window
    {
        private bool _autoStartEnabled;
        private bool _minimizeToTrayEnabled;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _autoStartEnabled = SettingsManager.Settings.AutoStartEnabled;
            _minimizeToTrayEnabled = SettingsManager.Settings.MinimizeToTray;
            UpdateAutoStartToggleVisual(_autoStartEnabled, false);
            UpdateTrayToggleVisual(_minimizeToTrayEnabled, false);
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void AutoStartToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _autoStartEnabled = !_autoStartEnabled;
            SettingsManager.SetAutoStart(_autoStartEnabled);
            UpdateAutoStartToggleVisual(_autoStartEnabled, true);
        }

        private void MinimizeToTrayToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _minimizeToTrayEnabled = !_minimizeToTrayEnabled;
            SettingsManager.Settings.MinimizeToTray = _minimizeToTrayEnabled;
            SettingsManager.Save();
            UpdateTrayToggleVisual(_minimizeToTrayEnabled, true);
        }

        private void UpdateAutoStartToggleVisual(bool isOn, bool animate)
        {
            var targetX = isOn ? 22.0 : 0.0;
            var targetColor = isOn ? 
                (Brush)FindResource("AccentGradient") : 
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A4A"));

            AutoStartToggle.Background = targetColor;

            if (animate)
            {
                var animation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = System.TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ThumbTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            else
            {
                ThumbTranslate.X = targetX;
            }
        }

        private void UpdateTrayToggleVisual(bool isOn, bool animate)
        {
            var targetX = isOn ? 22.0 : 0.0;
            var targetColor = isOn ? 
                (Brush)FindResource("AccentGradient") : 
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A4A"));

            MinimizeToTrayToggle.Background = targetColor;

            if (animate)
            {
                var animation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = System.TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                TrayThumbTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            else
            {
                TrayThumbTranslate.X = targetX;
            }
        }

        private void ClearData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all data? This will remove all tracked apps and reset all statistics. This action cannot be undone.",
                "Clear All Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DataManager.Clear();
                MessageBox.Show("All data has been cleared. Please restart DeskWatch for changes to take effect.", 
                    "Data Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
