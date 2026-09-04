using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace AccessoryPrototype
{
    public static class AccessoryRenderer
    {
        public static BitmapImage RenderReferenceAccessory(string sourcePath)
        {
            using var source = new Bitmap(sourcePath);
            Rectangle region = SelectAccessoryRegion(source, sourcePath);
            using Bitmap selected = source.Clone(region, PixelFormat.Format32bppArgb);
            RemovePresentationBackground(selected, UsesLightObjectBackground(sourcePath));
            using var output = new MemoryStream();
            selected.Save(output, ImageFormat.Png);
            output.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = output;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static Rectangle SelectAccessoryRegion(Bitmap source, string sourcePath)
        {
            string name = Path.GetFileName(sourcePath).ToLowerInvariant();
            if (name.Contains("desktop_accessory_keychain") || name.Contains("desktop_icon_keychain"))
            {
                return new Rectangle(
                    source.Width * 55 / 100,
                    source.Height * 12 / 100,
                    source.Width * 45 / 100,
                    source.Height * 88 / 100);
            }

            if (name.Contains("transparent_silver_keychain"))
            {
                return new Rectangle(
                    source.Width * 55 / 100,
                    source.Height * 10 / 100,
                    source.Width * 45 / 100,
                    source.Height * 90 / 100);
            }

            if (name.Contains("pink_bow"))
            {
                return new Rectangle(
                    source.Width * 20 / 100,
                    source.Height * 30 / 100,
                    source.Width * 60 / 100,
                    source.Height * 55 / 100);
            }

            if (name.Contains("mustache"))
            {
                return new Rectangle(
                    source.Width * 18 / 100,
                    source.Height * 43 / 100,
                    source.Width * 64 / 100,
                    source.Height * 32 / 100);
            }

            if (name.Contains("crochet_flower"))
            {
                return new Rectangle(
                    source.Width * 54 / 100,
                    source.Height * 42 / 100,
                    source.Width * 46 / 100,
                    source.Height * 58 / 100);
            }

            throw new InvalidOperationException(
                "This reference layout has no safe accessory extraction region: " + Path.GetFileName(sourcePath));
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
