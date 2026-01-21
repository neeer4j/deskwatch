using System;
using System.Collections.Generic;

namespace DeskWatch.Models
{
    /// <summary>
    /// Represents usage data for a single day
    /// </summary>
    public class DailyUsageEntry
    {
        public DateTime Date { get; set; }
        public long TotalSeconds { get; set; }
        public Dictionary<string, long> AppSeconds { get; set; } = new();

        public TimeSpan TotalTime => TimeSpan.FromSeconds(TotalSeconds);
        
        public string FormattedTotal => string.Format("{0:00}:{1:00}:{2:00}", 
            (int)TotalTime.TotalHours, TotalTime.Minutes, TotalTime.Seconds);

        public string FormattedTotalShort
        {
            get
            {
                if (TotalTime.TotalHours >= 1)
                    return $"{(int)TotalTime.TotalHours}h {TotalTime.Minutes}m";
                else if (TotalTime.TotalMinutes >= 1)
                    return $"{(int)TotalTime.TotalMinutes}m";
                else
                    return $"{TotalTime.Seconds}s";
            }
        }

        public string DayLabel => Date.ToString("ddd");
        public string DateLabel => Date.ToString("MMM d");
        public bool IsToday => Date.Date == DateTime.Today;
    }

    /// <summary>
    /// Historical usage data stored separately from live tracking
    /// </summary>
    public class HistoricalData
    {
        public List<DailyUsageEntry> DailyUsage { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Maximum days to keep in history
        /// </summary>
        public const int MaxDays = 14;
    }

    /// <summary>
    /// View model for displaying daily usage in the UI
    /// </summary>
    public class DailyUsageViewModel
    {
        public DateTime Date { get; set; }
        public TimeSpan TotalTime { get; set; }
        public double BarHeight { get; set; }
        public double BarHeightPercentage { get; set; }
        public bool IsToday { get; set; }
        public bool IsSelected { get; set; }
        
        public string DayLabel => Date.ToString("ddd");
        public string DateLabel => Date.ToString("MMM d");
        public string FullDateLabel => Date.ToString("dddd, MMMM d");
        
        public string FormattedTotal => string.Format("{0:00}:{1:00}:{2:00}", 
            (int)TotalTime.TotalHours, TotalTime.Minutes, TotalTime.Seconds);

        public string FormattedTotalShort
        {
            get
            {
                if (TotalTime.TotalHours >= 1)
                    return $"{(int)TotalTime.TotalHours}h {TotalTime.Minutes}m";
                else if (TotalTime.TotalMinutes >= 1)
                    return $"{(int)TotalTime.TotalMinutes}m";
                else
                    return $"{TotalTime.Seconds}s";
            }
        }
    }

    /// <summary>
    /// View model for app breakdown in a day
    /// </summary>
    public class AppUsageBreakdown
    {
        public string AppName { get; set; } = "";
        public string AppKey { get; set; } = "";
        public TimeSpan Time { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#6366F1";

        public string FormattedTime
        {
            get
            {
                if (Time.TotalHours >= 1)
                    return $"{(int)Time.TotalHours}h {Time.Minutes}m";
                else if (Time.TotalMinutes >= 1)
                    return $"{(int)Time.TotalMinutes}m";
                else
                    return $"{Time.Seconds}s";
            }
        }

        public string FormattedPercentage => $"{Percentage:F0}%";
    }
}
