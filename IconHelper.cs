using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskWatch
{
    /// <summary>
    /// Shared helper for extracting and caching application icons.
    /// </summary>
    public static class IconHelper
    {
        /// <summary>
        /// Extracts the icon from an executable file and returns it as an ImageSource.
        /// </summary>
        /// <param name="exePath">Full path to the executable file.</param>
        /// <returns>The icon as an ImageSource, or null if extraction fails.</returns>
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
                        img.StreamSource = new MemoryStream(ms.ToArray()); // Create new stream for BitmapImage
                        img.CacheOption = BitmapCacheOption.OnLoad;
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
            
            return null;
        }
    }
}
