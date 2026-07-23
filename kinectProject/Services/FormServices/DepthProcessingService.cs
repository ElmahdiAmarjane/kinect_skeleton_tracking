using Microsoft.Kinect;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace kinectProject
{
    public class DepthProcessingService
    {
        private const ushort BODY_DETECTION_MIN_DEPTH = 500;
        private const ushort BODY_DETECTION_MAX_DEPTH = 3000;
        private const int DEPTH_WINDOW = 200;

        private byte[] depthPixels;
        private Bitmap depthBitmap;
        private CoordinateMapper coordinateMapper;

        public Bitmap DepthBitmap => depthBitmap;

        public DepthProcessingService(CoordinateMapper mapper)
        {
            coordinateMapper = mapper;
            depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
            depthPixels = new byte[512 * 424 * 4];
        }

        public Body GetTrackedBody(BodyFrame bodyFrame)
        {
            Body[] bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);
            return bodies.FirstOrDefault(b => b.IsTracked);
        }

        public void ProcessDepthFrameWithBodyContext(DepthFrame depthFrame, Body trackedBody)
        {
            int width = depthFrame.FrameDescription.Width;
            int height = depthFrame.FrameDescription.Height;
            ushort[] depthData = new ushort[width * height];
            depthFrame.CopyFrameDataToArray(depthData);

            if (trackedBody == null) return;

            CameraSpacePoint spineBase = trackedBody.Joints[JointType.SpineMid].Position;
            ushort referenceDepth = (ushort)(spineBase.Z * 1000);
            ushort minDepth = (ushort)Math.Max(referenceDepth - DEPTH_WINDOW, BODY_DETECTION_MIN_DEPTH);
            ushort maxDepth = (ushort)Math.Min(referenceDepth + DEPTH_WINDOW, BODY_DETECTION_MAX_DEPTH);

            double depthRange = maxDepth - minDepth;

            Parallel.For(0, depthData.Length, i =>
            {
                ushort depth = depthData[i];

                // Outside the 3m range = black
                if (depth == 0 || depth < BODY_DETECTION_MIN_DEPTH || depth > BODY_DETECTION_MAX_DEPTH)
                {
                    SetPixelColor(i, 0, 0, 0);
                    return;
                }

                // Inside 3m range but outside body window = grayscale
                if (depth < minDepth || depth > maxDepth)
                {
                    byte gray = (byte)(128 + (depth % 64)); // Subtle texture for context
                    SetPixelColor(i, gray, gray, gray);
                    return;
                }

                // Inside body window = FULL PRECISION COLOR (same as before!)
                double normalizedDepth = (depth - minDepth) / depthRange;
                normalizedDepth = Math.Max(0.0, Math.Min(1.0, normalizedDepth));
                Color color = HsvToRgb(normalizedDepth * 360.0, 1.0, 1.0);
                SetPixelColor(i, color.R, color.G, color.B);
            });

            UpdateBitmap(width, height);
        }

        public ushort[] GetDepthData(DepthFrame depthFrame)
        {
            int width = depthFrame.FrameDescription.Width;
            int height = depthFrame.FrameDescription.Height;
            ushort[] depthData = new ushort[width * height];
            depthFrame.CopyFrameDataToArray(depthData);
            return depthData;
        }

        public ushort[] SmoothDepthData(ushort[] depthData, int width, int height)
        {
            ushort[] smoothed = new ushort[depthData.Length];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    if (depthData[index] == 0) continue;

                    ushort sum = 0;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int neighborIndex = (y + dy) * width + (x + dx);
                            if (depthData[neighborIndex] > 0)
                            {
                                sum += depthData[neighborIndex];
                                count++;
                            }
                        }
                    }
                    smoothed[index] = (ushort)(sum / Math.Max(1, count));
                }
            }
            return smoothed;
        }

        private void SetPixelColor(int index, byte r, byte g, byte b)
        {
            depthPixels[index * 4] = b;
            depthPixels[index * 4 + 1] = g;
            depthPixels[index * 4 + 2] = r;
            depthPixels[index * 4 + 3] = 255;
        }

        private void UpdateBitmap(int width, int height)
        {
            BitmapData bitmapData = depthBitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(depthPixels, 0, bitmapData.Scan0, depthPixels.Length);
            depthBitmap.UnlockBits(bitmapData);
        }

        public Bitmap GetSafeDepthBitmap()
        {
            if (depthBitmap == null) return null;
            return new Bitmap(depthBitmap);
        }

        public Color HsvToRgb(double h, double s, double v)
        {
            h = (h % 360 + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);

            return Color.FromArgb(r, g, b);
        }
    }
}