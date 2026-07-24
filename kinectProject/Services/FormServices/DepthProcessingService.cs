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

            // ✅ Use SpineMid for center reference
            CameraSpacePoint spineBase = trackedBody.Joints[JointType.SpineMid].Position;
            ushort referenceDepth = (ushort)(spineBase.Z * 1000);

            // ✅ Color window = ±20cm (tight precision, same as before)
            ushort colorMinDepth = (ushort)Math.Max(referenceDepth - DEPTH_WINDOW, BODY_DETECTION_MIN_DEPTH);
            ushort colorMaxDepth = (ushort)Math.Min(referenceDepth + DEPTH_WINDOW, BODY_DETECTION_MAX_DEPTH);

            // ✅ Body detection window = wider (±50cm) just for gray/black decision
            ushort bodyMinDepth = (ushort)Math.Max(referenceDepth - 500, BODY_DETECTION_MIN_DEPTH);
            ushort bodyMaxDepth = (ushort)Math.Min(referenceDepth + 500, BODY_DETECTION_MAX_DEPTH);

            double depthRange = colorMaxDepth - colorMinDepth;

            Parallel.For(0, depthData.Length, i =>
            {
                ushort depth = depthData[i];

                // Outside body range = black (true background)
                if (depth == 0 || depth < bodyMinDepth || depth > bodyMaxDepth)
                {
                    SetPixelColor(i, 0, 0, 0);
                    return;
                }

                // Inside body range but outside color window = gray (body context)
                if (depth < colorMinDepth || depth > colorMaxDepth)
                {
                    SetPixelColor(i, 60, 60, 65);
                    return;
                }

                // Inside color window = full precision rainbow
                double normalizedDepth = (depth - colorMinDepth) / depthRange;
                normalizedDepth = Math.Max(0.0, Math.Min(1.0, normalizedDepth));
                Color color = HsvToRgb(normalizedDepth * 360.0, 1.0, 1.0);
                SetPixelColor(i, color.R, color.G, color.B);
            });

            UpdateBitmap(width, height);
        }
        /// <summary>
        /// Build a body mask using joint positions and limb thickness
        /// </summary>
        private bool[,] BuildBodyMask(Body body, int width, int height)
        {
            bool[,] mask = new bool[height, width];

            // Define body segments as pairs of connected joints
            var segments = new (JointType start, JointType end, int thickness)[]
            {
                // Spine (thick)
                (JointType.Head, JointType.Neck, 35),
                (JointType.Neck, JointType.SpineShoulder, 40),
                (JointType.SpineShoulder, JointType.SpineMid, 45),
                (JointType.SpineMid, JointType.SpineBase, 50),
                
                // Arms (medium)
                (JointType.SpineShoulder, JointType.ShoulderLeft, 30),
                (JointType.ShoulderLeft, JointType.ElbowLeft, 25),
                (JointType.ElbowLeft, JointType.WristLeft, 20),
                (JointType.SpineShoulder, JointType.ShoulderRight, 30),
                (JointType.ShoulderRight, JointType.ElbowRight, 25),
                (JointType.ElbowRight, JointType.WristRight, 20),
                
                // Legs (thick)
                (JointType.SpineBase, JointType.HipLeft, 45),
                (JointType.HipLeft, JointType.KneeLeft, 40),
                (JointType.KneeLeft, JointType.AnkleLeft, 35),
                (JointType.SpineBase, JointType.HipRight, 45),
                (JointType.HipRight, JointType.KneeRight, 40),
                (JointType.KneeRight, JointType.AnkleRight, 35),
                
                // Head
                (JointType.Head, JointType.Neck, 30),
            };

            foreach (var seg in segments)
            {
                Joint startJoint = body.Joints[seg.start];
                Joint endJoint = body.Joints[seg.end];

                if (startJoint.TrackingState == TrackingState.NotTracked ||
                    endJoint.TrackingState == TrackingState.NotTracked)
                    continue;

                DepthSpacePoint startDP = coordinateMapper.MapCameraPointToDepthSpace(startJoint.Position);
                DepthSpacePoint endDP = coordinateMapper.MapCameraPointToDepthSpace(endJoint.Position);

                // Draw thick line between joints on the mask
                DrawThickLineOnMask(mask, width, height,
                    (int)startDP.X, (int)startDP.Y,
                    (int)endDP.X, (int)endDP.Y,
                    seg.thickness);
            }

            // ✅ Dilate mask slightly to capture body edges
            mask = DilateMask(mask, width, height, 4);

            return mask;
        }

        private void DrawThickLineOnMask(bool[,] mask, int width, int height,
            int x1, int y1, int x2, int y2, int thickness)
        {
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            int halfThick = thickness / 2;

            while (true)
            {
                // Fill a thick point
                for (int ty = -halfThick; ty <= halfThick; ty++)
                {
                    for (int tx = -halfThick; tx <= halfThick; tx++)
                    {
                        if (tx * tx + ty * ty <= halfThick * halfThick) // Circular
                        {
                            int px = x1 + tx;
                            int py = y1 + ty;
                            if (px >= 0 && px < width && py >= 0 && py < height)
                            {
                                mask[py, px] = true;
                            }
                        }
                    }
                }

                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x1 += sx; }
                if (e2 < dx) { err += dx; y1 += sy; }
            }
        }

        private bool[,] DilateMask(bool[,] mask, int width, int height, int radius)
        {
            bool[,] dilated = new bool[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x])
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    dilated[ny, nx] = true;
                                }
                            }
                        }
                    }
                }
            }
            return dilated;
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