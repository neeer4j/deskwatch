using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskWatch
{
    public partial class AddAppWindow : Window
    {
        public ProcessItem? SelectedProcess { get; private set; }

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

        private void LoadProcesses()
        {
            var apps = new List<ProcessItem>();
            var processGroups = Process.GetProcesses()
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .GroupBy(p => p.ProcessName);

            foreach (var group in processGroups)
            {
                try
                {
                    // Pick the process with the best title (e.g. not empty), or just the first one
                    var p = group.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MainWindowTitle)) ?? group.First();
                    
                    var item = new ProcessItem
                    {
                        ProcessName = p.ProcessName,
                        MainWindowTitle = p.MainWindowTitle
                    };
                    
                    try
                    {
                        var path = p.MainModule?.FileName;
                        if (path != null && File.Exists(path))
                        {
                            item.Icon = GetAppIcon(path);
                        }
                    }
                    catch { }

                    apps.Add(item);
                }
                catch { }
            }

            ProcessList.ItemsSource = apps.OrderBy(a => a.MainWindowTitle).ToList();
        }

        private ImageSource? GetAppIcon(string exePath)
        {
             try
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
                    return img;
                }
            }
            catch { }
            return null;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessList.SelectedItem is ProcessItem item)
            {
                SelectedProcess = item;
                DialogResult = true;
                Close();
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
        public ImageSource? Icon { get; set; }
    }
}
