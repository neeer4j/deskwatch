using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DeskWatch.Models;

namespace DeskWatch.Controls
{
    public partial class ScreenTimeControl : UserControl
    {
        private readonly List<DailyUsageViewModel> _chartData = new();
        private DailyUsageViewModel? _selectedDay;
        
        // Live refresh timer
        private DispatcherTimer? _refreshTimer;
        
        // Reference to get live screen time
        private Func<TimeSpan>? _getScreenTime;

        public ScreenTimeControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Start live refresh timer when control is visible
            if (_refreshTimer == null)
            {
                _refreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1) // Refresh every second for live count
                };
                _refreshTimer.Tick += RefreshTimer_Tick;
            }
            _refreshTimer.Start();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop timer when hidden to save resources
            _refreshTimer?.Stop();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Update today's time from main window
            if (_getScreenTime != null)
            {
                var screenTime = _getScreenTime();
                TodayTimeText.Text = FormatTimeShort(screenTime);
                
                // Update today's bar in chart if present
                var todayVm = _chartData.FirstOrDefault(d => d.IsToday);
                if (todayVm != null)
                {
                    todayVm.TotalTime = screenTime;
                    
                    // Recalculate bar height
                    var maxSeconds = _chartData.Max(d => d.TotalTime.TotalSeconds);
                    if (maxSeconds < 1) maxSeconds = 1;
                    todayVm.BarHeight = Math.Max(3, (screenTime.TotalSeconds / maxSeconds) * 120);
                    
                    // Update selected day if today is selected
                    if (_selectedDay?.IsToday == true)
                    {
                        SelectedDayTime.Text = $" — {FormatTimeShort(screenTime)}";
                    }
                }
            }
        }

        /// <summary>
        /// Initialize with a function to get live screen time
        /// </summary>
        public void Initialize(Func<TimeSpan> getScreenTime)
        {
            _getScreenTime = getScreenTime;
        }

        /// <summary>
        /// Full refresh of screen time data
        /// </summary>
        public void RefreshData(IEnumerable<AppUsage> trackedApps)
        {
            try
            {
                // Load 14 days of history
                var history = DataManager.GetLast14Days();
                
                // Get current screen time from main window
                var todayScreenTime = _getScreenTime?.Invoke() ?? TimeSpan.Zero;
                
                // Update today's entry with live data
                var todayEntry = history.FirstOrDefault(d => d.Date.Date == DateTime.Today);
                if (todayEntry != null)
                {
                    todayEntry.TotalSeconds = (long)todayScreenTime.TotalSeconds;
                }
                
                // Update summary cards
                UpdateSummaryCards(history, todayScreenTime);
                
                // Build chart
                BuildChartData(history);
                
                // Update date range
                if (history.Count >= 2)
                {
                    DateRangeText.Text = $"{history[0].Date:MMM d} - {history[^1].Date:MMM d}";
                }
                
                // Select today by default
                if (_selectedDay == null)
                {
                    var today = _chartData.FirstOrDefault(d => d.IsToday);
                    if (today != null)
                    {
                        SelectDay(today);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing screen time: {ex.Message}");
            }
        }

        private void UpdateSummaryCards(List<DailyUsageEntry> history, TimeSpan todayScreenTime)
        {
            // Today's time
            TodayTimeText.Text = FormatTimeShort(todayScreenTime);

            // Total 14 days
            var totalSeconds = history.Sum(d => d.TotalSeconds);
            TotalTimeText.Text = FormatTimeShort(TimeSpan.FromSeconds(totalSeconds));

            // Weekly average (last 7 days)
            var last7 = history.Where(d => d.Date >= DateTime.Today.AddDays(-6)).ToList();
            if (last7.Count > 0)
            {
                var avgSeconds = last7.Average(d => d.TotalSeconds);
                WeeklyAvgText.Text = FormatTimeShort(TimeSpan.FromSeconds(avgSeconds));
            }
            else
            {
                WeeklyAvgText.Text = "0s";
            }
        }

        private void BuildChartData(List<DailyUsageEntry> history)
        {
            _chartData.Clear();
            
            var maxSeconds = history.Count > 0 ? history.Max(d => d.TotalSeconds) : 1;
            if (maxSeconds == 0) maxSeconds = 1;
            
            const double maxBarHeight = 120;
            const double maxProgressWidth = 180;

            foreach (var entry in history)
            {
                _chartData.Add(new DailyUsageViewModel
                {
                    Date = entry.Date,
                    TotalTime = entry.TotalTime,
                    BarHeight = Math.Max(3, (entry.TotalSeconds / (double)maxSeconds) * maxBarHeight),
                    BarHeightPercentage = Math.Max(0, (entry.TotalSeconds / (double)maxSeconds) * maxProgressWidth),
                    IsToday = entry.Date.Date == DateTime.Today,
                    IsSelected = _selectedDay?.Date.Date == entry.Date.Date
                });
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

            SelectedDayTitle.Text = day.FullDateLabel;
            SelectedDayTime.Text = $" — {day.FormattedTotalShort}";
            SelectedDayPanel.Visibility = Visibility.Visible;

            // No app breakdown for global screen time
            AppBreakdownList.ItemsSource = null;

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
                return $"{(int)time.TotalMinutes}m {time.Seconds}s";
            else
                return $"{time.Seconds}s";
        }
    }
}
