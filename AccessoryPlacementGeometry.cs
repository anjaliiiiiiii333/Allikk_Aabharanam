using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DesktopKeychainApp
{
    internal sealed class AccessoryPlacement
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int Left { get; init; }
        public int Top { get; init; }
        public Rectangle VisibleBounds { get; init; }
    }

    internal static class AccessoryPlacementGeometry
    {
        private const double VisibleWidthRatio = 0.86;
        private const double VisibleHeightRatio = 0.86;
        private const double VisualCorrectionRatio = 0.03;

        public static AccessoryPlacement CalculateNormalPlacement(
            Bitmap accessory,
            int iconWidth,
            int iconHeight,
            IEnumerable<Rectangle> neighboringIcons)
        {
            Rectangle visibleBounds = FindVisibleBounds(accessory);
            double visibleWidth = Math.Max(1, visibleBounds.Width);
            double visibleHeight = Math.Max(1, visibleBounds.Height);
            double scale = Math.Min(
                iconWidth * VisibleWidthRatio / visibleWidth,
                iconHeight * VisibleHeightRatio / visibleHeight);
            int width = Math.Max(1, (int)Math.Round(accessory.Width * scale));
            int height = Math.Max(1, (int)Math.Round(accessory.Height * scale));
            double centerX = iconWidth / 2.0;
            double centerY = iconHeight / 2.0;
            double visibleCenterX = (visibleBounds.Left + visibleWidth / 2) * scale;
            double visibleCenterY = (visibleBounds.Top + visibleHeight / 2) * scale;
            double correctionX = iconWidth * VisualCorrectionRatio;
            double correctionY = -iconHeight * VisualCorrectionRatio;

            var candidates = new[]
            {
                new PointF((float)correctionX, (float)correctionY),
                new PointF((float)-correctionX, (float)correctionY),
                new PointF((float)correctionX, 0),
                new PointF((float)-correctionX, 0),
                new PointF(0, (float)(iconHeight * 0.06)),
                new PointF(0, (float)(-iconHeight * 0.06))
            };
            PointF correction = candidates
                .OrderBy(candidate => OverlapRatio(
                    new RectangleF(
                        (float)(centerX - visibleCenterX + candidate.X),
                        (float)(centerY - visibleCenterY + candidate.Y),
                        (float)(visibleWidth * scale),
                        (float)(visibleHeight * scale)),
                    neighboringIcons))
                .First();

            int left = (int)Math.Round(centerX - width / 2.0 + correction.X);
            int top = (int)Math.Round(centerY - height / 2.0 + correction.Y);
            return new AccessoryPlacement
            {
                Width = width,
                Height = height,
                Left = left,
                Top = top,
                VisibleBounds = new Rectangle(
                    left + (int)Math.Round(visibleBounds.Left * scale),
                    top + (int)Math.Round(visibleBounds.Top * scale),
                    Math.Max(1, (int)Math.Round(visibleWidth * scale)),
                    Math.Max(1, (int)Math.Round(visibleHeight * scale)))
            };
        }

        private static double OverlapRatio(RectangleF accessory, IEnumerable<Rectangle> neighbors)
        {
            double area = Math.Max(1, accessory.Width * accessory.Height);
            double overlap = 0;
            foreach (Rectangle neighbor in neighbors ?? Enumerable.Empty<Rectangle>())
            {
                RectangleF intersection = RectangleF.Intersect(accessory, neighbor);
                if (!intersection.IsEmpty)
                    overlap += intersection.Width * intersection.Height;
            }

            return overlap / area;
        }

        private static Rectangle FindVisibleBounds(Bitmap bitmap)
        {
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).A <= 8)
                        continue;

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < left
                ? new Rectangle(0, 0, bitmap.Width, bitmap.Height)
                : new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
    }
}
