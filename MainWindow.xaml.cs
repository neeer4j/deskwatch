using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskWatch.Models;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DeskWatch
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _saveTimer;
        private readonly Dictionary<string, AppUsage> _usageMap = new();
        private readonly Dictionary<string, ImageSource?> _iconCache = new();
        private readonly HashSet<string> _runningAppsLastTick = new();

        private DateTime _lastTickUtc;
        private string? _lastKey;

        private AppUsage? _selectedApp;
        
        // System Tray
        private Forms.NotifyIcon? _notifyIcon;
        private bool _isExiting = false;
        
        // Idle detection
        private bool _isIdle = false;
        
        // Search and Sort
        private string _searchText = "";
        private string _sortMode = "time_desc";
        private ICollectionView? _filteredView;

        public ObservableCollection<AppUsage> AppUsages { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Load saved data
            LoadSavedData();
            
            // Setup filtering/sorting
            _filteredView = CollectionViewSource.GetDefaultView(AppUsages);
            _filteredView.Filter = AppFilter;
            ApplySort();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            // Auto-save every 30 seconds
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _saveTimer.Tick += (s, e) => SaveData();
            _saveTimer.Start();

            // Initialize running apps set
            foreach (var app in AppUsages)
            {
                if (IsAppRunning(app.Key))
                {
                    _runningAppsLastTick.Add(app.Key);
                }
            }

            // Initialize system tray
            InitializeNotifyIcon();

            // Auto-start tracking
            _lastKey = GetCurrentAppKey(out _, out _);
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            UpdateStatusIndicator(true);
            UpdateEmptyState();
            UpdateTodayTime();
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            
            // Load icon from assets
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "deskwatch.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Drawing.Icon(iconPath);
            }
            else
            {
                // Use a default system icon if our icon isn't found
                _notifyIcon.Icon = Drawing.SystemIcons.Application;
            }
            
            _notifyIcon.Text = "DeskWatch - Tracking Active";
            _notifyIcon.Visible = true;
            
            // Double-click to show window
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
            
            // Context menu
            var contextMenu = new Forms.ContextMenuStrip();
            
            var showItem = new Forms.ToolStripMenuItem("Show DeskWatch");
            showItem.Click += (s, e) => ShowWindow();
            showItem.Font = new Drawing.Font(showItem.Font, Drawing.FontStyle.Bold);
            contextMenu.Items.Add(showItem);
            
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            
            var trackingItem = new Forms.ToolStripMenuItem("Stop Tracking");
            trackingItem.Click += (s, e) => ToggleTracking(trackingItem);
            contextMenu.Items.Add(trackingItem);
            
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            
            var exitItem = new Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ToggleTracking(Forms.ToolStripMenuItem menuItem)
        {
            if (_timer.IsEnabled)
            {
                StopButton_Click(this, new RoutedEventArgs());
                menuItem.Text = "Start Tracking";
                _notifyIcon!.Text = "DeskWatch - Paused";
            }
            else
            {
                StartButton_Click(this, new RoutedEventArgs());
                menuItem.Text = "Stop Tracking";
                _notifyIcon!.Text = "DeskWatch - Tracking Active";
            }
        }

        private void ExitApplication()
        {
            _isExiting = true;
            SaveData();
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void LoadSavedData()
        {
            var savedData = DataManager.Load();
            foreach (var appData in savedData.TrackedApps)
            {
                var app = new AppUsage(appData.Key, appData.DisplayName)
                {
                    ExePath = appData.ExePath,
                    FocusCount = appData.FocusCount
                };

                // Restore time
                app.Add(TimeSpan.FromSeconds(appData.TotalSeconds));

                // Try to load icon
                if (!string.IsNullOrEmpty(appData.ExePath) && File.Exists(appData.ExePath))
                {
                    app.Icon = IconHelper.GetAppIcon(appData.ExePath);
                    if (app.Icon != null)
                    {
                        _iconCache[appData.ExePath] = app.Icon;
                    }
                }

                _usageMap[app.Key] = app;
                AppUsages.Add(app);
            }
        }

        private void SaveData()
        {
            DataManager.Save(AppUsages);
        }

        private bool IsAppRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = AppUsages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Window Chrome Handlers
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeButton.Content = "\uE922";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeButton.Content = "\uE923";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsManager.Settings.MinimizeToTray && !_isExiting)
            {
                // Minimize to tray instead of closing
                this.Hide();
            }
            else
            {
                ExitApplication();
            }
        }
        #endregion

        private void UpdateStatusIndicator(bool isTracking)
        {
            if (isTracking)
            {
                StatusIndicator.Fill = (Brush)FindResource("SuccessBrush");
                StatusText.Text = "Tracking Active";
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717A"));
                StatusText.Text = "Paused";
            }
        }

        private void AddAppButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddAppWindow { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
            {
                var p = dialog.SelectedProcess;
                if (!_usageMap.ContainsKey(p.ProcessName))
                {
                    var app = new AppUsage(p.ProcessName, p.MainWindowTitle)
                    {
                        ExePath = p.ExePath,
                        Icon = p.Icon
                    };
                    _usageMap[p.ProcessName] = app;
                    AppUsages.Add(app);
                    UpdateEmptyState();
                    SaveData();
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            settingsWindow.ShowDialog();
        }

        private void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp != null)
            {
                _usageMap.Remove(_selectedApp.Key);
                AppUsages.Remove(_selectedApp);
                _selectedApp = null;
                DetailsPanel.Visibility = Visibility.Hidden;
                NoSelectionPanel.Visibility = Visibility.Visible;
                UpdateEmptyState();
                SaveData();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) return;

            _lastKey = GetCurrentAppKey(out _, out _);
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            UpdateStatusIndicator(true);
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
            UpdateStatusIndicator(false);
            SaveData();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all tracking data? This cannot be undone.",
                "Reset All Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (_timer.IsEnabled)
                {
                    StopButton_Click(sender, e);
                }

                foreach (var app in AppUsages)
                {
                    app.Reset();
                }
                SaveData();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            
            // Check for idle state if enabled
            if (SettingsManager.Settings.IdleDetectionEnabled)
            {
                var idleTimeout = TimeSpan.FromMinutes(SettingsManager.Settings.IdleTimeoutMinutes);
                var nowIdle = IdleDetector.IsIdle(idleTimeout);
                
                if (nowIdle && !_isIdle)
                {
                    // Just went idle - don't count this time
                    _isIdle = true;
                    _notifyIcon!.Text = "DeskWatch - Idle (Paused)";
                    StatusText.Text = "Idle - Paused";
                    StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")); // Amber
                }
                else if (!nowIdle && _isIdle)
                {
                    // Just returned from idle
                    _isIdle = false;
                    _lastTickUtc = now; // Reset timer to avoid counting idle time
                    _notifyIcon!.Text = "DeskWatch - Tracking Active";
                    StatusText.Text = "Tracking Active";
                    StatusIndicator.Fill = (Brush)FindResource("SuccessBrush");
                }
                
                if (_isIdle)
                {
                    _lastKey = null;
                    _lastTickUtc = now;
                    return; // Don't track while idle
                }
            }
            
            var currentKey = GetCurrentAppKey(out _, out _);

            // Attribute elapsed time since last tick to the previously active app
            if (_lastKey is not null)
            {
                var delta = now - _lastTickUtc;
                if (delta > TimeSpan.Zero)
                {
                    if (_usageMap.TryGetValue(_lastKey, out var lastUsage))
                    {
                        lastUsage.Add(delta);
                    }
                }
            }

            // Check which tracked apps are currently running to detect launches
            var currentlyRunning = new HashSet<string>();
            try
            {
                var runningProcessNames = Process.GetProcesses()
                    .Where(p => p.MainWindowHandle != IntPtr.Zero)
                    .Select(p => p.ProcessName)
                    .ToHashSet();

                foreach (var app in AppUsages)
                {
                    if (runningProcessNames.Contains(app.Key))
                    {
                        currentlyRunning.Add(app.Key);

                        if (!_runningAppsLastTick.Contains(app.Key))
                        {
                            app.IncrementFocusCount();
                        }
                    }
                }
            }
            catch { }

            _runningAppsLastTick.Clear();
            foreach (var key in currentlyRunning)
            {
                _runningAppsLastTick.Add(key);
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
            
            // Update today's total time display
            UpdateTodayTime();
        }

        private string? GetCurrentAppKey(out string? displayName, out string? exePath)
        {
            displayName = null;
            exePath = null;
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            try
            {
                uint pid;
                _ = GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return null;

                using var proc = Process.GetProcessById((int)pid);
                var key = proc.ProcessName;

                try
                {
                    exePath = proc.MainModule?.FileName;
                }
                catch
                {
                    exePath = null;
                }

                var title = GetWindowTitle(hwnd);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    displayName = title;
                }
                else
                {
                    try
                    {
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
            
            var icon = IconHelper.GetAppIcon(exePath);
            _iconCache[exePath] = icon;
            return icon;
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _saveTimer?.Stop();
            SaveData();
            _notifyIcon?.Dispose();
            base.OnClosed(e);
        }

        private void AppCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is AppUsage app)
            {
                foreach (var item in AppUsages)
                    item.IsSelected = false;
                
                app.IsSelected = true;
                _selectedApp = app;

                DetailsPanel.Visibility = Visibility.Visible;
                NoSelectionPanel.Visibility = Visibility.Collapsed;

                DetailsIcon.Source = app.Icon;
                DetailsName.Text = app.DisplayName;
                DetailsTime.Text = app.FormattedTotal;
                
                if (FindName("DetailsCount") is TextBlock countBlock)
                {
                    countBlock.Text = app.FocusCount.ToString();
                }
            }
        }

        #region Search, Sort, and Today's Time
        private bool AppFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            if (item is AppUsage app)
            {
                return app.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                       app.Key.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchBox.Text;
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(_searchText) ? Visibility.Visible : Visibility.Collapsed;
            _filteredView?.Refresh();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sortTag)
            {
                _sortMode = sortTag;
                ApplySort();
            }
        }

        private void ApplySort()
        {
            if (_filteredView == null) return;

            _filteredView.SortDescriptions.Clear();
            switch (_sortMode)
            {
                case "time_desc":
                    _filteredView.SortDescriptions.Add(new SortDescription("Total", ListSortDirection.Descending));
                    break;
                case "name_asc":
                    _filteredView.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
                    break;
                case "sessions_desc":
                    _filteredView.SortDescriptions.Add(new SortDescription("FocusCount", ListSortDirection.Descending));
                    break;
            }
        }

        private void UpdateTodayTime()
        {
            var totalToday = TimeSpan.Zero;
            foreach (var app in AppUsages)
            {
                totalToday += app.Total;
            }
            TodayTimeText.Text = string.Format("{0:00}:{1:00}:{2:00}", 
                (int)totalToday.TotalHours, totalToday.Minutes, totalToday.Seconds);
        }
        #endregion

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