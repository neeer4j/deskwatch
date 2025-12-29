using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace DeskWatch
{
    public class AppSettings
    {
        public bool AutoStartEnabled { get; set; } = true;
        public bool FirstRun { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeskWatch");
        
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");
        
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DeskWatch";

        private static AppSettings? _settings;
        
        public static AppSettings Settings
        {
            get
            {
                _settings ??= Load();
                return _settings;
            }
        }

        public static void Initialize()
        {
            // Ensure app data folder exists
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            // Load settings
            var settings = Settings;

            // On first run, enable auto-start
            if (settings.FirstRun)
            {
                settings.FirstRun = false;
                settings.AutoStartEnabled = true;
                SetAutoStart(true);
                Save();
            }
            else if (settings.AutoStartEnabled)
            {
                // Ensure registry entry exists if setting is enabled
                SetAutoStart(true);
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            
            return new AppSettings();
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public static bool GetAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoStart(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null) return;

                if (enabled)
                {
                    // Get the current executable path
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }

                Settings.AutoStartEnabled = enabled;
                Save();
            }
            catch { }
        }

        public static string GetAppDataFolder() => AppDataFolder;
    }
}
