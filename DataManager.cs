using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DeskWatch.Models;

namespace DeskWatch
{
    public class AppUsageData
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? ExePath { get; set; }
        public long TotalSeconds { get; set; }
        public int FocusCount { get; set; }
    }

    public class SavedData
    {
        public List<AppUsageData> TrackedApps { get; set; } = new();
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    }

    public static class DataManager
    {
        private static readonly string DataFile = Path.Combine(
            SettingsManager.GetAppDataFolder(), 
            "usage_data.json");

        public static SavedData Load()
        {
            try
            {
                if (File.Exists(DataFile))
                {
                    var json = File.ReadAllText(DataFile);
                    return JsonSerializer.Deserialize<SavedData>(json) ?? new SavedData();
                }
            }
            catch { }
            
            return new SavedData();
        }

        public static void Save(IEnumerable<AppUsage> apps)
        {
            try
            {
                var data = new SavedData
                {
                    LastSaved = DateTime.UtcNow,
                    TrackedApps = new List<AppUsageData>()
                };

                foreach (var app in apps)
                {
                    data.TrackedApps.Add(new AppUsageData
                    {
                        Key = app.Key,
                        DisplayName = app.DisplayName,
                        ExePath = app.ExePath,
                        TotalSeconds = (long)app.Total.TotalSeconds,
                        FocusCount = app.FocusCount
                    });
                }

                // Ensure directory exists
                var dir = Path.GetDirectoryName(DataFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DataFile, json);
            }
            catch { }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(DataFile))
                {
                    File.Delete(DataFile);
                }
            }
            catch { }
        }
    }
}
