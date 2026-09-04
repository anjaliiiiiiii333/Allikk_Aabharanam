using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Builds the desktop overlay bitmap in memory from a read-only dataset reference.
    /// The source image is never written or changed on disk.
    /// </summary>
    public static class DatasetAccessoryRenderer
    {
        private const double CenterAccessoryWidthRatio = 0.9;
        private const double CenterAccessoryHeightRatio = 0.9;

        public static Bitmap CreateOverlayBitmap(
            string sourcePath,
            int iconWidth,
            int iconHeight,
            IEnumerable<Rectangle> neighboringIcons = null)
        {
            if (iconWidth <= 0 || iconHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(iconWidth));

            using var source = new Bitmap(sourcePath);
            Rectangle region = SelectAccessoryRegion(source, sourcePath);
            using Bitmap accessory = source.Clone(region, PixelFormat.Format32bppArgb);
            if (IsRibbonAccessory(sourcePath))
            {
                RemoveRibbonPresentationBackground(accessory);
                return CreateRibbonOverlay(accessory, iconWidth, iconHeight);
            }

            RemovePresentationBackground(accessory, UsesLightObjectBackground(sourcePath));

            int accessoryHeight = Math.Max(22, (int)Math.Round(iconHeight * 0.65));
            int accessoryWidth = Math.Max(1, (int)Math.Round(
                accessoryHeight * (double)accessory.Width / accessory.Height));
            const int attachmentOverlap = 12;
            const int outsideGap = 1;
            const int verticalMargin = 4;

            int overlayWidth = iconWidth + outsideGap + accessoryWidth + 2;
            int overlayHeight = Math.Max(iconHeight + verticalMargin * 2, accessoryHeight + verticalMargin * 2);
            var composite = new Bitmap(overlayWidth, overlayHeight, PixelFormat.Format32bppPArgb);

            bool centerMounted = IsCenterMountedAccessory(sourcePath);
            if (centerMounted)
            {
                AccessoryPlacement placement = AccessoryPlacementGeometry.CalculateNormalPlacement(
                    accessory,
                    iconWidth,
                    iconHeight,
                    neighboringIcons);
                accessoryWidth = placement.Width;
                accessoryHeight = placement.Height;

                overlayWidth = iconWidth + 2;
                overlayHeight = Math.Max(iconHeight + verticalMargin * 2, accessoryHeight + verticalMargin * 2);
                composite.Dispose();
                composite = new Bitmap(overlayWidth, overlayHeight, PixelFormat.Format32bppPArgb);

                using (Graphics graphics = Graphics.FromImage(composite))
                using (GraphicsPath casingPath = CreateRoundedRectanglePath(
                    new Rectangle(1, 1, Math.Max(1, iconWidth - 2), Math.Max(1, iconHeight - 2)),
                    Math.Min(7, Math.Max(1, Math.Min(iconWidth, iconHeight) / 5))))
                using (var casingPen = new Pen(Color.FromArgb(82, 190, 225, 240), 1f))
                using (var documentBrush = new SolidBrush(Color.FromArgb(72, 255, 255, 255)))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawPath(casingPen, casingPath);
                    DrawDocumentBacking(graphics, iconWidth, iconHeight, documentBrush);
                    graphics.DrawImage(accessory,
                        new Rectangle(placement.Left, placement.Top, accessoryWidth, accessoryHeight),
                        0, 0, accessory.Width, accessory.Height, GraphicsUnit.Pixel);
                }

                return composite;
            }

            using (Graphics graphics = Graphics.FromImage(composite))
            using (GraphicsPath casingPath = CreateRoundedRectanglePath(
                new Rectangle(1, 0, Math.Max(1, iconWidth - 2), Math.Max(1, iconHeight + verticalMargin)),
                Math.Min(7, Math.Max(1, Math.Min(iconWidth, iconHeight) / 5))))
            using (var casingPen = new Pen(Color.FromArgb(82, 190, 225, 240), 1f))
            {
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);

                graphics.DrawPath(casingPen, casingPath);

                int accessoryX = centerMounted
                    ? Math.Max(0, (iconWidth - accessoryWidth) / 2)
                    : iconWidth - attachmentOverlap + outsideGap;
                int accessoryY = centerMounted
                    ? Math.Max(0, (iconHeight - accessoryHeight) / 2)
                    : Math.Max(2, (iconHeight - accessoryHeight) / 2);
                graphics.DrawImage(
                    accessory,
                    new Rectangle(accessoryX, accessoryY, accessoryWidth, accessoryHeight),
                    0,
                    0,
                    accessory.Width,
                    accessory.Height,
                    GraphicsUnit.Pixel);
            }

            return composite;
        }

        private static void DrawDocumentBacking(Graphics graphics, int iconWidth, int iconHeight, Brush brush)
        {
            int insetX = Math.Max(2, (int)Math.Round(iconWidth * 0.08));
            int insetY = Math.Max(2, (int)Math.Round(iconHeight * 0.06));
            int left = insetX;
            int top = insetY;
            int right = iconWidth - insetX;
            int bottom = iconHeight - insetY;
            int fold = Math.Max(3, (int)Math.Round(Math.Min(iconWidth, iconHeight) * 0.12));
            Point[] points =
            {
                new Point(left, top),
                new Point(right - fold, top),
                new Point(right, top + fold),
                new Point(right, bottom),
                new Point(left, bottom)
            };
            graphics.FillPolygon(brush, points);
        }

        private static Bitmap CreateRibbonOverlay(Bitmap ribbon, int iconWidth, int iconHeight)
        {
            double aspectRatio = (double)ribbon.Width / ribbon.Height;
            int ribbonWidth = Math.Max(1, (int)Math.Round(iconWidth * 0.7));
            int ribbonHeight = Math.Max(1, (int)Math.Round(ribbonWidth / aspectRatio));
            if (ribbonHeight > iconHeight * 0.7)
            {
                ribbonHeight = Math.Max(1, (int)Math.Round(iconHeight * 0.7));
                ribbonWidth = Math.Max(1, (int)Math.Round(ribbonHeight * aspectRatio));
            }

            int overlayWidth = iconWidth + Math.Max(4, ribbonWidth / 8);
            int overlayHeight = Math.Max(iconHeight + 8, ribbonHeight + 8);
            var composite = new Bitmap(overlayWidth, overlayHeight, PixelFormat.Format32bppPArgb);
            double ribbonCenterX = iconWidth - ribbonWidth * 0.35;
            double ribbonCenterY = iconHeight * 0.23;

            using (Graphics graphics = Graphics.FromImage(composite))
            using (GraphicsPath casingPath = CreateRoundedRectanglePath(
                new Rectangle(1, 1, Math.Max(1, iconWidth - 2), Math.Max(1, iconHeight - 2)),
                Math.Min(7, Math.Max(1, Math.Min(iconWidth, iconHeight) / 5))))
            using (var casingPen = new Pen(Color.FromArgb(82, 190, 225, 240), 1f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);
                graphics.DrawPath(casingPen, casingPath);
                DrawRotatedImage(graphics, ribbon, ribbonCenterX, ribbonCenterY,
                    ribbonWidth, ribbonHeight, 12.0f);
            }

            return composite;
        }

        private static void DrawRotatedImage(
            Graphics graphics,
            Bitmap image,
            double centerX,
            double centerY,
            int width,
            int height,
            float angle)
        {
            GraphicsState state = graphics.Save();
            graphics.TranslateTransform((float)centerX, (float)centerY);
            graphics.RotateTransform(angle);
            graphics.DrawImage(
                image,
                new Rectangle(-width / 2, -height / 2, width, height),
                0,
                0,
                image.Width,
                image.Height,
                GraphicsUnit.Pixel);
            graphics.Restore(state);
        }

        private static bool IsCenterMountedAccessory(string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            return name.Contains("ribbon") ||
                   name.Contains("sunglasses") ||
                   name.Contains("mustache") ||
                   name.Contains("moustache") ||
                   name.Contains("bow") ||
                   name.Contains("hat") ||
                   name.Contains("pin") ||
                   name.Contains("sticker") ||
                   name.Contains("badge");
        }

        private static bool IsRibbonAccessory(string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            return name.Contains("ribbon") || name.Contains("bow");
        }

        private static void RemoveRibbonPresentationBackground(Bitmap bitmap)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    if (!IsRibbonPixel(pixel))
                    {
                        bitmap.SetPixel(x, y, Color.FromArgb(0, pixel.R, pixel.G, pixel.B));
                    }
                }
            }
        }

        private static bool IsRibbonPixel(Color pixel)
        {
            // The ribbon is saturated pink; the paper, shadows, and scene background
            // are neutral or dark and must not survive into the overlay bitmap.
            int redOverGreen = pixel.R - pixel.G;
            int blueOverGreen = pixel.B - pixel.G;
            return pixel.R >= 100 && redOverGreen >= 18 && blueOverGreen >= 5;
        }

        private static Rectangle SelectAccessoryRegion(Bitmap source, string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            if (name.Contains("desktop_accessory_keychain") || name.Contains("desktop_icon_keychain"))
            {
                return new Rectangle(source.Width * 55 / 100, source.Height * 12 / 100,
                    source.Width * 45 / 100, source.Height * 88 / 100);
            }

            if (name.Contains("transparent_silver_keychain"))
            {
                return new Rectangle(source.Width * 55 / 100, source.Height * 10 / 100,
                    source.Width * 45 / 100, source.Height * 90 / 100);
            }

            if (name.Contains("pink_bow"))
            {
                return new Rectangle(source.Width * 20 / 100, source.Height * 30 / 100,
                    source.Width * 60 / 100, source.Height * 55 / 100);
            }

            if (name.Contains("mustache"))
            {
                return new Rectangle(source.Width * 18 / 100, source.Height * 43 / 100,
                    source.Width * 64 / 100, source.Height * 32 / 100);
            }

            if (name.Contains("crochet_flower"))
            {
                return new Rectangle(source.Width * 54 / 100, source.Height * 42 / 100,
                    source.Width * 46 / 100, source.Height * 58 / 100);
            }

            throw new InvalidOperationException(
                "This dataset reference has no safe accessory extraction region: " + Path.GetFileName(sourcePath));
        }

        private static bool UsesLightObjectBackground(string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            return name.Contains("pink_bow") || name.Contains("mustache");
        }

        private static void RemovePresentationBackground(Bitmap bitmap, bool lightObjectBackground)
        {
            Color background = AverageCorners(bitmap);
            var queue = new Queue<Point>();
            var visited = new bool[bitmap.Width, bitmap.Height];

            if (lightObjectBackground)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (IsNeutralLightPresentationPixel(pixel))
                            bitmap.SetPixel(x, y, Color.FromArgb(0, pixel.R, pixel.G, pixel.B));
                    }
                }
            }

            for (int x = 0; x < bitmap.Width; x++)
            {
                Enqueue(x, 0);
                Enqueue(x, bitmap.Height - 1);
            }
            for (int y = 0; y < bitmap.Height; y++)
            {
                Enqueue(0, y);
                Enqueue(bitmap.Width - 1, y);
            }

            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                Color pixel = bitmap.GetPixel(point.X, point.Y);
                if (ColorDistance(pixel, background) > 72)
                    continue;

                bitmap.SetPixel(point.X, point.Y, Color.FromArgb(0, pixel.R, pixel.G, pixel.B));
                Enqueue(point.X - 1, point.Y);
                Enqueue(point.X + 1, point.Y);
                Enqueue(point.X, point.Y - 1);
                Enqueue(point.X, point.Y + 1);
            }

            void Enqueue(int x, int y)
            {
                if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height || visited[x, y])
                    return;
                visited[x, y] = true;
                queue.Enqueue(new Point(x, y));
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color AverageCorners(Bitmap bitmap)
        {
            Color[] corners =
            {
                bitmap.GetPixel(0, 0),
                bitmap.GetPixel(bitmap.Width - 1, 0),
                bitmap.GetPixel(0, bitmap.Height - 1),
                bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1)
            };
            return Color.FromArgb(
                (corners[0].R + corners[1].R + corners[2].R + corners[3].R) / 4,
                (corners[0].G + corners[1].G + corners[2].G + corners[3].G) / 4,
                (corners[0].B + corners[1].B + corners[2].B + corners[3].B) / 4);
        }

        private static int ColorDistance(Color first, Color second)
        {
            int red = first.R - second.R;
            int green = first.G - second.G;
            int blue = first.B - second.B;
            return (int)Math.Sqrt(red * red + green * green + blue * blue);
        }

        private static bool IsNeutralLightPresentationPixel(Color pixel)
        {
            int spread = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) -
                         Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
            return pixel.R > 220 && pixel.G > 220 && pixel.B > 220 && spread < 35;
        }
    }
}
