using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DeskWatch.Models
{
    public class AppUsage : INotifyPropertyChanged
    {
        private TimeSpan _total;
        private TimeSpan _todayTime;
        private ImageSource? _icon;
        private bool _isSelected;
        private int _focusCount;
        private int _todayFocusCount;
        private DateTime _lastTrackedDate = DateTime.Today;

        public string Key { get; }
        public string DisplayName { get; }
        public string? ExePath { get; set; }

        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan Total
        {
            get => _total;
            private set
            {
                if (_total != value)
                {
                    _total = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTotal));
                }
            }
        }

        public TimeSpan TodayTime
        {
            get => _todayTime;
            private set
            {
                if (_todayTime != value)
                {
                    _todayTime = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTodayTime));
                }
            }
        }

        public string FormattedTotal => string.Format("{0:00}:{1:00}:{2:00}", (int)Total.TotalHours, Total.Minutes, Total.Seconds);
        
        public string FormattedTodayTime => string.Format("{0:00}:{1:00}:{2:00}", (int)TodayTime.TotalHours, TodayTime.Minutes, TodayTime.Seconds);

        public int FocusCount
        {
            get => _focusCount;
            set
            {
                if (_focusCount != value)
                {
                    _focusCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TodayFocusCount
        {
            get => _todayFocusCount;
            set
            {
                if (_todayFocusCount != value)
                {
                    _todayFocusCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public AppUsage(string key, string displayName)
        {
            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
            _total = TimeSpan.Zero;
            _todayTime = TimeSpan.Zero;
            FocusCount = 0;
            TodayFocusCount = 0;
            _lastTrackedDate = DateTime.Today;
        }

        public void IncrementFocusCount()
        {
            CheckDayRollover();
            FocusCount++;
            TodayFocusCount++;
            OnPropertyChanged(nameof(FocusCount));
            OnPropertyChanged(nameof(TodayFocusCount));
        }

        public void Add(TimeSpan delta)
        {
            if (delta <= TimeSpan.Zero) return;
            CheckDayRollover();
            Total = Total + delta;
            TodayTime = TodayTime + delta;
        }

        public void AddToTotal(TimeSpan delta)
        {
            if (delta <= TimeSpan.Zero) return;
            Total = Total + delta;
        }

        public void SetTodayTime(TimeSpan time)
        {
            TodayTime = time;
            _lastTrackedDate = DateTime.Today;
        }

        /// <summary>
        /// Checks if the day has changed and resets today's counters if needed
        /// </summary>
        private void CheckDayRollover()
        {
            if (_lastTrackedDate.Date != DateTime.Today)
            {
                // Day changed - reset today's counters
                TodayTime = TimeSpan.Zero;
                TodayFocusCount = 0;
                _lastTrackedDate = DateTime.Today;
            }
        }

        /// <summary>
        /// Called when a new day starts - archives today's data and resets
        /// </summary>
        public void OnDayRollover()
        {
            TodayTime = TimeSpan.Zero;
            TodayFocusCount = 0;
            _lastTrackedDate = DateTime.Today;
            OnPropertyChanged(nameof(TodayTime));
            OnPropertyChanged(nameof(FormattedTodayTime));
            OnPropertyChanged(nameof(TodayFocusCount));
        }

        public void Reset()
        {
            Total = TimeSpan.Zero;
            TodayTime = TimeSpan.Zero;
            FocusCount = 0;
            TodayFocusCount = 0;
            OnPropertyChanged(nameof(FocusCount));
            OnPropertyChanged(nameof(TodayFocusCount));
        }

        public void ResetToday()
        {
            TodayTime = TimeSpan.Zero;
            TodayFocusCount = 0;
            OnPropertyChanged(nameof(TodayFocusCount));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
