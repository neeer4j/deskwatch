using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskWatch
{
    /// <summary>
    /// Shared helper for extracting and caching application icons.
    /// </summary>
    public static class IconHelper
    {
        private static ImageSource? _fallbackIcon;

        /// <summary>
        /// Gets a fallback icon for apps where extraction fails.
        /// </summary>
        public static ImageSource GetFallbackIcon()
        {
            if (_fallbackIcon != null)
                return _fallbackIcon;

            // Create a simple fallback icon programmatically
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Draw a rounded rectangle with gradient fill
                var gradientBrush = new LinearGradientBrush(
                    Color.FromRgb(99, 102, 241),   // AccentPrimaryColor
                    Color.FromRgb(139, 92, 246),   // AccentSecondaryColor
                    45);

                var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
                context.DrawRoundedRectangle(gradientBrush, pen, new Rect(0, 0, 48, 48), 10, 10);

                // Draw a simple app icon shape (window)
                var iconBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
                context.DrawRoundedRectangle(iconBrush, null, new Rect(10, 14, 28, 22), 3, 3);

                // Title bar
                context.DrawRoundedRectangle(iconBrush, null, new Rect(10, 10, 28, 6), 3, 3);
            }

            var bitmap = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            _fallbackIcon = bitmap;
            return _fallbackIcon;
        }

        /// <summary>
        /// Extracts the icon from an executable file and returns it as an ImageSource.
        /// </summary>
        /// <param name="exePath">Full path to the executable file.</param>
        /// <returns>The icon as an ImageSource, or a fallback icon if extraction fails.</returns>
        public static ImageSource? GetAppIcon(string exePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        using var bmp = icon.ToBitmap();
                        using var ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;

                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                        img.Freeze(); // Make it thread-safe
                        return img;
                    }
                }
            }
            catch
            {
                // Icon extraction can fail for various reasons (access denied, invalid exe, etc.)
            }
            
            // Return fallback icon instead of null
            return GetFallbackIcon();
        }
    }
}
