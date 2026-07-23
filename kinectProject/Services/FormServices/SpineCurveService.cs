using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;


namespace kinectProject
{
    public class SpineCurveService
    {
        private const ushort BODY_DETECTION_MIN_DEPTH = 500;
        private const ushort BODY_DETECTION_MAX_DEPTH = 3000;
        private CoordinateMapper coordinateMapper;

        public int MaxZIndex { get; private set; } = -1;
        public float FixedDeepestXPixel { get;  set; } = -1;
        public float ManualZRef { get; set; } = -1;
        public List<System.Drawing.PointF> LastSmoothedSpinePoints { get; private set; } = new List<System.Drawing.PointF>();

        public SpineCurveService(CoordinateMapper mapper)
        {
            coordinateMapper = mapper;
        }

        public void DrawDepthSpineCurve(ushort[] depthData, Body trackedBody, PictureBox sideBox)
        {
            int width = 512;
            int height = 424;
            int centerX = width / 2;
            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);

            List<System.Drawing.PointF> rawPoints = new List<System.Drawing.PointF>();
            float maxZ = float.MinValue;
            MaxZIndex = -1;

            CameraSpacePoint neckPos = trackedBody.Joints[JointType.Neck].Position;
            CameraSpacePoint basePos = trackedBody.Joints[JointType.SpineBase].Position;

            DepthSpacePoint neckDepth = coordinateMapper.MapCameraPointToDepthSpace(neckPos);
            DepthSpacePoint baseDepth = coordinateMapper.MapCameraPointToDepthSpace(basePos);

            int startY = (int)Math.Max(0, neckDepth.Y);
            int endY = (int)Math.Min(height - 1, baseDepth.Y);

            if (endY <= startY)
            {
                sideBox.Image = sideView;
                return;
            }

            // ✅ Use the tracked body's distance as the reference depth
            ushort referenceDepth = (ushort)(trackedBody.Joints[JointType.SpineMid].Position.Z * 1000);

            // Keep only pixels within ±20 cm of the body
            ushort minDepth = (ushort)Math.Max(referenceDepth - 200, 500);
            ushort maxDepth = (ushort)Math.Min(referenceDepth + 200, 3000);

            for (int y = startY; y <= endY; y++)
            {
                List<float> zSamples = new List<float>();

                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = centerX + dx;
                    if (x < 0 || x >= width)
                        continue;

                    int index = y * width + x;
                    ushort depth = depthData[index];

                    // ✅ Adaptive depth filtering
                    if (depth == 0 || depth < minDepth || depth > maxDepth)
                        continue;

                    CameraSpacePoint cp = coordinateMapper.MapDepthPointToCameraSpace(
                        new DepthSpacePoint { X = x, Y = y }, depth);

                    zSamples.Add(cp.Z * 1000f);
                }

                if (zSamples.Count >= 3)
                {
                    float medianZ = zSamples.OrderBy(z => z).ElementAt(zSamples.Count / 2);

                    rawPoints.Add(new System.Drawing.PointF(medianZ, y));

                    if (medianZ > maxZ)
                    {
                        maxZ = medianZ;
                        MaxZIndex = rawPoints.Count - 1;
                    }
                }
            }

            if (rawPoints.Count < 5)
            {
                sideBox.Image = sideView;
                return;
            }

            var filtered = FilterDepthPoints(rawPoints);
            var gaussianed = GaussianSmooth(filtered, 5, 2.0);
            List<System.Drawing.PointF> smoothedPoints = InterpolateSpinePoints(gaussianed);
            LastSmoothedSpinePoints = smoothedPoints;

            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (Pen spinePen = new Pen(Color.Cyan, 3))
                {
                    for (int i = 1; i < smoothedPoints.Count; i++)
                    {
                        float x1 = 50 + smoothedPoints[i - 1].X * 0.1f;
                        float y1 = smoothedPoints[i - 1].Y;
                        float x2 = 50 + smoothedPoints[i].X * 0.1f;
                        float y2 = smoothedPoints[i].Y;

                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                float deepestZ = float.MinValue;
                float deepestX = 0;

                for (int i = 0; i < smoothedPoints.Count; i++)
                {
                    if (smoothedPoints[i].X > deepestZ)
                    {
                        deepestZ = smoothedPoints[i].X;
                        deepestX = smoothedPoints[i].X;
                        MaxZIndex = i;
                    }
                }

                float refX = 50 + deepestX * 0.1f;
                FixedDeepestXPixel = refX;

                using (Pen redPen = new Pen(Color.Red, 2)
                {
                    DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
                })
                {
                    g.DrawLine(redPen, refX, 0, refX, sideView.Height);
                }

                g.DrawString($"Deepest Z: {deepestZ:F0} mm",
                    new Font("Arial", 9),
                    Brushes.White,
                    refX + 5,
                    10);
            }

            sideBox.Image?.Dispose();
            sideBox.Image = sideView;
        }

        public double CalculateSpineAngle(Body body)
        {
            if (body == null || !body.IsTracked) return double.NaN;

            Joint shoulder = body.Joints[JointType.ShoulderLeft];
            Joint spineMid = body.Joints[JointType.SpineMid];
            Joint spineBase = body.Joints[JointType.SpineBase];

            if (shoulder.TrackingState == TrackingState.NotTracked ||
                spineMid.TrackingState == TrackingState.NotTracked ||
                spineBase.TrackingState == TrackingState.NotTracked)
                return double.NaN;

            System.Numerics.Vector3 vector1 = new System.Numerics.Vector3(
                spineMid.Position.X - shoulder.Position.X,
                spineMid.Position.Y - shoulder.Position.Y,
                spineMid.Position.Z - shoulder.Position.Z);

            System.Numerics.Vector3 vector2 = new System.Numerics.Vector3(
                spineBase.Position.X - spineMid.Position.X,
                spineBase.Position.Y - spineMid.Position.Y,
                spineBase.Position.Z - spineMid.Position.Z);

            float dot = System.Numerics.Vector3.Dot(vector1, vector2);
            float mag1 = vector1.Length();
            float mag2 = vector2.Length();
            double angleRadians = Math.Acos(dot / (mag1 * mag2));
            double angleDegrees = angleRadians * (180.0 / Math.PI);

            return Math.Round(angleDegrees, 1);
        }

        public void DrawSpineOnBitmap(Body body, Bitmap bitmap)
        {
            if (body == null || coordinateMapper == null) return;

            var joints = new JointType[]
            {
                JointType.SpineBase, JointType.SpineMid, JointType.SpineShoulder,
                JointType.Neck, JointType.Head
            };

            List<System.Drawing.PointF> spinePoints2D = new List<System.Drawing.PointF>();

            foreach (var jointType in joints)
            {
                Joint joint = body.Joints[jointType];
                if (joint.TrackingState == TrackingState.NotTracked) return;

                DepthSpacePoint dp = coordinateMapper.MapCameraPointToDepthSpace(joint.Position);
                if (float.IsNaN(dp.X) || float.IsNaN(dp.Y)) return;
                if (dp.X >= 0 && dp.X < 512 && dp.Y >= 0 && dp.Y < 424)
                {
                    spinePoints2D.Add(new System.Drawing.PointF(dp.X, dp.Y));
                }
            }

            if (spinePoints2D.Count >= 2)
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                using (Pen redPen = new Pen(Color.Red, 4))
                {
                    for (int i = 0; i < spinePoints2D.Count - 1; i++)
                    {
                        g.DrawLine(redPen, spinePoints2D[i], spinePoints2D[i + 1]);
                    }
                }
            }
        }

        public Bitmap GenerateSpineCurveImageForPdf(int width, int height)
        {
            if (LastSmoothedSpinePoints == null || LastSmoothedSpinePoints.Count < 2)
                return null;

            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                float offsetX = 50f;
                float scaleX = 0.1f;
                float scaleY = height / 424f;

                using (Pen spinePen = new Pen(Color.Cyan, 4))
                {
                    for (int i = 1; i < LastSmoothedSpinePoints.Count; i++)
                    {
                        var p1 = LastSmoothedSpinePoints[i - 1];
                        var p2 = LastSmoothedSpinePoints[i];
                        float x1 = offsetX + p1.X * scaleX;
                        float y1 = p1.Y * scaleY;
                        float x2 = offsetX + p2.X * scaleX;
                        float y2 = p2.Y * scaleY;
                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                if (MaxZIndex >= 0 && MaxZIndex < LastSmoothedSpinePoints.Count)
                {
                    float deepestZ = LastSmoothedSpinePoints[MaxZIndex].X;
                    float xDeep = offsetX + deepestZ * scaleX;

                    using (Pen redDash = new Pen(Color.Red, 3) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(redDash, xDeep, 0, xDeep, height);
                    }

                    using (Font font = new Font("Segoe UI", 20, FontStyle.Bold))
                    {
                        g.DrawString($"Profondeur max : {deepestZ:F0} mm", font, Brushes.White, xDeep + 10, 20);
                    }
                }
            }

            return bmp;
        }

        public void ExportSpineCurveHighRes(string filePath, int targetWidth, int targetHeight)
        {
            if (LastSmoothedSpinePoints == null || LastSmoothedSpinePoints.Count < 2) return;

            using (var bmp = new Bitmap(targetWidth, targetHeight))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                float offsetX = 50f;
                float scaleX = 0.1f;
                float scaleY = targetHeight / 424f;

                using (Pen spinePen = new Pen(Color.Cyan, 4))
                {
                    for (int i = 1; i < LastSmoothedSpinePoints.Count; i++)
                    {
                        var p1 = LastSmoothedSpinePoints[i - 1];
                        var p2 = LastSmoothedSpinePoints[i];
                        float x1 = offsetX + p1.X * scaleX;
                        float y1 = p1.Y * scaleY;
                        float x2 = offsetX + p2.X * scaleX;
                        float y2 = p2.Y * scaleY;
                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                if (MaxZIndex >= 0 && MaxZIndex < LastSmoothedSpinePoints.Count)
                {
                    float deepestZ = LastSmoothedSpinePoints[MaxZIndex].X;
                    float xDeep = offsetX + deepestZ * scaleX;

                    using (Pen redDash = new Pen(Color.Red, 3) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(redDash, xDeep, 0, xDeep, targetHeight);
                    }

                    using (Font font = new Font("Segoe UI", 20, FontStyle.Bold))
                    {
                        g.DrawString($"Profondeur max : {deepestZ:F0} mm", font, Brushes.White, xDeep + 10, 20);
                    }
                }

                bmp.Save(filePath, ImageFormat.Png);
            }
        }

        // Helper methods
        private List<System.Drawing.PointF> FilterDepthPoints(List<System.Drawing.PointF> points)
        {
            List<System.Drawing.PointF> filtered = new List<System.Drawing.PointF>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                float x = (points[i - 1].X + points[i].X + points[i + 1].X) / 3f;
                float y = points[i].Y;
                filtered.Add(new System.Drawing.PointF(x, y));
            }
            return filtered;
        }

        private List<System.Drawing.PointF> GaussianSmooth(List<System.Drawing.PointF> raw, int radius = 3, double sigma = 1.0)
        {
            int len = raw.Count;
            var smoothed = new List<System.Drawing.PointF>(len);

            var kernel = new double[2 * radius + 1];
            double sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-0.5 * (i * i) / (sigma * sigma));
                kernel[i + radius] = v;
                sum += v;
            }
            for (int i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            for (int i = 0; i < len; i++)
            {
                double accum = 0;
                double weight = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int idx = i + k;
                    if (idx < 0 || idx >= len) continue;
                    accum += raw[idx].X * kernel[k + radius];
                    weight += kernel[k + radius];
                }
                smoothed.Add(new System.Drawing.PointF((float)(accum / weight), raw[i].Y));
            }
            return smoothed;
        }

        private List<System.Drawing.PointF> InterpolateSpinePoints(List<System.Drawing.PointF> points)
        {
            List<System.Drawing.PointF> interpolated = new List<System.Drawing.PointF>();
            for (int i = 0; i < points.Count - 3; i++)
            {
                System.Drawing.PointF p0 = points[i];
                System.Drawing.PointF p1 = points[i + 1];
                System.Drawing.PointF p2 = points[i + 2];
                System.Drawing.PointF p3 = points[i + 3];

                for (float t = 0; t <= 1; t += 0.05f)
                {
                    float t2 = t * t;
                    float t3 = t2 * t;

                    float x = 0.5f * ((2 * p1.X) + (-p0.X + p2.X) * t +
                        (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                        (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

                    float y = 0.5f * ((2 * p1.Y) + (-p0.Y + p2.Y) * t +
                        (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                        (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

                    interpolated.Add(new System.Drawing.PointF(x, y));
                }
            }
            return interpolated;
        }
    }
}