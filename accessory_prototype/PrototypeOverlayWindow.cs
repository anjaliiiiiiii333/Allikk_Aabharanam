using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Linq;

namespace AccessoryPrototype
{
    public sealed class PrototypeOverlayWindow : Window
    {
        private readonly Canvas _canvas = new Canvas();
        private readonly Border _protectiveLayer = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 190, 225, 240)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(18, 210, 235, 245)),
            CornerRadius = new CornerRadius(7)
        };
        private readonly Image _accessoryImage = new Image
        {
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        private readonly Polygon _documentBacking = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255)),
            Stroke = Brushes.Transparent,
            IsHitTestVisible = false
        };
        private readonly string _iconName;
        private readonly bool _isNormalAccessory;

        public PrototypeOverlayWindow(BitmapImage accessoryImage, string iconName, bool isNormalAccessory)
        {
            _iconName = iconName;
            _isNormalAccessory = isNormalAccessory;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            IsHitTestVisible = false;
            Content = _canvas;
            _accessoryImage.Source = accessoryImage;
            SourceInitialized += (_, _) => MakeClickThrough();
        }

        public void SetTarget(Rect iconBounds)
        {
            if (_isNormalAccessory)
            {
                SetNormalAccessoryTarget(iconBounds);
                return;
            }

            double accessoryHeight = Math.Max(18, iconBounds.Height * 0.5);
            double sourceAspectRatio = _accessoryImage.Source is BitmapImage source && source.PixelHeight > 0
                ? (double)source.PixelWidth / source.PixelHeight
                : 1;
            double accessoryWidth = accessoryHeight * sourceAspectRatio;
            const double attachmentOverlap = 2;
            const double outsideGap = 3;

            Width = iconBounds.Width + accessoryWidth + outsideGap + attachmentOverlap + 2;
            Height = Math.Max(iconBounds.Height + 4, accessoryHeight + 8);
            Left = iconBounds.Left;
            Top = iconBounds.Top - 2;

            _canvas.Width = Width;
            _canvas.Height = Height;
            _protectiveLayer.Width = iconBounds.Width;
            _protectiveLayer.Height = iconBounds.Height;
            Canvas.SetLeft(_protectiveLayer, 1);
            Canvas.SetTop(_protectiveLayer, 2);

            _accessoryImage.Width = accessoryWidth;
            _accessoryImage.Height = accessoryHeight;
            Canvas.SetLeft(_accessoryImage, iconBounds.Width - attachmentOverlap + outsideGap);
            Canvas.SetTop(_accessoryImage, 2);
            _canvas.Children.Clear();
            _canvas.Children.Add(_protectiveLayer);
            _canvas.Children.Add(_accessoryImage);
        }

        private void SetNormalAccessoryTarget(Rect iconBounds)
        {
            var placement = AccessoryPlacementGeometry.Calculate(
                iconBounds,
                (BitmapSource)_accessoryImage.Source,
                DesktopIconProbe.ReadIcons().Where(item => !string.Equals(item.Name, _iconName, StringComparison.OrdinalIgnoreCase)));

            Width = placement.WindowWidth;
            Height = placement.WindowHeight;
            Left = iconBounds.Left + placement.WindowOffsetX;
            Top = iconBounds.Top + placement.WindowOffsetY;
            _canvas.Width = Width;
            _canvas.Height = Height;

            _protectiveLayer.Width = placement.CasingRect.Width;
            _protectiveLayer.Height = placement.CasingRect.Height;
            Canvas.SetLeft(_protectiveLayer, placement.CasingRect.Left);
            Canvas.SetTop(_protectiveLayer, placement.CasingRect.Top);

            _accessoryImage.Width = placement.ImageRect.Width;
            _accessoryImage.Height = placement.ImageRect.Height;
            Canvas.SetLeft(_accessoryImage, placement.ImageRect.Left);
            Canvas.SetTop(_accessoryImage, placement.ImageRect.Top);

            double backingInsetX = placement.CasingRect.Width * 0.08;
            double backingInsetY = placement.CasingRect.Height * 0.06;
            double backingLeft = placement.CasingRect.Left + backingInsetX;
            double backingTop = placement.CasingRect.Top + backingInsetY;
            double backingRight = placement.CasingRect.Right - backingInsetX;
            double backingBottom = placement.CasingRect.Bottom - backingInsetY;
            double foldSize = Math.Min(placement.CasingRect.Width, placement.CasingRect.Height) * 0.12;
            _documentBacking.Points = new PointCollection
            {
                new Point(backingLeft, backingTop),
                new Point(backingRight - foldSize, backingTop),
                new Point(backingRight, backingTop + foldSize),
                new Point(backingRight, backingBottom),
                new Point(backingLeft, backingBottom)
            };

            _canvas.Children.Clear();
            _canvas.Children.Add(_documentBacking);
            _canvas.Children.Add(_protectiveLayer);
            _canvas.Children.Add(_accessoryImage);
        }

        private void MakeClickThrough()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
            NativeMethods.SetWindowLong(
                handle,
                NativeMethods.GwlExStyle,
                style | NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExNoActivate);
        }

        private static class NativeMethods
        {
            public const int GwlExStyle = -20;
            public const int WsExTransparent = 0x20;
            public const int WsExLayered = 0x80000;
            public const int WsExNoActivate = 0x8000000;

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern int GetWindowLong(IntPtr handle, int index);

            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern int SetWindowLong(IntPtr handle, int index, int value);
        }
    }
}
