using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Manages a transparent overlay window that displays the keychain accessory.
    /// The overlay is positioned relative to a desktop item and automatically follows its movement.
    /// 
    /// Technical Details:
    /// - Uses layered window (WS_EX_LAYERED) with alpha blending for transparency
    /// - Uses WS_EX_TRANSPARENT to make the window click-through (no mouse interception)
    /// - Hosted by the desktop view so normal application windows remain above it
    /// - UpdateLayeredWindow is used to efficiently update the window's appearance without redraw overhead
    /// </summary>
    public class KeychainOverlay : IDisposable
    {
        private IntPtr _overlayHandle;
        private Bitmap _keychainBitmap;
        private readonly int _offsetX;
        private readonly int _offsetY;
        private bool _disposed;
        private int _lastOverlayX;
        private int _lastOverlayY;
        private bool _lastRepositionSucceeded;

        public event EventHandler Disposed;

        public KeychainOverlay(Bitmap keychainBitmap, int offsetX = 0, int offsetY = 0)
        {
            _keychainBitmap = keychainBitmap ?? throw new ArgumentNullException(nameof(keychainBitmap));
            _offsetX = offsetX;
            _offsetY = offsetY;

            CreateOverlayWindow();
        }

        private void CreateOverlayWindow()
        {
            try
            {
                IntPtr hInstance = Marshal.GetHINSTANCE(typeof(KeychainOverlay).Module);

                IntPtr desktopViewHandle = NativeMethods.FindDesktopView();
                if (desktopViewHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Desktop view window was not found.");
                }
                IntPtr desktopHostHandle = Win32Interop.GetAncestor(
                    desktopViewHandle,
                    Win32Interop.GA_ROOT);
                if (desktopHostHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Desktop host window was not found.");
                }

                // Extended window styles: transparent, layered, no activation.
                int exStyle = Win32Interop.WS_EX_TRANSPARENT | Win32Interop.WS_EX_LAYERED |
                              Win32Interop.WS_EX_NOACTIVATE;

                // Create foreground overlay window in front of the desktop icon
                _overlayHandle = Win32Interop.CreateWindowEx(
                    exStyle,
                    "STATIC",
                    "KeychainOverlay",
                    Win32Interop.WS_POPUP | Win32Interop.WS_VISIBLE,
                    0, 0,
                    _keychainBitmap.Width, _keychainBitmap.Height,
                    desktopHostHandle,
                    IntPtr.Zero,
                    hInstance,
                    IntPtr.Zero);

                if (_overlayHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create overlay window: " + Marshal.GetLastWin32Error());
                }

                UpdateLayeredImage(_overlayHandle, _keychainBitmap);
                NativeShowWindow(_overlayHandle, 4); // SW_SHOW
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating overlay window: {ex.Message}");
                throw;
            }
        }

        private static void UpdateLayeredImage(IntPtr handle, Bitmap bitmap)
        {
            if (handle == IntPtr.Zero || bitmap == null)
                return;

            try
            {
                IntPtr hdcDest = IntPtr.Zero;
                IntPtr hdcSrc = Win32Interop.CreateCompatibleDC(hdcDest);

                var bmpInfo = new Win32Interop.BITMAPINFO();
                bmpInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(Win32Interop.BITMAPINFOHEADER));
                bmpInfo.bmiHeader.biWidth = bitmap.Width;
                bmpInfo.bmiHeader.biHeight = -bitmap.Height;
                bmpInfo.bmiHeader.biPlanes = 1;
                bmpInfo.bmiHeader.biBitCount = 32;
                bmpInfo.bmiHeader.biCompression = 0;

                IntPtr ppvBits;
                IntPtr hBitmap = Win32Interop.CreateDIBSection(hdcDest, ref bmpInfo, 0, out ppvBits, IntPtr.Zero, 0);

                if (hBitmap == IntPtr.Zero)
                {
                    hBitmap = bitmap.GetHbitmap(System.Drawing.Color.Transparent);
                }
                else
                {
                    using (Bitmap sourceBitmap = new Bitmap(
                        bitmap.Width,
                        bitmap.Height,
                        PixelFormat.Format32bppPArgb))
                    using (Graphics graphics = Graphics.FromImage(sourceBitmap))
                    {
                        graphics.DrawImageUnscaled(bitmap, 0, 0);
                        BitmapData bitmapData = sourceBitmap.LockBits(
                            new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format32bppPArgb);
                        try
                        {
                            int rowBytes = sourceBitmap.Width * 4;
                            byte[] rowPixels = new byte[rowBytes];
                            for (int row = 0; row < sourceBitmap.Height; row++)
                            {
                                Marshal.Copy(
                                    IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride),
                                    rowPixels,
                                    0,
                                    rowBytes);
                                Marshal.Copy(
                                    rowPixels,
                                    0,
                                    IntPtr.Add(ppvBits, row * rowBytes),
                                    rowBytes);
                            }
                        }
                        finally
                        {
                            sourceBitmap.UnlockBits(bitmapData);
                        }
                    }
                }

                IntPtr hOld = Win32Interop.SelectObject(hdcSrc, hBitmap);

                var ptDst = new Win32Interop.POINT { X = 0, Y = 0 };
                var ptSrc = new Win32Interop.POINT { X = 0, Y = 0 };
                var size = new Win32Interop.SIZE { cx = bitmap.Width, cy = bitmap.Height };
                var blend = new Win32Interop.BLENDFUNCTION(255);

                bool success = Win32Interop.UpdateLayeredWindow(
                    handle, hdcDest, ref ptDst, ref size, hdcSrc, ref ptSrc, 0, ref blend,
                    Win32Interop.LWA_ALPHA);

                Win32Interop.SelectObject(hdcSrc, hOld);
                Win32Interop.DeleteObject(hBitmap);
                Win32Interop.DeleteDC(hdcSrc);

                if (!success)
                {
                    Debug.WriteLine("UpdateLayeredWindow failed: " + Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating overlay image: {ex.Message}");
            }
        }

        public bool SetPosition(double itemX, double itemY, double itemWidth, double itemHeight)
        {
            if (_disposed || _overlayHandle == IntPtr.Zero)
                return false;

            try
            {
                int overlayX = (int)(itemX + _offsetX);
                int overlayY = (int)(itemY + _offsetY);

                // Update foreground overlay position (in front of the desktop icon)
                bool repositioned = Win32Interop.SetWindowPos(
                    _overlayHandle,
                    IntPtr.Zero,
                    overlayX, overlayY,
                    _keychainBitmap.Width, _keychainBitmap.Height,
                    Win32Interop.SWP_NOACTIVATE |
                    Win32Interop.SWP_NOZORDER |
                    Win32Interop.SWP_SHOWWINDOW);
                _lastOverlayX = overlayX;
                _lastOverlayY = overlayY;
                _lastRepositionSucceeded = repositioned;
                NativeShowWindow(_overlayHandle, 5); // SW_SHOWNOACTIVATE
                Debug.WriteLine($"SetPosition overlay=({_overlayHandle}) item=({itemX},{itemY},{itemWidth},{itemHeight}) calculated=({overlayX},{overlayY}) result={repositioned}");
                return repositioned;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting overlay position: {ex.Message}");
                return false;
            }
        }

        public int LastOverlayX => _lastOverlayX;

        public int LastOverlayY => _lastOverlayY;

        public bool LastRepositionSucceeded => _lastRepositionSucceeded;

        public bool IsValid
        {
            get
            {
                if (_disposed || _overlayHandle == IntPtr.Zero)
                    return false;

                return NativeIsWindow(_overlayHandle);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (_overlayHandle != IntPtr.Zero)
                {
                    Win32Interop.DestroyWindow(_overlayHandle);
                    _overlayHandle = IntPtr.Zero;
                }

                _keychainBitmap?.Dispose();
                Disposed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing overlay: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        private static extern int NativeShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "IsWindow", SetLastError = true)]
        private static extern bool NativeIsWindow(IntPtr hWnd);
    }
}
