using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public long TodaySeconds { get; set; }
        public int FocusCount { get; set; }
        public int TodayFocusCount { get; set; }
        public DateTime LastTrackedDate { get; set; } = DateTime.Today;
    }

    public class SavedData
    {
        public List<AppUsageData> TrackedApps { get; set; } = new();
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;
        public DateTime CurrentTrackingDate { get; set; } = DateTime.Today;
    }

    public static class DataManager
    {
        private static readonly string DataFile = Path.Combine(
            SettingsManager.GetAppDataFolder(), 
            "usage_data.json");
        
        private static readonly string HistoryFile = Path.Combine(
            SettingsManager.GetAppDataFolder(), 
            "history_data.json");
        
        // Cached JSON serializer options (reuse to avoid allocations)
        private static readonly JsonSerializerOptions SerializerOptions = new() 
        { 
            WriteIndented = true 
        };
        
        // Reusable list to reduce allocations during save
        private static readonly List<AppUsageData> _appDataBuffer = new(32);

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
                // Reuse buffer to reduce GC pressure
                _appDataBuffer.Clear();
                
                foreach (var app in apps)
                {
                    _appDataBuffer.Add(new AppUsageData
                    {
                        Key = app.Key,
                        DisplayName = app.DisplayName,
                        ExePath = app.ExePath,
                        TotalSeconds = (long)app.Total.TotalSeconds,
                        TodaySeconds = (long)app.TodayTime.TotalSeconds,
                        FocusCount = app.FocusCount,
                        TodayFocusCount = app.TodayFocusCount,
                        LastTrackedDate = DateTime.Today
                    });
                }
                
                var data = new SavedData
                {
                    LastSaved = DateTime.UtcNow,
                    CurrentTrackingDate = DateTime.Today,
                    TrackedApps = new List<AppUsageData>(_appDataBuffer)
                };

                // Ensure directory exists
                var dir = Path.GetDirectoryName(DataFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(data, SerializerOptions);
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

        #region Historical Data

        public static HistoricalData LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    var json = File.ReadAllText(HistoryFile);
                    return JsonSerializer.Deserialize<HistoricalData>(json) ?? new HistoricalData();
                }
            }
            catch { }
            
            return new HistoricalData();
        }

        public static void SaveHistory(HistoricalData history)
        {
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(HistoryFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Trim to max days
                if (history.DailyUsage.Count > HistoricalData.MaxDays)
                {
                    history.DailyUsage = history.DailyUsage
                        .OrderByDescending(d => d.Date)
                        .Take(HistoricalData.MaxDays)
                        .ToList();
                }

                history.LastUpdated = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(history, SerializerOptions);
                File.WriteAllText(HistoryFile, json);
            }
            catch { }
        }

        public static void ArchiveDayToHistory(DateTime date, IEnumerable<AppUsage> apps)
        {
            try
            {
                var history = LoadHistory();
                
                // Check if this day already exists
                var existingEntry = history.DailyUsage.FirstOrDefault(d => d.Date.Date == date.Date);
                if (existingEntry != null)
                {
                    history.DailyUsage.Remove(existingEntry);
                }

                // Create new entry with pre-sized dictionary
                var appsList = apps.ToList();
                var entry = new DailyUsageEntry
                {
                    Date = date.Date,
                    TotalSeconds = 0,
                    AppSeconds = new Dictionary<string, long>(appsList.Count)
                };

                foreach (var app in appsList)
                {
                    var seconds = (long)app.TodayTime.TotalSeconds;
                    if (seconds > 0)
                    {
                        entry.AppSeconds[app.Key] = seconds;
                        entry.TotalSeconds += seconds;
                    }
                }

                // Only save if there's data
                if (entry.TotalSeconds > 0)
                {
                    history.DailyUsage.Add(entry);
                    SaveHistory(history);
                }
            }
            catch { }
        }

        public static void UpdateTodayInHistory(IEnumerable<AppUsage> apps)
        {
            try
            {
                var history = LoadHistory();
                var today = DateTime.Today;
                
                // Find or create today's entry
                var todayEntry = history.DailyUsage.FirstOrDefault(d => d.Date.Date == today);
                if (todayEntry == null)
                {
                    todayEntry = new DailyUsageEntry
                    {
                        Date = today,
                        AppSeconds = new Dictionary<string, long>()
                    };
                    history.DailyUsage.Add(todayEntry);
                }

                // Update with current data
                todayEntry.TotalSeconds = 0;
                todayEntry.AppSeconds.Clear();
                
                foreach (var app in apps)
                {
                    var seconds = (long)app.TodayTime.TotalSeconds;
                    if (seconds > 0)
                    {
                        todayEntry.AppSeconds[app.Key] = seconds;
                        todayEntry.TotalSeconds += seconds;
                    }
                }

                SaveHistory(history);
            }
            catch { }
        }

        public static List<DailyUsageEntry> GetLast14Days()
        {
            var history = LoadHistory();
            var result = new List<DailyUsageEntry>();
            var today = DateTime.Today;

            // Ensure we have entries for all 14 days (fill gaps with zeros)
            for (int i = 13; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var entry = history.DailyUsage.FirstOrDefault(d => d.Date.Date == date.Date);
                
                if (entry != null)
                {
                    result.Add(entry);
                }
                else
                {
                    result.Add(new DailyUsageEntry
                    {
                        Date = date,
                        TotalSeconds = 0,
                        AppSeconds = new Dictionary<string, long>()
                    });
                }
            }

            return result;
        }

        public static void ClearHistory()
        {
            try
            {
                if (File.Exists(HistoryFile))
                {
                    File.Delete(HistoryFile);
                }
            }
            catch { }
        }

        #endregion
    }
}
