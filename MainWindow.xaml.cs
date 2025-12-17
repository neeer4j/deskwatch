using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskWatch.Models;

namespace DeskWatch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Dictionary<string, AppUsage> _usageMap = new();
        private readonly Dictionary<string, ImageSource?> _iconCache = new();

        private DateTime _lastTickUtc;
        private string? _lastKey;

        private AppUsage? _selectedApp;

        public ObservableCollection<AppUsage> AppUsages { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            // No longer populating all apps automatically. User must add them.
        }

        private void AddAppButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddAppWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
            {
                var p = dialog.SelectedProcess;
                if (!_usageMap.ContainsKey(p.ProcessName))
                {
                    var app = new AppUsage(p.ProcessName, p.MainWindowTitle);
                    app.Icon = p.Icon;
                    _usageMap[p.ProcessName] = app;
                    AppUsages.Add(app);
                }
            }
        }

        private void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp != null)
            {
                _usageMap.Remove(_selectedApp.Key);
                AppUsages.Remove(_selectedApp);
                _selectedApp = null;
                DetailsPanel.Visibility = Visibility.Hidden;
                NoSelectionText.Visibility = Visibility.Visible;
                if (NoSelectionSubText != null) NoSelectionSubText.Visibility = Visibility.Visible;
            }
        }


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) return;

            _lastKey = GetCurrentAppKey(out var displayName, out var exePath);
            
            // Only track if it's already in the whitelist
            // No auto-add here anymore.
            if (_lastKey != null && !_usageMap.ContainsKey(_lastKey))
            {
               // Do nothing. It's not a tracked app.
            }
            
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_timer.IsEnabled) return;

            var now = DateTime.UtcNow;
            if (_lastKey is not null && _usageMap.TryGetValue(_lastKey, out var lastUsage))
            {
                lastUsage.Add(now - _lastTickUtc);
            }
            _timer.Stop();
            _lastKey = null;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled)
            {
                StopButton_Click(sender, e);
            }
            
            foreach (var app in AppUsages)
            {
                app.Reset();
            }
            // Do not clear the list or cache
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var currentKey = GetCurrentAppKey(out var currentDisplayName, out var currentExePath);

            // Attribute elapsed time since last tick to the previously active app
            if (_lastKey is not null)
            {
                var delta = now - _lastTickUtc;
                if (delta > TimeSpan.Zero)
                {
                    // Update time for the PREVIOUS app (the one that was active)
                    if (_usageMap.TryGetValue(_lastKey, out var lastUsage))
                    {
                        lastUsage.Add(delta);
                    }
                }
            }

            // If we switched to a new app, check if it is in our whitelist
            if (currentKey != null)
            {
                if (_usageMap.TryGetValue(currentKey, out var usage))
                {
                    // Increment focus count if we just switched to this app
                    if (currentKey != _lastKey)
                    {
                        usage.IncrementFocusCount();
                    }
                }
            }

            _lastKey = currentKey;
            _lastTickUtc = now;

            // Refresh Details Panel if an app is selected
            if (_selectedApp != null && DetailsPanel.Visibility == Visibility.Visible)
            {
                DetailsTime.Text = _selectedApp.FormattedTotal;
                 if (FindName("DetailsCount") is TextBlock countBlock)
                {
                    countBlock.Text = _selectedApp.FocusCount.ToString();
                }
            }
        }

        private void AddTime(string key, TimeSpan delta, string? displayName, string? exePath)
        {
            if (!_usageMap.TryGetValue(key, out var usage))
            {
                usage = new AppUsage(key, displayName ?? key);
                if (!string.IsNullOrEmpty(exePath))
                {
                    usage.Icon = GetAppIcon(exePath);
                }
                _usageMap[key] = usage;
                AppUsages.Add(usage);
            }
            usage.Add(delta);
        }

        private string? GetCurrentAppKey(out string? displayName, out string? exePath)
        {
            displayName = null;
            exePath = null;
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                uint pid;
                _ = GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return null;

                using var proc = Process.GetProcessById((int)pid);
                var key = proc.ProcessName;
                
                // Try to get MainModule to find exe path, but handle Access Denied
                try 
                { 
                    exePath = proc.MainModule?.FileName; 
                } 
                catch 
                { 
                    // Access denied or process exited - ignoring exe path
                    exePath = null;
                }

                // Prefer window title, fall back to product name, then process name
                var title = GetWindowTitle(hwnd);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    displayName = title;
                }
                else
                {
                    try
                    {
                        // Use MainModule only if we have access
                        if (exePath != null)
                        {
                            displayName = proc.MainModule?.FileVersionInfo.ProductName;
                        }
                    }
                    catch { }
                    displayName ??= key;
                }
                return key;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private ImageSource? GetAppIcon(string exePath)
        {
            if (_iconCache.TryGetValue(exePath, out var cached))
                return cached;
            try
            {
                if (System.IO.File.Exists(exePath))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        using var bmp = icon.ToBitmap();
                        var ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.StreamSource = ms;
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.EndInit();
                        img.Freeze();
                        _iconCache[exePath] = img;
                        return img;
                    }
                }
            }
            catch { }
            _iconCache[exePath] = null;
            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_timer?.IsEnabled == true)
            {
                _timer.Stop();
            }
            base.OnClosed(e);
        }

        private void AppCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is AppUsage app)
            {
                // Deselect all
                foreach (var item in AppUsages)
                    item.GetType().GetProperty("IsSelected")?.SetValue(item, false);
                // Select this
                app.GetType().GetProperty("IsSelected")?.SetValue(app, true);
                _selectedApp = app;
                
                // Update details panel
                DetailsPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
                // Check if the new subtext exists (it might not if XAML didn't have it, but our new XAML does)
                var subText = NoSelectionSubText; 
                if (subText != null) subText.Visibility = Visibility.Collapsed;

                DetailsIcon.Source = app.Icon;
                DetailsName.Text = app.DisplayName;
                // Just the time string, label is in XAML
                DetailsTime.Text = app.FormattedTotal;
                // Bind count if element exists (it does in new XAML)
                if (FindName("DetailsCount") is TextBlock countBlock)
                {
                    countBlock.Text = app.FocusCount.ToString();
                }
            }
        }

        #region Win32
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        #endregion
    }
}