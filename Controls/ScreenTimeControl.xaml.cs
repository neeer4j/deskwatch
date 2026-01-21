using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskWatch.Models;

namespace DeskWatch.Controls
{
    public partial class ScreenTimeControl : UserControl
    {
        // Pre-allocated color array (static readonly for efficiency)
        private static readonly string[] ChartColors =
        {
            "#06B6D4", "#14B8A6", "#22C55E", "#84CC16", "#EAB308",
            "#F59E0B", "#F97316", "#EF4444", "#EC4899", "#A855F7",
            "#6366F1", "#3B82F6", "#0EA5E9", "#10B981"
        };

        private readonly ObservableCollection<DailyUsageViewModel> _chartData = new();
        private DailyUsageViewModel? _selectedDay;
        private readonly Dictionary<string, string> _appDisplayNames = new(32);
        
        // Cache the last history to avoid reloading when selecting days
        private List<DailyUsageEntry>? _cachedHistory;

        public ScreenTimeControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Refreshes all screen time data from history
        /// </summary>
        public void RefreshData(IEnumerable<AppUsage> trackedApps)
        {
            try
            {
                // Cache app display names (reuse dictionary)
                _appDisplayNames.Clear();
                foreach (var app in trackedApps)
                {
                    _appDisplayNames[app.Key] = app.DisplayName;
                }

                // Update today's data in history first
                DataManager.UpdateTodayInHistory(trackedApps);

                // Load 14 days of history and cache it
                _cachedHistory = DataManager.GetLast14Days();
                
                // Calculate statistics
                UpdateSummaryCards(_cachedHistory);
                
                // Build chart data
                BuildChartData(_cachedHistory);
                
                // Update date range text
                if (_cachedHistory.Count >= 2)
                {
                    var firstDate = _cachedHistory[0].Date;
                    var lastDate = _cachedHistory[^1].Date;
                    DateRangeText.Text = $"{firstDate:MMM d} - {lastDate:MMM d}";
                }
                
                // Select today by default if nothing selected
                if (_selectedDay == null)
                {
                    var today = _chartData.FirstOrDefault(d => d.IsToday);
                    if (today != null)
                    {
                        SelectDay(today);
                    }
                }
                else
                {
                    // Refresh the selected day's data
                    var updated = _chartData.FirstOrDefault(d => d.Date.Date == _selectedDay.Date.Date);
                    if (updated != null)
                    {
                        SelectDay(updated);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing screen time: {ex.Message}");
            }
        }

        private void UpdateSummaryCards(List<DailyUsageEntry> history)
        {
            // Today's time
            var todayEntry = history.FirstOrDefault(d => d.Date.Date == DateTime.Today);
            var todayTime = todayEntry?.TotalTime ?? TimeSpan.Zero;
            TodayTimeText.Text = FormatTimeShort(todayTime);

            // Total 14 days
            var totalSeconds = history.Sum(d => d.TotalSeconds);
            var totalTime = TimeSpan.FromSeconds(totalSeconds);
            TotalTimeText.Text = FormatTimeShort(totalTime);

            // Weekly average (last 7 days)
            var last7Days = history.Where(d => d.Date >= DateTime.Today.AddDays(-6)).ToList();
            if (last7Days.Count > 0)
            {
                var avgSeconds = last7Days.Average(d => d.TotalSeconds);
                var avgTime = TimeSpan.FromSeconds(avgSeconds);
                WeeklyAvgText.Text = FormatTimeShort(avgTime);
            }
            else
            {
                WeeklyAvgText.Text = "0h 0m";
            }

            // Update Y-axis labels based on max time
            var maxSeconds = history.Count > 0 ? history.Max(d => d.TotalSeconds) : 0;
            UpdateYAxisLabels(maxSeconds);
        }

        private void UpdateYAxisLabels(long maxSeconds)
        {
            // Round up to nice intervals
            var maxHours = Math.Max(1, Math.Ceiling(maxSeconds / 3600.0));
            
            // Choose nice round numbers
            double[] niceIntervals = { 1, 2, 4, 6, 8, 10, 12, 16, 20, 24 };
            var targetMax = niceIntervals.FirstOrDefault(n => n >= maxHours);
            if (targetMax == 0) targetMax = 24;

            var interval = targetMax / 4.0;

            YLabel4.Text = $"{targetMax:F0}h";
            YLabel3.Text = $"{targetMax * 0.75:F0}h";
            YLabel2.Text = $"{targetMax * 0.5:F0}h";
            YLabel1.Text = $"{targetMax * 0.25:F0}h";
        }

        private void BuildChartData(List<DailyUsageEntry> history)
        {
            _chartData.Clear();
            
            // Find max for scaling
            var maxSeconds = history.Count > 0 ? history.Max(d => d.TotalSeconds) : 1;
            if (maxSeconds == 0) maxSeconds = 1;
            
            // Max bar height in pixels
            const double maxBarHeight = 180;
            const double maxPercentWidth = 200; // For progress bar in table

            foreach (var entry in history)
            {
                var vm = new DailyUsageViewModel
                {
                    Date = entry.Date,
                    TotalTime = entry.TotalTime,
                    BarHeight = Math.Max(4, (entry.TotalSeconds / (double)maxSeconds) * maxBarHeight),
                    BarHeightPercentage = Math.Max(0, (entry.TotalSeconds / (double)maxSeconds) * maxPercentWidth),
                    IsToday = entry.Date.Date == DateTime.Today,
                    IsSelected = _selectedDay?.Date.Date == entry.Date.Date
                };
                _chartData.Add(vm);
            }

            BarChart.ItemsSource = _chartData;
            DailyLogList.ItemsSource = _chartData.OrderByDescending(d => d.Date).ToList();
        }

        private void SelectDay(DailyUsageViewModel day)
        {
            // Deselect previous
            if (_selectedDay != null)
            {
                _selectedDay.IsSelected = false;
            }

            _selectedDay = day;
            day.IsSelected = true;

            // Update UI
            SelectedDayTitle.Text = day.FullDateLabel;
            SelectedDayTime.Text = $" — {day.FormattedTotalShort}";
            SelectedDayPanel.Visibility = Visibility.Visible;

            // Use cached history instead of reloading from disk
            var dayEntry = _cachedHistory?.FirstOrDefault(d => d.Date.Date == day.Date.Date);
            
            if (dayEntry != null && dayEntry.AppSeconds.Count > 0)
            {
                var breakdown = new List<AppUsageBreakdown>(dayEntry.AppSeconds.Count);
                var colorIndex = 0;
                var totalSeconds = dayEntry.TotalSeconds;

                foreach (var kvp in dayEntry.AppSeconds.OrderByDescending(k => k.Value))
                {
                    var displayName = _appDisplayNames.TryGetValue(kvp.Key, out var name) ? name : kvp.Key;
                    breakdown.Add(new AppUsageBreakdown
                    {
                        AppKey = kvp.Key,
                        AppName = displayName,
                        Time = TimeSpan.FromSeconds(kvp.Value),
                        Percentage = totalSeconds > 0 ? (kvp.Value * 100.0 / totalSeconds) : 0,
                        Color = ChartColors[colorIndex % ChartColors.Length]
                    });
                    colorIndex++;
                }

                AppBreakdownList.ItemsSource = breakdown;
            }
            else
            {
                AppBreakdownList.ItemsSource = null;
            }

            // Refresh chart to update selection highlight
            BarChart.Items.Refresh();
        }

        private void BarChart_DayClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is DailyUsageViewModel day)
            {
                SelectDay(day);
            }
        }

        private void DailyLog_RowClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DailyUsageViewModel day)
            {
                SelectDay(day);
            }
        }

        private static string FormatTimeShort(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            else if (time.TotalMinutes >= 1)
                return $"{(int)time.TotalMinutes}m";
            else
                return $"{time.Seconds}s";
        }
    }
}
