using System;
using System.Windows;
using System.Windows.Threading;

namespace AccessoryPrototype
{
    public sealed class PrototypeTracker : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly string _iconName;
        private readonly PrototypeOverlayWindow _overlay;
        private Rect? _lastBounds;
        private bool _wasLeftButtonDown;
        private bool _dragging;
        private double _offsetX;
        private double _offsetY;

        public PrototypeTracker(string iconName, PrototypeOverlayWindow overlay)
        {
            _iconName = iconName;
            _overlay = overlay;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += Tick;
        }

        public void Start()
        {
            Tick(this, EventArgs.Empty);
            _timer.Start();
        }

        public void Dispose()
        {
            _timer.Stop();
        }

        private void Tick(object sender, EventArgs args)
        {
            bool leftButtonDown = NativeMethods.IsLeftButtonDown();
            bool dragEnded = _dragging && !leftButtonDown;
            if (dragEnded)
                _dragging = false;

            if (!_dragging && leftButtonDown && !_wasLeftButtonDown && _lastBounds.HasValue &&
                NativeMethods.GetCursorPos(out NativeMethods.Point cursor) &&
                _lastBounds.Value.Contains(cursor.X, cursor.Y))
            {
                _offsetX = _lastBounds.Value.Left - cursor.X;
                _offsetY = _lastBounds.Value.Top - cursor.Y;
                _dragging = true;
            }

            if (_dragging && leftButtonDown && NativeMethods.GetCursorPos(out NativeMethods.Point dragCursor))
            {
                Rect bounds = _lastBounds ?? new Rect(dragCursor.X, dragCursor.Y, 64, 64);
                bounds.X = dragCursor.X + _offsetX;
                bounds.Y = dragCursor.Y + _offsetY;
                _overlay.SetTarget(bounds);
            }
            else
            {
                DesktopIconInfo current = DesktopIconProbe.FindIcon(_iconName);
                if (current != null)
                {
                    _lastBounds = current.Bounds;
                    _overlay.SetTarget(current.Bounds);
                }
            }

            _wasLeftButtonDown = leftButtonDown;
        }

        private static class NativeMethods
        {
            public struct Point
            {
                public int X;
                public int Y;
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern short GetAsyncKeyState(int virtualKey);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern bool GetCursorPos(out Point point);

            public static bool IsLeftButtonDown() => (GetAsyncKeyState(0x01) & 0x8000) != 0;
        }
    }
}
