using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AccessoryPrototype
{
    public sealed class AccessoryPlacement
    {
        public double WindowOffsetX { get; init; }
        public double WindowOffsetY { get; init; }
        public double WindowWidth { get; init; }
        public double WindowHeight { get; init; }
        public Rect CasingRect { get; init; }
        public Rect ImageRect { get; init; }
        public Rect VisibleImageRect { get; init; }
    }

    public static class AccessoryPlacementGeometry
    {
        private const double VisibleWidthRatio = 0.86;
        private const double VisibleHeightRatio = 0.86;
        private const double VisualCorrectionRatio = 0.03;
        private const double NeighborOverlapRatio = 0.15;

        public static AccessoryPlacement Calculate(
            Rect iconBounds,
            BitmapSource accessoryImage,
            IEnumerable<DesktopIconInfo> nearbyIcons)
        {
            Rect visiblePixels = FindVisiblePixelBounds(accessoryImage);
            double visibleWidth = Math.Max(1, visiblePixels.Width);
            double visibleHeight = Math.Max(1, visiblePixels.Height);
            double scale = Math.Min(
                iconBounds.Width * VisibleWidthRatio / visibleWidth,
                iconBounds.Height * VisibleHeightRatio / visibleHeight);

            double imageWidth = accessoryImage.PixelWidth * scale;
            double imageHeight = accessoryImage.PixelHeight * scale;
            double visibleCenterX = (visiblePixels.Left + visiblePixels.Width / 2) * scale;
            double visibleCenterY = (visiblePixels.Top + visiblePixels.Height / 2) * scale;
            double iconCenterX = iconBounds.Width / 2;
            double iconCenterY = iconBounds.Height / 2;
            double correctionX = iconBounds.Width * VisualCorrectionRatio;
            double correctionY = -iconBounds.Height * VisualCorrectionRatio;

            var candidates = new[]
            {
                new Vector(correctionX, correctionY),
                new Vector(-correctionX, correctionY),
                new Vector(correctionX, 0),
                new Vector(-correctionX, 0),
                new Vector(0, iconBounds.Height * 0.06),
                new Vector(0, -iconBounds.Height * 0.06)
            };

            Vector selectedCorrection = candidates
                .OrderBy(candidate => CalculateNeighborOverlap(
                    new Rect(
                        iconBounds.Left + iconCenterX - visibleCenterX + candidate.X,
                        iconBounds.Top + iconCenterY - visibleCenterY + candidate.Y,
                        visibleWidth * scale,
                        visibleHeight * scale),
                    nearbyIcons,
                    iconBounds))
                .First();

            double imageLeft = iconCenterX - visibleCenterX + selectedCorrection.X;
            double imageTop = iconCenterY - visibleCenterY + selectedCorrection.Y;
            double windowOffsetX = Math.Min(0, imageLeft - 2);
            double windowOffsetY = Math.Min(0, imageTop - 2);
            double localImageLeft = imageLeft - windowOffsetX;
            double localImageTop = imageTop - windowOffsetY;
            double localCasingLeft = 1 - windowOffsetX;
            double localCasingTop = 1 - windowOffsetY;
            double windowWidth = Math.Max(
                iconBounds.Width - windowOffsetX + 2,
                localImageLeft + imageWidth + 2);
            double windowHeight = Math.Max(
                iconBounds.Height - windowOffsetY + 2,
                localImageTop + imageHeight + 2);

            return new AccessoryPlacement
            {
                WindowOffsetX = windowOffsetX,
                WindowOffsetY = windowOffsetY,
                WindowWidth = windowWidth,
                WindowHeight = windowHeight,
                CasingRect = new Rect(localCasingLeft, localCasingTop, iconBounds.Width, iconBounds.Height),
                ImageRect = new Rect(localImageLeft, localImageTop, imageWidth, imageHeight),
                VisibleImageRect = new Rect(
                    localImageLeft + visiblePixels.Left * scale,
                    localImageTop + visiblePixels.Top * scale,
                    visibleWidth * scale,
                    visibleHeight * scale)
            };
        }

        private static double CalculateNeighborOverlap(
            Rect proposedAccessory,
            IEnumerable<DesktopIconInfo> nearbyIcons,
            Rect selectedIcon)
        {
            double accessoryArea = Math.Max(1, proposedAccessory.Width * proposedAccessory.Height);
            double overlap = 0;
            foreach (DesktopIconInfo nearbyIcon in nearbyIcons ?? Enumerable.Empty<DesktopIconInfo>())
            {
                if (nearbyIcon.Bounds == selectedIcon)
                    continue;

                Rect intersection = Rect.Intersect(proposedAccessory, nearbyIcon.Bounds);
                if (!intersection.IsEmpty)
                    overlap += intersection.Width * intersection.Height;
            }

            return overlap / accessoryArea;
        }

        private static Rect FindVisiblePixelBounds(BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            int left = width;
            int top = height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte alpha = pixels[y * stride + x * 4 + 3];
                    if (alpha <= 8)
                        continue;

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < left
                ? new Rect(0, 0, width, height)
                : new Rect(left, top, right - left + 1, bottom - top + 1);
        }
    }
}
