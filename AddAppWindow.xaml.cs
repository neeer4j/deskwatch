using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DeskWatch
{
    public partial class AddAppWindow : Window
    {
        public ProcessItem? SelectedProcess { get; private set; }
        private ProcessItem? _browsedApp;
        private bool _isRunningAppsTab = true;

        // System processes to exclude
        private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "TextInputHost", "WindowsInputExperience", "SystemSettings", "ShellExperienceHost",
            "SearchHost", "StartMenuExperienceHost", "LockApp", "ApplicationFrameHost",
            "RuntimeBroker", "svchost", "csrss", "dwm", "explorer", "Taskmgr",
            "ctfmon", "SecurityHealthSystray", "NVIDIA Share", "NVDisplay.Container",
            "SearchUI", "Cortana", "GameBar", "GameBarFTServer", "XboxGameBarWidgets",
            "WmiPrvSE", "dllhost", "sihost", "fontdrvhost", "conhost", "winlogon",
            "services", "lsass", "wininit", "smss", "System", "Idle", "MsMpEng",
            "NisSrv", "SgrmBroker", "spoolsv", "audiodg", "SearchIndexer",
            "SecurityHealthService", "SettingSyncHost", "backgroundTaskHost",
            "Windows.WARP.JITService", "WUDFHost", "LsaIso", "Memory Compression",
            "Registry", "dasHost", "PhoneExperienceHost", "WidgetService", "Widgets"
        };

        public AddAppWindow()
        {
            InitializeComponent();
            LoadProcesses();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void RunningAppsTab_Click(object sender, RoutedEventArgs e)
        {
            _isRunningAppsTab = true;
            RunningAppsContent.Visibility = Visibility.Visible;
            BrowseContent.Visibility = Visibility.Collapsed;
            
            // Update tab styles
            RunningAppsTab.Style = (Style)FindResource("TabButton");
            BrowseTab.Style = (Style)FindResource("TabButtonInactive");
            
            AddButton.Content = "Add Selected";
        }

        private void BrowseTab_Click(object sender, RoutedEventArgs e)
        {
            _isRunningAppsTab = false;
            RunningAppsContent.Visibility = Visibility.Collapsed;
            BrowseContent.Visibility = Visibility.Visible;
            
            // Update tab styles
            RunningAppsTab.Style = (Style)FindResource("TabButtonInactive");
            BrowseTab.Style = (Style)FindResource("TabButton");
            
            AddButton.Content = _browsedApp != null ? "Add Application" : "Add Selected";
        }

        private void LoadProcesses()
        {
            var apps = new List<ProcessItem>();
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
            
            var processGroups = Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .Where(p => !ExcludedProcesses.Contains(p.ProcessName))
                .GroupBy(p => p.ProcessName);

            foreach (var group in processGroups)
            {
                try
                {
                    var p = group.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MainWindowTitle)) ?? group.First();
                    
                    string? exePath = null;
                    try
                    {
                        exePath = p.MainModule?.FileName;
                    }
                    catch { }

                    // Filter out system apps by path
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var lowerPath = exePath.ToLowerInvariant();
                        
                        if (lowerPath.StartsWith(windowsDir) || 
                            (lowerPath.Contains("\\windowsapps\\") && lowerPath.Contains("microsoft.")) ||
                            lowerPath.Contains("\\system32\\") ||
                            lowerPath.Contains("\\syswow64\\"))
                        {
                            continue;
                        }
                    }
                    
                    var item = new ProcessItem
                    {
                        ProcessName = p.ProcessName,
                        MainWindowTitle = p.MainWindowTitle,
                        ExePath = exePath
                    };
                    
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        item.Icon = GetAppIcon(exePath);
                    }

                    apps.Add(item);
                }
                catch { }
            }

            ProcessList.ItemsSource = apps.OrderBy(a => a.MainWindowTitle).ToList();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Application",
                Filter = "Executable files (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var exePath = dialog.FileName;
                var fileName = Path.GetFileNameWithoutExtension(exePath);
                
                // Try to get product name from file version info
                string displayName = fileName;
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                    {
                        displayName = versionInfo.ProductName;
                    }
                    else if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                    {
                        displayName = versionInfo.FileDescription;
                    }
                }
                catch { }

                _browsedApp = new ProcessItem
                {
                    ProcessName = fileName,
                    MainWindowTitle = displayName,
                    ExePath = exePath,
                    Icon = GetAppIcon(exePath)
                };

                // Update UI
                BrowsedAppIcon.Source = _browsedApp.Icon;
                BrowsedAppName.Text = displayName;
                BrowsedAppPath.Text = exePath;
                SelectedFileInfo.Visibility = Visibility.Visible;
                AddButton.Content = "Add Application";
            }
        }

        private ImageSource? GetAppIcon(string exePath)
        {
            return IconHelper.GetAppIcon(exePath);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunningAppsTab)
            {
                if (ProcessList.SelectedItem is ProcessItem item)
                {
                    SelectedProcess = item;
                    DialogResult = true;
                    Close();
                }
            }
            else
            {
                if (_browsedApp != null)
                {
                    SelectedProcess = _browsedApp;
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ProcessItem
    {
        public string ProcessName { get; set; } = "";
        public string MainWindowTitle { get; set; } = "";
        public string? ExePath { get; set; }
        public ImageSource? Icon { get; set; }
    }
}
