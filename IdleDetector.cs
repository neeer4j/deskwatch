using System;
using System.Runtime.InteropServices;

namespace DeskWatch
{
    /// <summary>
    /// Utility class to detect user idle time using Win32 API.
    /// </summary>
    public static class IdleDetector
    {
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        /// <summary>
        /// Gets the time in seconds since the last user input (mouse/keyboard).
        /// </summary>
        public static TimeSpan GetIdleTime()
        {
            var lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);

            if (GetLastInputInfo(ref lastInput))
            {
                // Use TickCount64 to avoid overflow after ~49.7 days of uptime
                // Mask to 32 bits since dwTime is a 32-bit value from Win32 API
                var currentTick = Environment.TickCount64 & 0xFFFFFFFF;
                var lastInputTick = (long)lastInput.dwTime;
                
                // Handle wrap-around case
                var idleMs = currentTick >= lastInputTick 
                    ? currentTick - lastInputTick 
                    : (0xFFFFFFFF - lastInputTick) + currentTick + 1;
                    
                return TimeSpan.FromMilliseconds(idleMs);
            }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Checks if the user has been idle for at least the specified duration.
        /// </summary>
        public static bool IsIdle(TimeSpan threshold)
        {
            return GetIdleTime() >= threshold;
        }
    }
}
