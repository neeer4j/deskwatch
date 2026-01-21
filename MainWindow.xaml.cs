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
using System.Windows.Media.Animation;
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
        
        // Cached brushes to avoid repeated allocations
        private static readonly SolidColorBrush IdleBrush = new(Color.FromRgb(0xF5, 0x9E, 0x0B));
        private static readonly SolidColorBrush PausedBrush = new(Color.FromRgb(0x71, 0x71, 0x7A));
        
        // Optimization: Track running apps less frequently
        private int _processCheckCounter = 0;
        private const int ProcessCheckInterval = 5; // Check every 5 seconds instead of every second
        private HashSet<string>? _cachedRunningProcessNames;

        public ObservableCollection<AppUsage> AppUsages { get; } = new();
        
        static MainWindow()
        {
            // Freeze brushes for better performance
            IdleBrush.Freeze();
            PausedBrush.Freeze();
        }

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

            // Auto-save every 60 seconds (reduced from 30s for better performance)
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
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
            // Also save today's data to history on exit
            DataManager.UpdateTodayInHistory(AppUsages);
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        private void LoadSavedData()
        {
            var savedData = DataManager.Load();
            var today = DateTime.Today;
            
            // Check if we need to roll over to a new day
            var needsDayRollover = savedData.CurrentTrackingDate.Date != today;
            
            if (needsDayRollover && savedData.TrackedApps.Count > 0)
            {
                // Archive previous day's data before loading
                var tempApps = new List<AppUsage>();
                foreach (var appData in savedData.TrackedApps)
                {
                    var tempApp = new AppUsage(appData.Key, appData.DisplayName);
                    tempApp.SetTodayTime(TimeSpan.FromSeconds(appData.TodaySeconds));
                    tempApps.Add(tempApp);
                }
                DataManager.ArchiveDayToHistory(savedData.CurrentTrackingDate.Date, tempApps);
            }
            
            foreach (var appData in savedData.TrackedApps)
            {
                var app = new AppUsage(appData.Key, appData.DisplayName)
                {
                    ExePath = appData.ExePath,
                    FocusCount = appData.FocusCount
                };

                // Restore total time
                app.AddToTotal(TimeSpan.FromSeconds(appData.TotalSeconds));
                
                // Restore today's time (or reset if new day)
                if (!needsDayRollover)
                {
                    app.SetTodayTime(TimeSpan.FromSeconds(appData.TodaySeconds));
                    app.TodayFocusCount = appData.TodayFocusCount;
                }
                // If new day, TodayTime stays at zero (default)

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

        private DateTime _lastSaveDate = DateTime.Today;
        private int _historyUpdateCounter = 0;

        private void SaveData()
        {
            DataManager.Save(AppUsages);
            
            // Update history every 10 saves (~10 minutes) to reduce disk writes
            _historyUpdateCounter++;
            if (_historyUpdateCounter >= 10)
            {
                DataManager.UpdateTodayInHistory(AppUsages);
                _historyUpdateCounter = 0;
            }
        }
        
        private void CheckDayRollover()
        {
            var today = DateTime.Today;
            if (_lastSaveDate.Date != today)
            {
                // Day has changed - archive previous day and reset today's counters
                DataManager.ArchiveDayToHistory(_lastSaveDate, AppUsages);
                
                foreach (var app in AppUsages)
                {
                    app.OnDayRollover();
                }
                
                _lastSaveDate = today;
                SaveData();
            }
        }

        private bool IsAppRunning(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                var isRunning = processes.Length > 0;
                // Dispose all returned processes
                foreach (var proc in processes)
                {
                    proc.Dispose();
                }
                return isRunning;
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

        #region Navigation
        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboardView();
        }

        private void ScreenTimeButton_Click(object sender, RoutedEventArgs e)
        {
            ShowScreenTimeView();
        }

        private void ShowDashboardView()
        {
            DashboardView.Visibility = Visibility.Visible;
            ScreenTimeView.Visibility = Visibility.Collapsed;
            
            // Update sidebar button states
            DashboardButton.Style = (Style)FindResource("SidebarButtonAccent");
            ScreenTimeButton.Style = (Style)FindResource("SidebarButton");
        }

        private void ShowScreenTimeView()
        {
            DashboardView.Visibility = Visibility.Collapsed;
            ScreenTimeView.Visibility = Visibility.Visible;
            
            // Update sidebar button states
            DashboardButton.Style = (Style)FindResource("SidebarButton");
            ScreenTimeButton.Style = (Style)FindResource("SidebarButtonAccent");
            
            // Refresh screen time data
            ScreenTimeView.RefreshData(AppUsages);
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
                StatusIndicator.Fill = PausedBrush;
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
            // Toggle inline settings panel
            if (InlineSettingsPanel.Visibility == Visibility.Visible)
            {
                // Close settings - restore previous state
                InlineSettingsPanel.Visibility = Visibility.Collapsed;
                if (_selectedApp != null)
                {
                    DetailsPanel.Visibility = Visibility.Visible;
                    NoSelectionPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    DetailsPanel.Visibility = Visibility.Collapsed;
                    NoSelectionPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Show settings - hide everything else
                InlineSettingsPanel.Visibility = Visibility.Visible;
                DetailsPanel.Visibility = Visibility.Collapsed;
                NoSelectionPanel.Visibility = Visibility.Collapsed;
                LoadInlineSettingsState();
            }
        }

        private void LoadInlineSettingsState()
        {
            var accent = (Brush)FindResource("AccentGradient");
            var off = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"));

            // Auto Start
            var autoStart = SettingsManager.Settings.AutoStartEnabled;
            InlineAutoStartToggle.Background = autoStart ? accent : off;
            InlineAutoStartTranslate.X = autoStart ? 20 : 0;

            // Minimize to Tray
            var tray = SettingsManager.Settings.MinimizeToTray;
            InlineTrayToggle.Background = tray ? accent : off;
            InlineTrayTranslate.X = tray ? 20 : 0;

            // Idle Detection
            var idle = SettingsManager.Settings.IdleDetectionEnabled;
            InlineIdleToggle.Background = idle ? accent : off;
            InlineIdleTranslate.X = idle ? 20 : 0;
        }

        private void InlineAutoStartToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var newState = !SettingsManager.Settings.AutoStartEnabled;
            SettingsManager.SetAutoStart(newState);
            AnimateToggle(InlineAutoStartToggle, InlineAutoStartTranslate, newState);
        }

        private void InlineTrayToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var newState = !SettingsManager.Settings.MinimizeToTray;
            SettingsManager.Settings.MinimizeToTray = newState;
            SettingsManager.Save();
            AnimateToggle(InlineTrayToggle, InlineTrayTranslate, newState);
        }

        private void InlineIdleToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var newState = !SettingsManager.Settings.IdleDetectionEnabled;
            SettingsManager.Settings.IdleDetectionEnabled = newState;
            SettingsManager.Save();
            AnimateToggle(InlineIdleToggle, InlineIdleTranslate, newState);
        }

        private void AnimateToggle(Border toggle, TranslateTransform transform, bool isOn)
        {
            var accent = (Brush)FindResource("AccentGradient");
            var off = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#404040"));
            toggle.Background = isOn ? accent : off;

            var animation = new DoubleAnimation
            {
                To = isOn ? 20 : 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void InlineExportData_Click(object sender, RoutedEventArgs e)
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
                    MessageBox.Show($"Data exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void InlineImportData_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import DeskWatch Data",
                Filter = "JSON files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var result = MessageBox.Show("This will replace all current data. Continue?", "Import Data",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(dialog.FileName);
                        var data = System.Text.Json.JsonSerializer.Deserialize<SavedData>(json);
                        if (data?.TrackedApps != null)
                        {
                            var existingData = DataManager.Load();
                            existingData.TrackedApps = data.TrackedApps;
                            existingData.LastSaved = DateTime.UtcNow;
                            var newJson = System.Text.Json.JsonSerializer.Serialize(existingData);
                            System.IO.File.WriteAllText(System.IO.Path.Combine(SettingsManager.GetAppDataFolder(), "usage_data.json"), newJson);
                            MessageBox.Show("Data imported! Please restart DeskWatch.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void InlineClearData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear ALL data including history? This cannot be undone.",
                "Clear All Data", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DataManager.Clear();
                DataManager.ClearHistory();
                AppUsages.Clear();
                _usageMap.Clear();
                _selectedApp = null;
                DetailsPanel.Visibility = Visibility.Hidden;
                InlineSettingsPanel.Visibility = Visibility.Collapsed;
                NoSelectionPanel.Visibility = Visibility.Visible;
                UpdateEmptyState();
                UpdateTodayTime();
                MessageBox.Show("All data has been cleared.", "Data Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
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
            
            // Check for day rollover at midnight
            CheckDayRollover();
            
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
                    StatusIndicator.Fill = IdleBrush;
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
            // Optimization: Only check every N seconds to reduce CPU usage
            var currentlyRunning = new HashSet<string>();
            _processCheckCounter++;
            
            if (_processCheckCounter >= ProcessCheckInterval)
            {
                _processCheckCounter = 0;
                try
                {
                    // Get all processes and properly dispose them
                    var processes = Process.GetProcesses();
                    var runningNames = new HashSet<string>();
                    
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (proc.MainWindowHandle != IntPtr.Zero)
                            {
                                runningNames.Add(proc.ProcessName);
                            }
                        }
                        finally
                        {
                            proc.Dispose(); // Important: Dispose each process
                        }
                    }
                    
                    _cachedRunningProcessNames = runningNames;
                }
                catch { }
            }
            
            // Use cached process names
            if (_cachedRunningProcessNames != null)
            {
                foreach (var app in AppUsages)
                {
                    if (_cachedRunningProcessNames.Contains(app.Key))
                    {
                        currentlyRunning.Add(app.Key);

                        if (!_runningAppsLastTick.Contains(app.Key))
                        {
                            app.IncrementFocusCount();
                        }
                    }
                }
            }

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
                DetailsTodayTime.Text = _selectedApp.FormattedTodayTime;
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
            DataManager.UpdateTodayInHistory(AppUsages);
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

                // Hide settings panel if open, show details
                InlineSettingsPanel.Visibility = Visibility.Collapsed;
                DetailsPanel.Visibility = Visibility.Visible;
                NoSelectionPanel.Visibility = Visibility.Collapsed;

                DetailsIcon.Source = app.Icon;
                DetailsName.Text = app.DisplayName;
                DetailsTime.Text = app.FormattedTotal;
                DetailsTodayTime.Text = app.FormattedTodayTime;
                
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

        private void SortByTime_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _sortMode = "time_desc";
            ApplySort();
            UpdateSortButtonVisuals();
        }

        private void SortByName_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _sortMode = "name_asc";
            ApplySort();
            UpdateSortButtonVisuals();
        }

        private void SortBySessions_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _sortMode = "sessions_desc";
            ApplySort();
            UpdateSortButtonVisuals();
        }

        private void UpdateSortButtonVisuals()
        {
            var accent = (Brush)FindResource("AccentGradient");
            var transparent = Brushes.Transparent;
            var white = Brushes.White;
            var muted = (Brush)FindResource("TextMutedBrush");

            SortByTimeBtn.Background = _sortMode == "time_desc" ? accent : transparent;
            SortByNameBtn.Background = _sortMode == "name_asc" ? accent : transparent;
            SortBySessionsBtn.Background = _sortMode == "sessions_desc" ? accent : transparent;

            // Update icon colors
            if (SortByTimeBtn.Child is TextBlock t1) t1.Foreground = _sortMode == "time_desc" ? white : muted;
            if (SortByNameBtn.Child is TextBlock t2) t2.Foreground = _sortMode == "name_asc" ? white : muted;
            if (SortBySessionsBtn.Child is TextBlock t3) t3.Foreground = _sortMode == "sessions_desc" ? white : muted;
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
                totalToday += app.TodayTime;
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