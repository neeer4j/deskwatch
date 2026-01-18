using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskWatch
{
    public partial class SettingsWindow : Window
    {
        private bool _autoStartEnabled;
        private bool _minimizeToTrayEnabled;
        private bool _idleDetectionEnabled;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _autoStartEnabled = SettingsManager.Settings.AutoStartEnabled;
            _minimizeToTrayEnabled = SettingsManager.Settings.MinimizeToTray;
            _idleDetectionEnabled = SettingsManager.Settings.IdleDetectionEnabled;
            UpdateAutoStartToggleVisual(_autoStartEnabled, false);
            UpdateTrayToggleVisual(_minimizeToTrayEnabled, false);
            UpdateIdleToggleVisual(_idleDetectionEnabled, false);
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

        private void IdleDetectionToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _idleDetectionEnabled = !_idleDetectionEnabled;
            SettingsManager.Settings.IdleDetectionEnabled = _idleDetectionEnabled;
            SettingsManager.Save();
            UpdateIdleToggleVisual(_idleDetectionEnabled, true);
        }

        private void UpdateIdleToggleVisual(bool isOn, bool animate)
        {
            var targetX = isOn ? 22.0 : 0.0;
            var targetColor = isOn ? 
                (Brush)FindResource("AccentGradient") : 
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A4A"));

            IdleDetectionToggle.Background = targetColor;

            if (animate)
            {
                var animation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = System.TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                IdleThumbTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            else
            {
                IdleThumbTranslate.X = targetX;
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

        private void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export DeskWatch Data",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"DeskWatch_Export_{DateTime.Now:yyyy-MM-dd}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = DataManager.Load();
                    var json = System.Text.Json.JsonSerializer.Serialize(data, 
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dialog.FileName, json);
                    
                    MessageBox.Show($"Data exported successfully to:\n{dialog.FileName}", 
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export data:\n{ex.Message}", 
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import DeskWatch Data",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "This will replace all your current tracking data with the imported data. Continue?",
                    "Import Data",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(dialog.FileName);
                        var data = System.Text.Json.JsonSerializer.Deserialize<SavedData>(json);
                        
                        if (data?.TrackedApps != null)
                        {
                            // Save the imported data
                            var existingData = DataManager.Load();
                            existingData.TrackedApps = data.TrackedApps;
                            existingData.LastSaved = DateTime.UtcNow;
                            
                            var newJson = System.Text.Json.JsonSerializer.Serialize(existingData, 
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                            System.IO.File.WriteAllText(
                                System.IO.Path.Combine(SettingsManager.GetAppDataFolder(), "usage_data.json"), 
                                newJson);
                            
                            MessageBox.Show("Data imported successfully. Please restart DeskWatch for changes to take effect.", 
                                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("The selected file does not contain valid DeskWatch data.", 
                                "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import data:\n{ex.Message}", 
                            "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
