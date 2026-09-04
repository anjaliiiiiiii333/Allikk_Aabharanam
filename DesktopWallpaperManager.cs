using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Manages switching the Windows desktop wallpaper to white when an accessory is attached,
    /// and restoring the original user wallpaper when detached.
    /// </summary>
    public static class DesktopWallpaperManager
    {
        private const uint SPI_GETDESKWALLPAPER = 0x0073;
        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SystemParametersInfo(
            uint uAction,
            uint uParam,
            StringBuilder lpvParam,
            uint fuWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SystemParametersInfo(
            uint uAction,
            uint uParam,
            string lpvParam,
            uint fuWinIni);

        private static string _originalWallpaperPath;
        private static string _whiteWallpaperPath;
        private static bool _isWhiteWallpaperActive;

        public static void SetWhiteWallpaper()
        {
            if (_isWhiteWallpaperActive)
                return;

            try
            {
                // Capture original wallpaper path
                string current = GetCurrentWallpaper();
                if (!string.IsNullOrEmpty(current) && !current.Equals(_whiteWallpaperPath, StringComparison.OrdinalIgnoreCase))
                {
                    _originalWallpaperPath = current;
                }

                // Generate solid white wallpaper image
                if (_whiteWallpaperPath == null || !File.Exists(_whiteWallpaperPath))
                {
                    _whiteWallpaperPath = Path.Combine(
                        Path.GetTempPath(),
                        "desktop_keychain_white_wallpaper.bmp");

                    using (var bmp = new Bitmap(1920, 1080))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        bmp.Save(_whiteWallpaperPath, ImageFormat.Bmp);
                    }
                }

                // Apply white wallpaper to desktop
                bool result = SystemParametersInfo(
                    SPI_SETDESKWALLPAPER,
                    0,
                    _whiteWallpaperPath,
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                Debug.WriteLine($"SetWhiteWallpaper result={result}, path={_whiteWallpaperPath}");
                _isWhiteWallpaperActive = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting white wallpaper: {ex.Message}");
            }
        }

        public static void RestoreOriginalWallpaper()
        {
            if (!_isWhiteWallpaperActive)
                return;

            try
            {
                string wallpaperToRestore = _originalWallpaperPath;
                if (string.IsNullOrEmpty(wallpaperToRestore) || !File.Exists(wallpaperToRestore))
                {
                    wallpaperToRestore = GetRegistryWallpaper();
                }

                if (!string.IsNullOrEmpty(wallpaperToRestore) && File.Exists(wallpaperToRestore))
                {
                    bool result = SystemParametersInfo(
                        SPI_SETDESKWALLPAPER,
                        0,
                        wallpaperToRestore,
                        SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                    Debug.WriteLine($"RestoreOriginalWallpaper result={result}, path={wallpaperToRestore}");
                }

                _isWhiteWallpaperActive = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring original wallpaper: {ex.Message}");
            }
        }

        private static string GetCurrentWallpaper()
        {
            try
            {
                var sb = new StringBuilder(512);
                if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0) && sb.Length > 0)
                {
                    string path = sb.ToString();
                    if (File.Exists(path))
                        return path;
                }
            }
            catch { }

            return GetRegistryWallpaper();
        }

        private static string GetRegistryWallpaper()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key != null)
                {
                    object val = key.GetValue("Wallpaper");
                    if (val is string path && File.Exists(path))
                        return path;
                }
            }
            catch { }

            string transcoded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(transcoded))
                return transcoded;

            return "";
        }
    }
}
