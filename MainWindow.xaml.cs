using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
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

        private DateTime _lastTickUtc;
        private string? _lastKey;

        public ObservableCollection<AppUsage> AppUsages { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) return;

            _lastKey = GetCurrentAppKey(out _);
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_timer.IsEnabled) return;

            var now = DateTime.UtcNow;
            if (_lastKey is not null)
            {
                var delta = now - _lastTickUtc;
                AddTime(_lastKey, delta, null);
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
            _usageMap.Clear();
            AppUsages.Clear();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var currentKey = GetCurrentAppKey(out var displayName);

            // Attribute elapsed time since last tick to the previously active app
            if (_lastKey is not null)
            {
                var delta = now - _lastTickUtc;
                if (delta > TimeSpan.Zero)
                {
                    AddTime(_lastKey, delta, displayName);
                }
            }

            _lastKey = currentKey;
            _lastTickUtc = now;
        }

        private void AddTime(string key, TimeSpan delta, string? displayName)
        {
            if (!_usageMap.TryGetValue(key, out var usage))
            {
                usage = new AppUsage(key, displayName ?? key);
                _usageMap[key] = usage;
                AppUsages.Add(usage);
            }
            usage.Add(delta);
        }

        private string? GetCurrentAppKey(out string? displayName)
        {
            displayName = null;
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
                        displayName = proc.MainModule?.FileVersionInfo.ProductName;
                    }
                    catch
                    {
                        // Access denied for some system processes — ignore
                    }
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

        protected override void OnClosed(EventArgs e)
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }
            base.OnClosed(e);
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