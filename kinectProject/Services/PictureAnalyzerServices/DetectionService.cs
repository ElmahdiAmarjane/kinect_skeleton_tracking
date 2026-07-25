using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace kinectProject
{
    public class DetectionService
    {
        #region Main Detection Methods

        /// <summary>
        /// Main flexible detection method that handles both preset and manually picked colors
        /// </summary>
        public void DetectColoredPointsFlexible(
            Color? referenceColor,
            Bitmap originalImage,
            PointColor selectedColor,
            int detectionTolerance,
            int minPointSize,
            int maxPointSize,
            Color customColor,
            out List<DetectedPoint> detectedPoints)
        {
            detectedPoints = new List<DetectedPoint>();

            if (originalImage == null) return;

            Color targetColor;
            if (referenceColor.HasValue)
            {
                targetColor = referenceColor.Value;
            }
            else
            {
                targetColor = GetColorFromEnum(selectedColor, customColor);
            }

            using (Bitmap bmp = new Bitmap(originalImage))
            {
                int width = bmp.Width;
                int height = bmp.Height;

                HsvColor targetHsv = RgbToHsv(targetColor);
                bool[,] strictMask = new bool[height, width];
                int totalStrictPixels = 0;

                // Adaptive thresholds
                float hueThreshold = (targetHsv.S < 0.3f) ? 360f : 25f;
                float satThreshold = 0.35f;
                float valThreshold = 0.35f;
                double rgbThreshold = 55.0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = bmp.GetPixel(x, y);
                        HsvColor pixelHsv = RgbToHsv(pixel);

                        double rgbDistance = Math.Sqrt(
                            Math.Pow(pixel.R - targetColor.R, 2) +
                            Math.Pow(pixel.G - targetColor.G, 2) +
                            Math.Pow(pixel.B - targetColor.B, 2));

                        float hueDiff = Math.Abs(pixelHsv.H - targetHsv.H);
                        hueDiff = Math.Min(hueDiff, 360 - hueDiff);
                        float satDiff = Math.Abs(pixelHsv.S - targetHsv.S);
                        float valDiff = Math.Abs(pixelHsv.V - targetHsv.V);

                        bool isMatch =
                            rgbDistance <= rgbThreshold &&
                            hueDiff <= hueThreshold &&
                            satDiff <= satThreshold &&
                            valDiff <= valThreshold;

                        if (isMatch)
                        {
                            strictMask[y, x] = true;
                            totalStrictPixels++;
                        }
                    }
                }

                // Apply morphological closing
                strictMask = MorphologicalClose(strictMask, width, height, radius: 2);

                if (totalStrictPixels == 0)
                {
                    string debugPath = Path.Combine(Path.GetTempPath(), "strict_detection.png");
                    SaveDebugImage(bmp, strictMask, debugPath);
                    MessageBox.Show($"No matches found.\nDebug image: {debugPath}",
                        "Detection Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                List<ConnectedComponent> components = FindStrictComponents(strictMask, width, height);
                int expectedStickerSize = EstimateStickerSizeRobust(components);

                int minStickerArea = Math.Max(10, expectedStickerSize / 4);
                int maxStickerArea = expectedStickerSize * 5;

                StringBuilder debugInfo = new StringBuilder();
                debugInfo.AppendLine($"=== DETECTION REPORT ===");
                debugInfo.AppendLine($"Target RGB: ({targetColor.R},{targetColor.G},{targetColor.B})");
                debugInfo.AppendLine($"Target HSV: H={targetHsv.H:F1}°, S={targetHsv.S:F2}, V={targetHsv.V:F2}");
                debugInfo.AppendLine($"Matched pixels: {totalStrictPixels}");
                debugInfo.AppendLine($"Components found: {components.Count}");
                debugInfo.AppendLine($"Estimated sticker size: {expectedStickerSize} px");
                debugInfo.AppendLine($"Size range: {minStickerArea} - {maxStickerArea}");

                int id = 1;
                List<DetectedPoint> validatedPoints = new List<DetectedPoint>();

                foreach (var component in components.OrderByDescending(c => c.PixelCount))
                {
                    debugInfo.AppendLine($"\n--- Component {id} ---");
                    debugInfo.AppendLine($"  Pixels: {component.PixelCount}");
                    debugInfo.AppendLine($"  Bounds: {component.Width} x {component.Height}");

                    // Size validation
                    bool validSize = component.PixelCount >= minStickerArea &&
                                     component.PixelCount <= maxStickerArea;

                    // Aspect ratio validation
                    float aspectRatio = (float)Math.Max(component.Width, component.Height) /
                                         Math.Max(1, Math.Min(component.Width, component.Height));
                    bool validAspect = aspectRatio <= 3.0f;

                    // Circularity validation
                    double circularity = CalculateFastCircularity(component);
                    bool validCircularity = circularity >= 0.3;

                    // Color consistency validation
                    double colorStdDev = CalculateColorStandardDeviation(component, bmp, targetColor);
                    bool consistentColor = colorStdDev <= 40;

                    // Density validation
                    int bboxArea = Math.Max(1, component.Width * component.Height);
                    double density = (double)component.PixelCount / bboxArea;
                    bool validDensity = density >= 0.35;

                    // Edge strength validation
                    double edgeStrength = CalculateEdgeStrength(component, bmp);
                    bool validEdge = edgeStrength > 0.2;

                    int passedCriteria = 0;
                    if (validSize) { passedCriteria++; debugInfo.AppendLine($"  ✓ Size ({component.PixelCount})"); }
                    else debugInfo.AppendLine($"  ✗ Size ({component.PixelCount}, need {minStickerArea}-{maxStickerArea})");

                    if (validAspect) { passedCriteria++; debugInfo.AppendLine($"  ✓ Aspect ({aspectRatio:F2})"); }
                    else debugInfo.AppendLine($"  ✗ Aspect ({aspectRatio:F2})");

                    if (validCircularity) { passedCriteria++; debugInfo.AppendLine($"  ✓ Circularity ({circularity:F3})"); }
                    else debugInfo.AppendLine($"  ✗ Circularity ({circularity:F3})");

                    if (consistentColor) { passedCriteria++; debugInfo.AppendLine($"  ✓ Color StdDev ({colorStdDev:F1})"); }
                    else debugInfo.AppendLine($"  ✗ Color StdDev ({colorStdDev:F1})");

                    if (validDensity) { passedCriteria++; debugInfo.AppendLine($"  ✓ Density ({density:F2})"); }
                    else debugInfo.AppendLine($"  ✗ Density ({density:F2})");

                    if (validEdge) { passedCriteria++; debugInfo.AppendLine($"  ✓ Edge ({edgeStrength:F2})"); }
                    else debugInfo.AppendLine($"  ✗ Edge ({edgeStrength:F2})");

                    bool isValid = validSize && passedCriteria >= 3;

                    debugInfo.AppendLine($"  Criteria passed: {passedCriteria}/6 → {(isValid ? "✓ ACCEPTED" : "✗ REJECTED")}");

                    if (isValid)
                    {
                        Point center = CalculatePreciseCenter(component);
                        double confidence = passedCriteria / 6.0;

                        validatedPoints.Add(new DetectedPoint(
                            center,
                            selectedColor,
                            confidence,
                            (int)Math.Sqrt(component.PixelCount / Math.PI),
                            id
                        ));
                        id++;
                    }
                }

                debugInfo.AppendLine($"\n=== FINAL: {validatedPoints.Count} stickers detected ===");

                MessageBox.Show(debugInfo.ToString(), "Detection Analysis",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                detectedPoints.AddRange(validatedPoints);
            }
        }

        /// <summary>
        /// Simple detection test for bright red pixels
        /// </summary>
        public void SimpleDetectionTest(Bitmap originalImage, out List<DetectedPoint> detectedPoints)
        {
            detectedPoints = new List<DetectedPoint>();

            if (originalImage == null) return;

            using (Bitmap bmp = new Bitmap(originalImage))
            {
                int id = 1;

                for (int x = 0; x < bmp.Width; x += 2)
                {
                    for (int y = 0; y < bmp.Height; y += 2)
                    {
                        Color pixel = bmp.GetPixel(x, y);

                        if (pixel.R > 200 && pixel.G < 100 && pixel.B < 100)
                        {
                            bool isNewPoint = true;
                            foreach (var existing in detectedPoints)
                            {
                                double distance = Math.Sqrt(
                                    Math.Pow(existing.Location.X - x, 2) +
                                    Math.Pow(existing.Location.Y - y, 2));
                                if (distance < 30)
                                {
                                    isNewPoint = false;
                                    break;
                                }
                            }

                            if (isNewPoint)
                            {
                                detectedPoints.Add(new DetectedPoint(
                                    new Point(x, y),
                                    PointColor.Red,
                                    1.0,
                                    10,
                                    id++
                                ));
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Component Finding Methods

        /// <summary>
        /// Find connected components using 4-connectivity (strict)
        /// </summary>
        public List<ConnectedComponent> FindStrictComponents(bool[,] mask, int width, int height)
        {
            List<ConnectedComponent> components = new List<ConnectedComponent>();
            bool[,] visited = new bool[height, width];
            Queue<Point> queue = new Queue<Point>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x] && !visited[y, x])
                    {
                        ConnectedComponent comp = new ConnectedComponent();
                        queue.Clear();
                        queue.Enqueue(new Point(x, y));

                        while (queue.Count > 0)
                        {
                            Point p = queue.Dequeue();

                            if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height ||
                                visited[p.Y, p.X] || !mask[p.Y, p.X])
                                continue;

                            visited[p.Y, p.X] = true;
                            comp.Add(p.X, p.Y);

                            // 4-connectivity
                            queue.Enqueue(new Point(p.X + 1, p.Y));
                            queue.Enqueue(new Point(p.X - 1, p.Y));
                            queue.Enqueue(new Point(p.X, p.Y + 1));
                            queue.Enqueue(new Point(p.X, p.Y - 1));
                        }

                        if (comp.PixelCount >= 10)
                        {
                            components.Add(comp);
                        }
                    }
                }
            }

            return components;
        }

        /// <summary>
        /// Find stickers using 8-connectivity
        /// </summary>
        public List<ConnectedComponent> FindStickers(bool[,] mask, int width, int height)
        {
            List<ConnectedComponent> components = new List<ConnectedComponent>();
            bool[,] visited = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x] && !visited[y, x])
                    {
                        ConnectedComponent comp = new ConnectedComponent();
                        Stack<Point> stack = new Stack<Point>();
                        stack.Push(new Point(x, y));

                        while (stack.Count > 0)
                        {
                            Point p = stack.Pop();

                            if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height ||
                                visited[p.Y, p.X] || !mask[p.Y, p.X])
                                continue;

                            visited[p.Y, p.X] = true;
                            comp.Add(p.X, p.Y);

                            // 8-connectivity
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    stack.Push(new Point(p.X + dx, p.Y + dy));
                                }
                            }
                        }

                        if (comp.PixelCount >= 10)
                        {
                            components.Add(comp);
                        }
                    }
                }
            }

            return components;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Calculate circularity of a component (1 = perfect circle)
        /// </summary>
        public double CalculateCircularity(ConnectedComponent comp)
        {
            double area = comp.PixelCount;
            double perimeter = 2 * (comp.Width + comp.Height);

            if (perimeter == 0) return 0;
            return (4 * Math.PI * area) / (perimeter * perimeter);
        }

        /// <summary>
        /// Fast circularity calculation using HashSet for O(1) lookup
        /// </summary>
        public double CalculateFastCircularity(ConnectedComponent comp)
        {
            var pixelSet = new HashSet<long>();
            foreach (var p in comp.Pixels)
                pixelSet.Add((long)p.Y * 100000 + p.X);

            int perimeter = 0;
            foreach (var p in comp.Pixels)
            {
                if (!pixelSet.Contains((long)(p.Y - 1) * 100000 + p.X) ||
                    !pixelSet.Contains((long)(p.Y + 1) * 100000 + p.X) ||
                    !pixelSet.Contains((long)p.Y * 100000 + (p.X - 1)) ||
                    !pixelSet.Contains((long)p.Y * 100000 + (p.X + 1)))
                {
                    perimeter++;
                }
            }

            if (perimeter == 0) return 1.0;
            double area = comp.PixelCount;
            return (4.0 * Math.PI * area) / (perimeter * perimeter);
        }

        /// <summary>
        /// Calculate average color of a component
        /// </summary>
        public Color CalculateAverageColor(ConnectedComponent comp, Bitmap image)
        {
            long totalR = 0, totalG = 0, totalB = 0;

            foreach (var p in comp.Pixels)
            {
                Color pixel = image.GetPixel(p.X, p.Y);
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
            }

            int count = comp.PixelCount;
            return Color.FromArgb(
                (int)(totalR / count),
                (int)(totalG / count),
                (int)(totalB / count));
        }

        /// <summary>
        /// Check if color is a sticker red
        /// </summary>
        public bool IsStickerRed(Color color)
        {
            return color.R > 180 &&
                   color.G < 100 &&
                   color.B < 100 &&
                   color.R > color.G + 80 &&
                   color.R > color.B + 80;
        }

        /// <summary>
        /// Check if component has holes (non-red pixels inside bounding box)
        /// </summary>
        public bool HasHoles(ConnectedComponent comp, bool[,] mask)
        {
            int holePixels = 0;
            int totalPixelsInBbox = 0;

            for (int y = comp.MinY + 1; y < comp.MaxY; y++)
            {
                for (int x = comp.MinX + 1; x < comp.MaxX; x++)
                {
                    totalPixelsInBbox++;
                    if (!mask[y, x])
                    {
                        holePixels++;
                    }
                }
            }

            return totalPixelsInBbox > 0 && ((double)holePixels / totalPixelsInBbox) > 0.2;
        }

        /// <summary>
        /// Calculate color standard deviation within component
        /// </summary>
        public double CalculateColorStandardDeviation(ConnectedComponent comp, Bitmap image, Color targetColor)
        {
            List<double> colorDistances = new List<double>();

            foreach (var p in comp.Pixels)
            {
                Color pixel = image.GetPixel(p.X, p.Y);
                double distance = Math.Sqrt(
                    Math.Pow(pixel.R - targetColor.R, 2) +
                    Math.Pow(pixel.G - targetColor.G, 2) +
                    Math.Pow(pixel.B - targetColor.B, 2));
                colorDistances.Add(distance);
            }

            if (colorDistances.Count == 0) return 0;

            double mean = colorDistances.Average();
            double sumOfSquares = colorDistances.Sum(d => Math.Pow(d - mean, 2));
            return Math.Sqrt(sumOfSquares / colorDistances.Count);
        }

        /// <summary>
        /// Calculate edge strength of a component
        /// </summary>
        public double CalculateEdgeStrength(ConnectedComponent comp, Bitmap image)
        {
            double totalGradient = 0;
            int edgePixels = 0;

            foreach (var p in comp.Pixels)
            {
                bool isEdge = false;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = p.X + dx;
                        int ny = p.Y + dy;

                        if (nx < 0 || nx >= image.Width || ny < 0 || ny >= image.Height)
                        {
                            isEdge = true;
                            break;
                        }

                        if (!comp.Pixels.Any(pp => pp.X == nx && pp.Y == ny))
                        {
                            isEdge = true;
                            break;
                        }
                    }
                    if (isEdge) break;
                }

                if (isEdge)
                {
                    if (p.X > 0 && p.X < image.Width - 1)
                    {
                        Color left = image.GetPixel(p.X - 1, p.Y);
                        Color right = image.GetPixel(p.X + 1, p.Y);
                        double gradX = (right.R - left.R) / 255.0;

                        if (p.Y > 0 && p.Y < image.Height - 1)
                        {
                            Color top = image.GetPixel(p.X, p.Y - 1);
                            Color bottom = image.GetPixel(p.X, p.Y + 1);
                            double gradY = (bottom.R - top.R) / 255.0;

                            totalGradient += Math.Sqrt(gradX * gradX + gradY * gradY);
                            edgePixels++;
                        }
                    }
                }
            }

            return edgePixels > 0 ? totalGradient / edgePixels : 0;
        }

        /// <summary>
        /// Check if point is inside component using ray casting
        /// </summary>
        public bool IsPointInsideComponent(int x, int y, ConnectedComponent comp)
        {
            int intersections = 0;
            foreach (var p in comp.Pixels)
            {
                var next = comp.Pixels.FirstOrDefault(q => q.X > p.X && Math.Abs(q.Y - p.Y) < 2);
                if (next.X != 0 && next.Y != 0)
                {
                    if (IsIntersecting(x, y, p, next))
                    {
                        intersections++;
                    }
                }
            }
            return intersections % 2 == 1;
        }

        /// <summary>
        /// Ray casting helper
        /// </summary>
        public bool IsIntersecting(int px, int py, Point p1, Point p2)
        {
            if (p1.Y > py && p2.Y > py) return false;
            if (p1.Y < py && p2.Y < py) return false;
            if (p1.X < px && p2.X < px) return false;

            double xIntersect = p1.X + (double)(py - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
            return xIntersect > px;
        }

        /// <summary>
        /// Calculate precise center using weighted average
        /// </summary>
        public Point CalculatePreciseCenter(ConnectedComponent comp)
        {
            double sumX = 0, sumY = 0;
            int count = comp.Pixels.Count;

            foreach (var p in comp.Pixels)
            {
                sumX += p.X;
                sumY += p.Y;
            }

            return new Point((int)(sumX / count), (int)(sumY / count));
        }

        /// <summary>
        /// Estimate sticker size robustly using mode/cluster
        /// </summary>
        public int EstimateStickerSizeRobust(List<ConnectedComponent> components)
        {
            if (components.Count == 0) return 50;

            var sizes = components
                .Where(c => c.PixelCount >= 10)
                .Select(c => c.PixelCount)
                .OrderBy(s => s)
                .ToList();

            if (sizes.Count == 0) return 50;

            int half = Math.Max(1, sizes.Count / 2);
            var topHalf = sizes.Skip(sizes.Count - half).ToList();
            return (int)topHalf.Average();
        }

        #endregion

        #region Morphological Operations

        /// <summary>
        /// Morphological close operation (dilate then erode)
        /// </summary>
        public bool[,] MorphologicalClose(bool[,] mask, int width, int height, int radius)
        {
            // Dilate
            bool[,] dilated = new bool[height, width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (mask[y, x])
                        for (int dy = -radius; dy <= radius; dy++)
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                    dilated[ny, nx] = true;
                            }

            // Erode
            bool[,] closed = new bool[height, width];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bool allSet = true;
                    for (int dy = -radius; dy <= radius && allSet; dy++)
                        for (int dx = -radius; dx <= radius && allSet; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height || !dilated[ny, nx])
                                allSet = false;
                        }
                    closed[y, x] = allSet;
                }
            return closed;
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// Save debug image with detected pixels highlighted
        /// </summary>
        public void SaveDebugImage(Bitmap original, bool[,] mask, string path)
        {
            int w = original.Width, h = original.Height;
            using (Bitmap dbg = new Bitmap(w, h))
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y, x])
                            dbg.SetPixel(x, y, Color.Red);
                        else
                        {
                            Color p = original.GetPixel(x, y);
                            dbg.SetPixel(x, y, Color.FromArgb(p.R / 4, p.G / 4, p.B / 4));
                        }
                    }
                dbg.Save(path);
            }
        }

        /// <summary>
        /// Draw sticker marker on debug image
        /// </summary>
        public void DrawStickerMarker(Bitmap debug, ConnectedComponent sticker, int id)
        {
            using (Graphics g = Graphics.FromImage(debug))
            {
                g.DrawEllipse(Pens.Lime,
                    sticker.MinX, sticker.MinY,
                    sticker.Width, sticker.Height);

                Point center = new Point(
                    (sticker.MinX + sticker.MaxX) / 2,
                    (sticker.MinY + sticker.MaxY) / 2);

                g.FillEllipse(Brushes.Cyan, center.X - 3, center.Y - 3, 6, 6);

                g.DrawString(id.ToString(),
                    new Font("Arial", 10, FontStyle.Bold),
                    Brushes.Yellow,
                    center.X + 5, center.Y - 10);
            }
        }

        /// <summary>
        /// Simple confirmation: show image with detected points highlighted
        /// </summary>
        public bool ShowDetectionConfirmation(List<DetectedPoint> points, Bitmap originalImage)
        {
            if (points.Count == 0) return false;

            using (Bitmap preview = new Bitmap(originalImage))
            using (Graphics g = Graphics.FromImage(preview))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                foreach (var point in points.OrderBy(p => p.ID))
                {
                    // Draw circle
                    using (Pen pen = new Pen(Color.Lime, 3))
                    {
                        g.DrawEllipse(pen,
                            point.Location.X - point.Radius - 5,
                            point.Location.Y - point.Radius - 5,
                            (point.Radius + 5) * 2,
                            (point.Radius + 5) * 2);
                    }

                    // Draw ID number
                    using (Font font = new Font("Arial", 12, FontStyle.Bold))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    using (Brush textBrush = new SolidBrush(Color.Yellow))
                    {
                        string text = point.ID.ToString();
                        SizeF size = g.MeasureString(text, font);

                        g.FillRectangle(bgBrush,
                            point.Location.X - size.Width / 2 - 3,
                            point.Location.Y - point.Radius - size.Height - 15,
                            size.Width + 6, size.Height + 4);

                        g.DrawString(text, font, textBrush,
                            point.Location.X - size.Width / 2,
                            point.Location.Y - point.Radius - size.Height - 13);
                    }
                }

                using (Form previewForm = new Form())
                {
                    previewForm.Text = $"{points.Count} Markers Detected";
                    previewForm.Size = new Size(700, 500);
                    previewForm.StartPosition = FormStartPosition.CenterParent;
                    previewForm.BackColor = Color.FromArgb(45, 45, 48);

                    PictureBox pb = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        Image = new Bitmap(preview),
                        SizeMode = PictureBoxSizeMode.Zoom
                    };
                    previewForm.Controls.Add(pb);

                    Panel buttonPanel = new Panel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 50,
                        BackColor = Color.FromArgb(35, 35, 40)
                    };

                    Button btnAccept = new Button
                    {
                        Text = $"✓ Accept {points.Count} Points",
                        Dock = DockStyle.Right,
                        Width = 160,
                        BackColor = Color.FromArgb(0, 122, 204),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat
                    };
                    btnAccept.FlatAppearance.BorderSize = 0;
                    btnAccept.Click += (s, ev) => { previewForm.DialogResult = DialogResult.OK; previewForm.Close(); };

                    Button btnReject = new Button
                    {
                        Text = "✗ Cancel",
                        Dock = DockStyle.Left,
                        Width = 100,
                        BackColor = Color.FromArgb(62, 62, 64),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat
                    };
                    btnReject.FlatAppearance.BorderSize = 0;
                    btnReject.Click += (s, ev) => { previewForm.DialogResult = DialogResult.Cancel; previewForm.Close(); };

                    Label lblInfo = new Label
                    {
                        Text = $"Points ordered top-to-bottom | Green circles = detected markers",
                        Dock = DockStyle.Fill,
                        ForeColor = Color.LightGray,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI", 9)
                    };

                    buttonPanel.Controls.Add(btnAccept);
                    buttonPanel.Controls.Add(btnReject);
                    buttonPanel.Controls.Add(lblInfo);
                    previewForm.Controls.Add(buttonPanel);

                    return previewForm.ShowDialog() == DialogResult.OK;
                }
            }
        }
        /// <summary>
        /// Show color preview and detection dialog
        /// </summary>
        public bool ShowColorPreviewAndDetect(
            Color pickedColor,
            Point pickPoint,
            Bitmap originalImage,
            int detectionTolerance,
            PointColor selectedColor,
            Color customColor,
            out List<DetectedPoint> detectedPoints)
        {
            detectedPoints = new List<DetectedPoint>();
            int tolerance = detectionTolerance;
            bool shouldPickAgain = false;

            Form previewForm = new Form
            {
                Text = "Color Sampled - Adjust Detection",
                Size = new Size(450, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // Sampled color preview
            Label sampledLabel = new Label
            {
                Text = "Sampled Color:",
                Location = new Point(20, 20),
                Size = new Size(100, 25),
                ForeColor = Color.White
            };

            Panel colorPanel = new Panel
            {
                BackColor = pickedColor,
                Location = new Point(130, 20),
                Size = new Size(100, 25),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label rgbLabel = new Label
            {
                Text = $"RGB: {pickedColor.R}, {pickedColor.G}, {pickedColor.B}",
                Location = new Point(240, 20),
                Size = new Size(150, 25),
                ForeColor = Color.White
            };

            HsvColor hsv = RgbToHsv(pickedColor);
            Label hsvLabel = new Label
            {
                Text = $"HSV: H={hsv.H:F0}°, S={hsv.S:F2}, V={hsv.V:F2}",
                Location = new Point(20, 55),
                Size = new Size(300, 25),
                ForeColor = Color.Cyan
            };

            Label toleranceLabel = new Label
            {
                Text = "Color Tolerance:",
                Location = new Point(20, 100),
                Size = new Size(100, 25),
                ForeColor = Color.White
            };

            TrackBar toleranceTrackBar = new TrackBar
            {
                Location = new Point(130, 100),
                Size = new Size(200, 45),
                Minimum = 5,
                Maximum = 50,
                Value = detectionTolerance,
                TickFrequency = 5
            };

            Label toleranceValue = new Label
            {
                Text = detectionTolerance.ToString(),
                Location = new Point(340, 100),
                Size = new Size(40, 25),
                ForeColor = Color.Yellow
            };

            toleranceTrackBar.ValueChanged += (s, ev) =>
            {
                toleranceValue.Text = toleranceTrackBar.Value.ToString();
            };

            Panel previewPanel = new Panel
            {
                Location = new Point(20, 160),
                Size = new Size(400, 100),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            PictureBox previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            previewPanel.Controls.Add(previewBox);

            toleranceTrackBar.ValueChanged += (s, ev) =>
            {
                UpdateDetectionPreview(previewBox, pickedColor, toleranceTrackBar.Value, originalImage);
            };

            Button detectButton = new Button
            {
                Text = "Detect Stickers",
                Location = new Point(100, 280),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(230, 280),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            Button resetButton = new Button
            {
                Text = "Pick Another Color",
                Location = new Point(100, 320),
                Size = new Size(250, 30),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // Fix: use a local variable instead of out parameter in lambda
            var localDetectedPoints = new List<DetectedPoint>();

            detectButton.Click += (s, ev) =>
            {
                tolerance = toleranceTrackBar.Value;
                DetectColoredPointsFlexible(pickedColor, originalImage, selectedColor,
                    tolerance, 5, 30, customColor, out localDetectedPoints);
                previewForm.DialogResult = DialogResult.OK;
                previewForm.Close();
            };

            resetButton.Click += (s, ev) =>
            {
                shouldPickAgain = true;
                previewForm.Close();
            };

            previewForm.Controls.AddRange(new Control[]
            {
        sampledLabel, colorPanel, rgbLabel, hsvLabel,
        toleranceLabel, toleranceTrackBar, toleranceValue,
        previewPanel, detectButton, cancelButton, resetButton
            });

            UpdateDetectionPreview(previewBox, pickedColor, detectionTolerance, originalImage);

            DialogResult result = previewForm.ShowDialog();

            if (result == DialogResult.OK)
            {
                detectedPoints = localDetectedPoints;
                return false; // Don't pick again
            }

            if (shouldPickAgain)
            {
                return true; // Signal to pick again
            }

            return false;
        }
        /// <summary>
        /// Update detection preview image
        /// </summary>
        public void UpdateDetectionPreview(PictureBox previewBox, Color targetColor, int tolerance, Bitmap originalImage)
        {
            if (originalImage == null) return;

            using (Bitmap bmp = new Bitmap(originalImage))
            using (Bitmap preview = new Bitmap(bmp.Width, bmp.Height))
            {
                HsvColor targetHsv = RgbToHsv(targetColor);
                float hueTolerance = tolerance;

                for (int y = 0; y < bmp.Height; y += 3)
                {
                    for (int x = 0; x < bmp.Width; x += 3)
                    {
                        Color pixel = bmp.GetPixel(x, y);
                        HsvColor pixelHsv = RgbToHsv(pixel);

                        float hueDiff = Math.Abs(pixelHsv.H - targetHsv.H);
                        hueDiff = Math.Min(hueDiff, 360 - hueDiff);

                        if (hueDiff <= hueTolerance &&
                            Math.Abs(pixelHsv.S - targetHsv.S) < 0.3f &&
                            Math.Abs(pixelHsv.V - targetHsv.V) < 0.3f)
                        {
                            preview.SetPixel(x, y, Color.Red);
                        }
                        else
                        {
                            preview.SetPixel(x, y, Color.FromArgb(
                                pixel.R / 3,
                                pixel.G / 3,
                                pixel.B / 3));
                        }
                    }
                }

                previewBox.Image = new Bitmap(preview);
            }
        }

        #endregion

        #region Color Conversion Helpers

        /// <summary>
        /// Convert RGB color to HSV
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

        /// <summary>
        /// Check if two colors are similar within tolerance
        /// </summary>
        public bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            int rDiff = c1.R - c2.R;
            int gDiff = c1.G - c2.G;
            int bDiff = c1.B - c2.B;

            double distance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
            return distance <= tolerance;
        }

        #endregion

        #region Manual Point Detection

        /// <summary>
        /// Handle manual point detection at click location
        /// </summary>
        public void HandleManualPointDetection(
            Point clickPoint,
            PointColor selectedColor,
            Color customColor,
            bool autoRenameEnabled,
            List<DetectedPoint> detectedPoints,
            List<Measurement> measurements,
            ref int idCounter)
        {
            int newId = detectedPoints.Count > 0 ? detectedPoints.Max(p => p.ID) + 1 : 1;

            DetectedPoint newPoint = new DetectedPoint(
                clickPoint,
                selectedColor,
                1.0,
                10,
                newId
            );

            detectedPoints.Add(newPoint);

            string pointName = $"P{newId}";

            if (autoRenameEnabled)
            {
                using (var renameDialog = new AutoRenameDialog(pointName))
                {
                    if (renameDialog.ShowDialog() == DialogResult.OK)
                    {
                        pointName = string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                                   pointName : renameDialog.NewName.Trim();

                        // Note: DontAskAgain property exists but we can't modify autoRenameEnabled here
                        // since it's a ref parameter issue - the parent form handles this
                    }
                }
            }

            Measurement measurement = new Measurement(
                clickPoint,
                clickPoint,
                pointName,
                MeasurementType.Point,
                idCounter++);

            measurements.Add(measurement);
        }

        /// <summary>
        /// Create measurements from detected points
        /// </summary>
        public void CreateMeasurementsFromDetectedPoints(
            List<DetectedPoint> detectedPoints,
            List<Measurement> measurements,
            ref int idCounter,
            bool autoRenameEnabled)
        {
            int existingPointCount = measurements.Count(m => m.Type == MeasurementType.Point);

            foreach (var detectedPoint in detectedPoints)
            {
                string pointName = $"P{existingPointCount + 1}";
                existingPointCount++;

                if (autoRenameEnabled)
                {
                    using (var renameDialog = new AutoRenameDialog(pointName))
                    {
                        if (renameDialog.ShowDialog() == DialogResult.OK)
                        {
                            pointName = string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                                       pointName : renameDialog.NewName.Trim();
                        }
                        // Cancel = keep default name
                    }
                }

                Measurement measurement = new Measurement(
                    detectedPoint.Location,
                    detectedPoint.Location,
                    pointName,
                    MeasurementType.Point,
                    idCounter++);

                measurements.Add(measurement);
            }
        }
        #endregion


        /// <summary>
        /// Doctor clicks one point, we sample its color and find all similar points
        /// </summary>
        public List<DetectedPoint> DetectByColorSample(Bitmap image, Point samplePoint,
            int tolerance, int minSize, int maxSize)
        {
            var points = new List<DetectedPoint>();

            if (image == null) return points;
            if (samplePoint.X < 0 || samplePoint.X >= image.Width ||
                samplePoint.Y < 0 || samplePoint.Y >= image.Height)
                return points;

            // Sample the color at the clicked point
            Color targetColor = image.GetPixel(samplePoint.X, samplePoint.Y);

            // Also sample surrounding pixels for better color average
            int sampleRadius = 3;
            int totalR = 0, totalG = 0, totalB = 0, count = 0;

            for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
            {
                for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
                {
                    int sx = samplePoint.X + dx;
                    int sy = samplePoint.Y + dy;
                    if (sx >= 0 && sx < image.Width && sy >= 0 && sy < image.Height)
                    {
                        Color c = image.GetPixel(sx, sy);
                        totalR += c.R;
                        totalG += c.G;
                        totalB += c.B;
                        count++;
                    }
                }
            }

            targetColor = Color.FromArgb(totalR / count, totalG / count, totalB / count);

            // Use existing flexible detection with sampled color
            DetectColoredPointsFlexible(targetColor, image, PointColor.Custom,
                tolerance, minSize, maxSize, targetColor, out points);

            return points;
        }

        /// <summary>
        /// Aggressive detection - finds ALL points of similar color
        /// Designed for medical markers drawn on skin
        /// </summary>
        /// <summary>
        /// Smart marker detection - finds brightly colored dots on skin
        /// </summary>
        public List<DetectedPoint> DetectAllMarkers(Bitmap image, Point samplePoint)
        {
            var points = new List<DetectedPoint>();
            if (image == null) return points;
            if (samplePoint.X < 0 || samplePoint.X >= image.Width ||
                samplePoint.Y < 0 || samplePoint.Y >= image.Height)
                return points;

            // Sample the EXACT color at the clicked point
            Color targetColor = image.GetPixel(samplePoint.X, samplePoint.Y);

            // Also sample a 3x3 area around it
            Color avgColor = SampleColorAtPoint(image, samplePoint, 1);

            int width = image.Width;
            int height = image.Height;
            bool[,] visited = new bool[height, width];
            int id = 1;

            // Very strict: must be very close to target color
            int strictThreshold = 40; // RGB distance max

            // Find all blobs
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[y, x]) continue;

                    Color pixel = image.GetPixel(x, y);

                    // Quick check: is this pixel similar to our target?
                    int rDiff = Math.Abs(pixel.R - targetColor.R);
                    int gDiff = Math.Abs(pixel.G - targetColor.G);
                    int bDiff = Math.Abs(pixel.B - targetColor.B);
                    double dist = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);

                    if (dist > strictThreshold) continue;

                    // Flood fill this blob
                    var blob = new List<Point>();
                    var queue = new Queue<Point>();
                    queue.Enqueue(new Point(x, y));
                    visited[y, x] = true;

                    while (queue.Count > 0)
                    {
                        var p = queue.Dequeue();

                        // Check bounds
                        if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height)
                            continue;

                        Color pxl = image.GetPixel(p.X, p.Y);
                        rDiff = Math.Abs(pxl.R - targetColor.R);
                        gDiff = Math.Abs(pxl.G - targetColor.G);
                        bDiff = Math.Abs(pxl.B - targetColor.B);
                        dist = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);

                        if (dist > strictThreshold) continue;

                        blob.Add(p);

                        // Check neighbors
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = p.X + dx;
                                int ny = p.Y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[ny, nx])
                                {
                                    visited[ny, nx] = true;
                                    queue.Enqueue(new Point(nx, ny));
                                }
                            }
                        }
                    }

                    // Only accept blobs of reasonable size (a marker dot)
                    if (blob.Count >= 5 && blob.Count <= 300)
                    {
                        // Calculate center of blob
                        int cx = 0, cy = 0;
                        foreach (var pt in blob) { cx += pt.X; cy += pt.Y; }
                        cx /= blob.Count;
                        cy /= blob.Count;

                        // Verify the blob is roughly circular
                        double avgDist = 0;
                        foreach (var pt in blob)
                        {
                            avgDist += Math.Sqrt(Math.Pow(pt.X - cx, 2) + Math.Pow(pt.Y - cy, 2));
                        }
                        avgDist /= blob.Count;
                        int radius = (int)avgDist;

                        // Check if blob is too elongated (not a dot)
                        int minX = blob.Min(pt => pt.X);
                        int maxX = blob.Max(pt => pt.X);
                        int minY = blob.Min(pt => pt.Y);
                        int maxY = blob.Max(pt => pt.Y);
                        int w = maxX - minX;
                        int h = maxY - minY;

                        double aspectRatio = (double)Math.Max(w, h) / Math.Max(1, Math.Min(w, h));

                        // Accept if roughly circular (aspect ratio < 3)
                        if (aspectRatio < 3.0 && radius >= 2 && radius <= 25)
                        {
                            points.Add(new DetectedPoint(
                                new Point(cx, cy),
                                PointColor.Custom,
                                1.0,
                                radius,
                                id++));
                        }
                    }
                }
            }

            // Sort by Y (top to bottom), then X (left to right) for consistent ordering
            points = points.OrderBy(p => p.Location.Y)
                           .ThenBy(p => p.Location.X)
                           .ToList();

            // Re-assign IDs
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                points[i] = new DetectedPoint(p.Location, p.Color, p.Confidence, p.Radius, i + 1);
            }

            return points;
        }

        private Color SampleColorAtPoint(Bitmap image, Point center, int radius)
        {
            int totalR = 0, totalG = 0, totalB = 0, count = 0;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int sx = center.X + dx;
                    int sy = center.Y + dy;
                    if (sx >= 0 && sx < image.Width && sy >= 0 && sy < image.Height)
                    {
                        Color c = image.GetPixel(sx, sy);
                        totalR += c.R; totalG += c.G; totalB += c.B;
                        count++;
                    }
                }
            }
            return Color.FromArgb(totalR / count, totalG / count, totalB / count);
        }
        
        private bool IsSimilarColor(Color pixel, Color target, HsvColor targetHsv,
            double rgbThreshold, float hueThreshold, float satThreshold, float valThreshold)
        {
            double rgbDist = Math.Sqrt(
                Math.Pow(pixel.R - target.R, 2) +
                Math.Pow(pixel.G - target.G, 2) +
                Math.Pow(pixel.B - target.B, 2));

            if (rgbDist <= rgbThreshold) return true;

            HsvColor pixelHsv = RgbToHsv(pixel);
            float hueDiff = Math.Abs(pixelHsv.H - targetHsv.H);
            hueDiff = Math.Min(hueDiff, 360 - hueDiff);

            return hueDiff <= hueThreshold &&
                   Math.Abs(pixelHsv.S - targetHsv.S) <= satThreshold &&
                   Math.Abs(pixelHsv.V - targetHsv.V) <= valThreshold;
        }

        private List<Point> FloodFillBlob(Bitmap image, int startX, int startY, bool[,] visited,
            Color target, HsvColor targetHsv, double rgbThreshold, float hueThreshold,
            float satThreshold, float valThreshold, int width, int height)
        {
            var blob = new List<Point>();
            var queue = new Queue<Point>();
            queue.Enqueue(new Point(startX, startY));

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height || visited[p.Y, p.X])
                    continue;

                Color pixel = image.GetPixel(p.X, p.Y);
                if (!IsSimilarColor(pixel, target, targetHsv, rgbThreshold, hueThreshold, satThreshold, valThreshold))
                    continue;

                visited[p.Y, p.X] = true;
                blob.Add(p);

                // 8-connectivity
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0)
                            queue.Enqueue(new Point(p.X + dx, p.Y + dy));
            }

            return blob;
        }
    }
}