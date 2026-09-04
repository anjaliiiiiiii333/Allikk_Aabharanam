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

            if (IsRibbonAccessory(sourcePath))
            {
                using Bitmap ribbonAccessory = source.Clone(region, PixelFormat.Format32bppArgb);
                RemoveRibbonPresentationBackground(ribbonAccessory);
                return CreateRibbonOverlay(ribbonAccessory, iconWidth, iconHeight);
            }

            if (IsMustacheAccessory(sourcePath))
            {
                return CreateMustacheOverlay(source, iconWidth, iconHeight);
            }

            // Extract and cleanly separate the physical keychain asset from the source composition
            using Bitmap accessory = ExtractAndCleanKeychain(source, sourcePath);

            // Scale the physical keychain independently (1.35x icon height) so clasp, chain, ring, and charms are prominent
            int accessoryHeight = Math.Max(40, (int)Math.Round(iconHeight * 1.35));
            int accessoryWidth = Math.Max(1, (int)Math.Round(
                accessoryHeight * (double)accessory.Width / accessory.Height));
            int claspOverlap = Math.Max(4, (int)Math.Round(iconWidth * 0.08));
            const int verticalMargin = 4;

            int overlayWidth = iconWidth - claspOverlap + accessoryWidth + 2;
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
                {
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.Clear(Color.Transparent);

                    graphics.DrawPath(casingPen, casingPath);
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

                int accessoryX = iconWidth - claspOverlap;
                int accessoryY = 0;
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

        private static Bitmap ExtractAndCleanKeychain(Bitmap source, string sourcePath)
        {
            Rectangle region = SelectAccessoryRegion(source, sourcePath);
            using Bitmap crop = source.Clone(region, PixelFormat.Format32bppArgb);
            int w = crop.Width;
            int h = crop.Height;

            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color p = crop.GetPixel(x, y);

                    // 1. Remove acrylic plate border on the left (below clasp level)
                    if (x < w * 0.16 && y > h * 0.12)
                    {
                        if (p.R < 140 && p.G < 140 && p.B < 140 && Math.Abs(p.R - p.G) < 14)
                        {
                            result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                            continue;
                        }
                    }

                    // 2. Remove dark presentation background everywhere (including inside loops, chains, and split rings)
                    if (p.R < 45 && p.G < 48 && p.B < 52)
                    {
                        result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                    }
                    else
                    {
                        int brightness = Math.Max(p.R, Math.Max(p.G, p.B));
                        if (brightness < 65)
                        {
                            int alpha = Math.Clamp((int)((brightness - 38) * 255.0 / 27.0), 0, 255);
                            result.SetPixel(x, y, Color.FromArgb(alpha, p.R, p.G, p.B));
                        }
                        else
                        {
                            result.SetPixel(x, y, p);
                        }
                    }
                }
            }

            return result;
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

        private static bool IsMustacheAccessory(string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            return name.Contains("mustache") || name.Contains("moustache");
        }

        private static Bitmap CreateMustacheOverlay(Bitmap source, int iconWidth, int iconHeight)
        {
            using Bitmap cleanedMustache = CleanMustacheAsset(source);

            var composite = new Bitmap(iconWidth, iconHeight, PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(composite))
            {
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.Clear(Color.Transparent);

                // 1. Extremely thin, subtle transparent protective layer around the exact icon-sized area
                Rectangle casingRect = new Rectangle(1, 1, Math.Max(1, iconWidth - 2), Math.Max(1, iconHeight - 2));
                int casingRadius = Math.Min(8, Math.Max(3, Math.Min(iconWidth, iconHeight) / 6));
                using (GraphicsPath casingPath = CreateRoundedRectanglePath(casingRect, casingRadius))
                using (var casingPen = new Pen(Color.FromArgb(60, 210, 235, 250), 1f))
                using (var casingFill = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                {
                    graphics.FillPath(casingFill, casingPath);
                    graphics.DrawPath(casingPen, casingPath);
                }

                // 2. The real Windows desktop icon underneath serves as the file appearance in the sandwich.
                // The 3D gloss moustache is positioned directly in the center of the icon space.
                int mustacheWidth = Math.Max(1, (int)Math.Round(iconWidth * 0.78));
                int mustacheHeight = Math.Max(1, (int)Math.Round(mustacheWidth * (double)cleanedMustache.Height / cleanedMustache.Width));
                int mustacheX = (iconWidth - mustacheWidth) / 2;
                int mustacheY = (iconHeight - mustacheHeight) / 2;

                graphics.DrawImage(cleanedMustache,
                    new Rectangle(mustacheX, mustacheY, mustacheWidth, mustacheHeight),
                    0, 0, cleanedMustache.Width, cleanedMustache.Height,
                    GraphicsUnit.Pixel);
            }

            return composite;
        }

        private static Bitmap CleanMustacheAsset(Bitmap source)
        {
            Rectangle region = new Rectangle(
                (int)(source.Width * 0.20),
                (int)(source.Height * 0.47),
                (int)(source.Width * 0.60),
                (int)(source.Height * 0.22));

            using Bitmap crop = source.Clone(region, PixelFormat.Format32bppArgb);
            int w = crop.Width;
            int h = crop.Height;

            Bitmap result = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color p = crop.GetPixel(x, y);

                    // On the curls / outer wings
                    if (x < w * 0.28 || x > w * 0.72)
                    {
                        // Only black curl pixels belong to the mustache
                        if (p.R < 48 && p.G < 48 && p.B < 48)
                        {
                            result.SetPixel(x, y, p);
                        }
                        else if (p.R < 65 && p.G < 65 && p.B < 65)
                        {
                            // Soft alpha blend at outer curl edge
                            int alpha = Math.Clamp((int)((65 - Math.Max(p.R, Math.Max(p.G, p.B))) * 255.0 / 17.0), 0, 255);
                            result.SetPixel(x, y, Color.FromArgb(alpha, p.R, p.G, p.B));
                        }
                        else
                        {
                            result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                        }
                    }
                    else
                    {
                        // Center lobes and bulbs
                        // Background is white paper (R > 165 && G > 165 && B > 165)
                        if (p.R > 165 && p.G > 165 && p.B > 165)
                        {
                            result.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));
                        }
                        else if (p.R > 135 && p.G > 135 && p.B > 135 && Math.Abs(p.R - p.G) < 15)
                        {
                            // Soft shadow on paper
                            int alpha = Math.Clamp((int)((165 - p.R) * 255.0 / 30.0), 0, 255);
                            result.SetPixel(x, y, Color.FromArgb(alpha, p.R, p.G, p.B));
                        }
                        else
                        {
                            // Mustache body + specular highlights on lobes
                            result.SetPixel(x, y, p);
                        }
                    }
                }
            }

            return result;
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
                return new Rectangle(source.Width * 56 / 100, source.Height * 16 / 100,
                    source.Width * 42 / 100, source.Height * 80 / 100);
            }

            if (name.Contains("transparent_silver_keychain"))
            {
                return new Rectangle(source.Width * 56 / 100, source.Height * 16 / 100,
                    source.Width * 42 / 100, source.Height * 80 / 100);
            }

            if (name.Contains("crochet_flower"))
            {
                return new Rectangle(source.Width * 56 / 100, source.Height * 16 / 100,
                    source.Width * 42 / 100, source.Height * 80 / 100);
            }

            if (name.Contains("pink_bow"))
            {
                return new Rectangle(source.Width * 20 / 100, source.Height * 30 / 100,
                    source.Width * 60 / 100, source.Height * 55 / 100);
            }

            if (name.Contains("mustache") || name.Contains("moustache"))
            {
                return new Rectangle(source.Width * 18 / 100, source.Height * 43 / 100,
                    source.Width * 64 / 100, source.Height * 32 / 100);
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
