using System;
using System.Drawing;
using System.IO;

namespace DesktopKeychainApp
{
    /// <summary>
    /// Utility to generate the keychain PNG asset if it doesn't exist.
    /// Creates a simple, visually distinct keychain icon.
    /// </summary>
    public static class AssetGenerator
    {
        /// <summary>
        /// Generates a keychain PNG asset with a ring and key shape.
        /// Saves it to the specified path with transparency support.
        /// </summary>
        public static void GenerateKeychainAsset(string outputPath)
        {
            // Create output directory if it doesn't exist
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // If the file already exists, don't overwrite it
            if (File.Exists(outputPath))
            {
                return;
            }

            // Create a 64x64 bitmap with transparency
            using (Bitmap bitmap = new Bitmap(64, 64))
            {
                // Make the bitmap transparent
                bitmap.MakeTransparent();

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // Draw the keychain ring (outer circle)
                    Pen ringPen = new Pen(Color.FromArgb(255, 214, 120), 4); // Gold color
                    g.DrawEllipse(ringPen, 8, 8, 30, 30);
                    ringPen.Dispose();

                    // Draw the key shaft (rectangle)
                    Brush keyShaftBrush = new SolidBrush(Color.FromArgb(218, 165, 32)); // Darker gold
                    g.FillRectangle(keyShaftBrush, 38, 18, 18, 8);
                    keyShaftBrush.Dispose();

                    // Draw the key bow (top circle of the key)
                    Brush keyBowBrush = new SolidBrush(Color.FromArgb(255, 215, 0)); // Bright gold
                    g.FillEllipse(keyBowBrush, 36, 10, 14, 14);
                    keyBowBrush.Dispose();

                    // Draw the key teeth (small rectangles at the bottom of the key)
                    Brush keyTeethBrush = new SolidBrush(Color.FromArgb(218, 165, 32));
                    g.FillRectangle(keyTeethBrush, 38, 24, 4, 4);
                    g.FillRectangle(keyTeethBrush, 46, 24, 4, 4);
                    keyTeethBrush.Dispose();

                    // Add a small highlight for depth
                    Pen highlightPen = new Pen(Color.FromArgb(255, 255, 200), 1);
                    g.DrawArc(highlightPen, 10, 10, 26, 26, 45, 90);
                    highlightPen.Dispose();
                }

                // Save as PNG with transparency
                bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            Console.WriteLine($"Generated keychain asset at: {outputPath}");
        }
    }
}
