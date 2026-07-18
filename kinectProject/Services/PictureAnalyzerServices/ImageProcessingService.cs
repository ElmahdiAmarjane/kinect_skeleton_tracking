using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace kinectProject
{
    public class ImageProcessingService
    {
        #region Color Conversion

        /// <summary>
        /// Convert RGB color to HSV color space
        /// </summary>
        public HsvColor RgbToHsv(Color rgb)
        {
            float r = rgb.R / 255f;
            float g = rgb.G / 255f;
            float b = rgb.B / 255f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0;
            float s = (max == 0) ? 0 : delta / max;
            float v = max;

            if (delta != 0)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60 * (((b - r) / delta) + 2);
                else
                    h = 60 * (((r - g) / delta) + 4);
            }

            if (h < 0) h += 360;

            return new HsvColor(h, s, v);
        }

        /// <summary>
        /// Convert HSV color to RGB color
        /// </summary>
        public Color HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            float m = v - c;

            float r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }

        /// <summary>
        /// Get Color from PointColor enum
        /// </summary>
        public Color GetColorFromEnum(PointColor color, Color customColor)
        {
            switch (color)
            {
                case PointColor.Red: return Color.Red;
                case PointColor.Green: return Color.Green;
                case PointColor.Blue: return Color.Blue;
                case PointColor.Yellow: return Color.Yellow;
                case PointColor.White: return Color.White;
                case PointColor.Custom: return customColor;
                default: return Color.Red;
            }
        }

        #endregion

        #region Color Comparison

        /// <summary>
        /// Check if two colors are similar within RGB tolerance
        /// </summary>
        public bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            int rDiff = c1.R - c2.R;
            int gDiff = c1.G - c2.G;
            int bDiff = c1.B - c2.B;

            double distance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
            return distance <= tolerance;
        }

        /// <summary>
        /// Check if two colors are similar using HSV comparison
        /// </summary>
        public bool IsColorSimilarHSV(Color c1, Color c2, float hueTolerance, float satTolerance, float valTolerance)
        {
            HsvColor hsv1 = RgbToHsv(c1);
            HsvColor hsv2 = RgbToHsv(c2);

            float hueDiff = Math.Abs(hsv1.H - hsv2.H);
            hueDiff = Math.Min(hueDiff, 360 - hueDiff);

            return hueDiff <= hueTolerance &&
                   Math.Abs(hsv1.S - hsv2.S) <= satTolerance &&
                   Math.Abs(hsv1.V - hsv2.V) <= valTolerance;
        }

        /// <summary>
        /// Calculate Euclidean color distance between two colors
        /// </summary>
        public double GetColorDistance(Color c1, Color c2)
        {
            int rDiff = c1.R - c2.R;
            int gDiff = c1.G - c2.G;
            int bDiff = c1.B - c2.B;

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        /// <summary>
        /// Calculate perceptually weighted color distance
        /// </summary>
        public double GetWeightedColorDistance(Color c1, Color c2)
        {
            // Weighted to account for human perception
            double rMean = (c1.R + c2.R) / 2.0;
            double rDiff = c1.R - c2.R;
            double gDiff = c1.G - c2.G;
            double bDiff = c1.B - c2.B;

            double weightR = 2 + rMean / 256.0;
            double weightG = 4.0;
            double weightB = 2 + (255 - rMean) / 256.0;

            return Math.Sqrt(
                weightR * rDiff * rDiff +
                weightG * gDiff * gDiff +
                weightB * bDiff * bDiff);
        }

        #endregion

        #region Image Adjustments

        /// <summary>
        /// Apply brightness and contrast adjustments to an image
        /// </summary>
        public Bitmap ApplyImageAdjustments(Bitmap source, int brightness, int contrast)
        {
            Bitmap adjusted = new Bitmap(source.Width, source.Height);

            float brightnessFactor = brightness / 100.0f;
            float contrastFactor = (contrast + 100) / 100.0f;
            contrastFactor *= contrastFactor; // Square for more noticeable effect

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);

                    // Apply brightness
                    int r = (int)(pixel.R + (brightnessFactor * 255));
                    int g = (int)(pixel.G + (brightnessFactor * 255));
                    int b = (int)(pixel.B + (brightnessFactor * 255));

                    // Apply contrast
                    r = (int)(((r / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);
                    g = (int)(((g / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);
                    b = (int)(((b / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);

                    // Clamp values
                    r = Math.Max(0, Math.Min(255, r));
                    g = Math.Max(0, Math.Min(255, g));
                    b = Math.Max(0, Math.Min(255, b));

                    adjusted.SetPixel(x, y, Color.FromArgb(pixel.A, r, g, b));
                }
            }

            return adjusted;
        }

        /// <summary>
        /// Create a grayscale version of an image
        /// </summary>
        public Bitmap ConvertToGrayscale(Bitmap source)
        {
            Bitmap grayscale = new Bitmap(source.Width, source.Height);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    grayscale.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
                }
            }

            return grayscale;
        }

        /// <summary>
        /// Apply a color mask to highlight specific colors
        /// </summary>
        public Bitmap ApplyColorMask(Bitmap source, Color targetColor, int tolerance)
        {
            Bitmap masked = new Bitmap(source.Width, source.Height);
            HsvColor targetHsv = RgbToHsv(targetColor);
            float hueTolerance = tolerance;

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    HsvColor pixelHsv = RgbToHsv(pixel);

                    float hueDiff = Math.Abs(pixelHsv.H - targetHsv.H);
                    hueDiff = Math.Min(hueDiff, 360 - hueDiff);

                    if (hueDiff <= hueTolerance &&
                        Math.Abs(pixelHsv.S - targetHsv.S) < 0.3f &&
                        Math.Abs(pixelHsv.V - targetHsv.V) < 0.3f)
                    {
                        // Highlight matched pixels
                        masked.SetPixel(x, y, Color.Red);
                    }
                    else
                    {
                        // Darken non-matched pixels
                        masked.SetPixel(x, y, Color.FromArgb(
                            pixel.R / 3,
                            pixel.G / 3,
                            pixel.B / 3));
                    }
                }
            }

            return masked;
        }

        /// <summary>
        /// Resize an image while maintaining aspect ratio
        /// </summary>
        public Bitmap ResizeImage(Bitmap source, int maxWidth, int maxHeight)
        {
            // Calculate new dimensions
            float ratioX = (float)maxWidth / source.Width;
            float ratioY = (float)maxHeight / source.Height;
            float ratio = Math.Min(ratioX, ratioY);

            int newWidth = (int)(source.Width * ratio);
            int newHeight = (int)(source.Height * ratio);

            Bitmap resized = new Bitmap(newWidth, newHeight);

            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, newWidth, newHeight);
            }

            return resized;
        }

        /// <summary>
        /// Crop an image to a rectangle
        /// </summary>
        public Bitmap CropImage(Bitmap source, Rectangle cropArea)
        {
            // Ensure crop area is within image bounds
            cropArea.Intersect(new Rectangle(0, 0, source.Width, source.Height));

            if (cropArea.Width <= 0 || cropArea.Height <= 0)
                return new Bitmap(source);

            Bitmap cropped = new Bitmap(cropArea.Width, cropArea.Height);

            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.DrawImage(source,
                    new Rectangle(0, 0, cropArea.Width, cropArea.Height),
                    cropArea,
                    GraphicsUnit.Pixel);
            }

            return cropped;
        }

        #endregion

        #region Color Sampling

        /// <summary>
        /// Pick color from an image at a specific point
        /// </summary>
        public Color PickColorFromImage(Bitmap image, Point location)
        {
            if (image == null)
                return Color.Black;

            if (location.X < 0 || location.X >= image.Width ||
                location.Y < 0 || location.Y >= image.Height)
                return Color.Black;

            return image.GetPixel(location.X, location.Y);
        }

        /// <summary>
        /// Get average color from a region of an image
        /// </summary>
        public Color GetAverageColor(Bitmap image, Rectangle region)
        {
            if (image == null) return Color.Black;

            // Ensure region is within image bounds
            region.Intersect(new Rectangle(0, 0, image.Width, image.Height));

            if (region.Width <= 0 || region.Height <= 0)
                return Color.Black;

            long totalR = 0, totalG = 0, totalB = 0;
            int pixelCount = region.Width * region.Height;

            for (int y = region.Y; y < region.Y + region.Height; y++)
            {
                for (int x = region.X; x < region.X + region.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                }
            }

            return Color.FromArgb(
                (int)(totalR / pixelCount),
                (int)(totalG / pixelCount),
                (int)(totalB / pixelCount));
        }

        /// <summary>
        /// Get dominant color from a region using histogram
        /// </summary>
        public Color GetDominantColor(Bitmap image, Rectangle region)
        {
            if (image == null) return Color.Black;

            region.Intersect(new Rectangle(0, 0, image.Width, image.Height));

            if (region.Width <= 0 || region.Height <= 0)
                return Color.Black;

            // Simple histogram (reduced color space)
            Dictionary<int, int> colorHistogram = new Dictionary<int, int>();

            for (int y = region.Y; y < region.Y + region.Height; y++)
            {
                for (int x = region.X; x < region.X + region.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    // Quantize color to reduce histogram size
                    int r = (pixel.R / 32) * 32;
                    int g = (pixel.G / 32) * 32;
                    int b = (pixel.B / 32) * 32;

                    int colorKey = (r << 16) | (g << 8) | b;

                    if (colorHistogram.ContainsKey(colorKey))
                        colorHistogram[colorKey]++;
                    else
                        colorHistogram[colorKey] = 1;
                }
            }

            // Find the most frequent color
            int dominantKey = colorHistogram
                                 .OrderByDescending(kvp => kvp.Value)
                                 .First()
                                 .Key;

            int domR = (dominantKey >> 16) & 0xFF;
            int domG = (dominantKey >> 8) & 0xFF;
            int domB = dominantKey & 0xFF;

            return Color.FromArgb(domR, domG, domB);
        }

        #endregion

        #region Image Analysis

        /// <summary>
        /// Calculate image brightness (0-255)
        /// </summary>
        public double GetImageBrightness(Bitmap image)
        {
            if (image == null) return 0;

            long totalBrightness = 0;
            int sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 50);

            int sampleCount = 0;
            for (int y = 0; y < image.Height; y += sampleStep)
            {
                for (int x = 0; x < image.Width; x += sampleStep)
                {
                    Color pixel = image.GetPixel(x, y);
                    totalBrightness += (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    sampleCount++;
                }
            }

            return sampleCount > 0 ? (double)totalBrightness / sampleCount : 0;
        }

        /// <summary>
        /// Calculate image contrast
        /// </summary>
        public double GetImageContrast(Bitmap image)
        {
            if (image == null) return 0;

            List<int> luminanceValues = new List<int>();
            int sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 50);

            for (int y = 0; y < image.Height; y += sampleStep)
            {
                for (int x = 0; x < image.Width; x += sampleStep)
                {
                    Color pixel = image.GetPixel(x, y);
                    int luminance = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    luminanceValues.Add(luminance);
                }
            }

            if (luminanceValues.Count == 0) return 0;

            double mean = luminanceValues.Average();
            double sumSquaredDiff = luminanceValues.Sum(v => Math.Pow(v - mean, 2));

            return Math.Sqrt(sumSquaredDiff / luminanceValues.Count);
        }

        /// <summary>
        /// Detect edges using simple Sobel operator
        /// </summary>
        public Bitmap DetectEdges(Bitmap source, int threshold = 50)
        {
            Bitmap edges = new Bitmap(source.Width, source.Height);

            // Convert to grayscale first
            int[,] gray = new int[source.Width, source.Height];

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    gray[x, y] = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                }
            }

            // Apply Sobel operator
            for (int y = 1; y < source.Height - 1; y++)
            {
                for (int x = 1; x < source.Width - 1; x++)
                {
                    int gx = (-1 * gray[x - 1, y - 1]) + (0 * gray[x, y - 1]) + (1 * gray[x + 1, y - 1]) +
                             (-2 * gray[x - 1, y]) + (0 * gray[x, y]) + (2 * gray[x + 1, y]) +
                             (-1 * gray[x - 1, y + 1]) + (0 * gray[x, y + 1]) + (1 * gray[x + 1, y + 1]);

                    int gy = (-1 * gray[x - 1, y - 1]) + (-2 * gray[x, y - 1]) + (-1 * gray[x + 1, y - 1]) +
                             (0 * gray[x - 1, y]) + (0 * gray[x, y]) + (0 * gray[x + 1, y]) +
                             (1 * gray[x - 1, y + 1]) + (2 * gray[x, y + 1]) + (1 * gray[x + 1, y + 1]);

                    int magnitude = (int)Math.Sqrt(gx * gx + gy * gy);
                    magnitude = Math.Min(255, magnitude);

                    if (magnitude > threshold)
                        edges.SetPixel(x, y, Color.FromArgb(magnitude, magnitude, magnitude));
                    else
                        edges.SetPixel(x, y, Color.Black);
                }
            }

            return edges;
        }

        #endregion

        #region Color Palette

        /// <summary>
        /// Extract dominant colors from an image
        /// </summary>
        public List<Color> ExtractColorPalette(Bitmap image, int colorCount = 5)
        {
            List<Color> palette = new List<Color>();
            Dictionary<int, int> colorHistogram = new Dictionary<int, int>();

            int sampleStep = Math.Max(1, Math.Min(image.Width, image.Height) / 20);

            for (int y = 0; y < image.Height; y += sampleStep)
            {
                for (int x = 0; x < image.Width; x += sampleStep)
                {
                    Color pixel = image.GetPixel(x, y);

                    // Quantize color
                    int r = (pixel.R / 64) * 64;
                    int g = (pixel.G / 64) * 64;
                    int b = (pixel.B / 64) * 64;

                    int colorKey = (r << 16) | (g << 8) | b;

                    if (colorHistogram.ContainsKey(colorKey))
                        colorHistogram[colorKey]++;
                    else
                        colorHistogram[colorKey] = 1;
                }
            }

            // Get top N colors
            var topColors = colorHistogram
                .OrderByDescending(kvp => kvp.Value)
                .Take(colorCount);

            foreach (var kvp in topColors)
            {
                int r = (kvp.Key >> 16) & 0xFF;
                int g = (kvp.Key >> 8) & 0xFF;
                int b = kvp.Key & 0xFF;
                palette.Add(Color.FromArgb(r, g, b));
            }

            return palette;
        }

        #endregion
    }
}