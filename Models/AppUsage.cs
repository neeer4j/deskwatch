using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DeskWatch.Models
{
    public class AppUsage : INotifyPropertyChanged
    {
        private TimeSpan _total;
        private ImageSource? _icon;
        private bool _isSelected;
        private int _focusCount;

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

        public string FormattedTotal => string.Format("{0:00}:{1:00}:{2:00}", (int)Total.TotalHours, Total.Minutes, Total.Seconds);

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

        public AppUsage(string key, string displayName)
        {
            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
            _total = TimeSpan.Zero;
            FocusCount = 0;
        }

        public void IncrementFocusCount()
        {
            FocusCount++;
            OnPropertyChanged(nameof(FocusCount));
        }

        public void Add(TimeSpan delta)
        {
            if (delta <= TimeSpan.Zero) return;
            Total = Total + delta;
        }

        public void Reset()
        {
            Total = TimeSpan.Zero;
            FocusCount = 0;
            OnPropertyChanged(nameof(FocusCount));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
