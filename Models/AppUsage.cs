using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeskWatch.Models
{
    public class AppUsage : INotifyPropertyChanged
    {
        private TimeSpan _total;

        public string Key { get; }
        public string DisplayName { get; }

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

        public AppUsage(string key, string displayName)
        {
            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
            _total = TimeSpan.Zero;
        }

        public void Add(TimeSpan delta)
        {
            if (delta <= TimeSpan.Zero) return;
            Total = Total + delta;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
