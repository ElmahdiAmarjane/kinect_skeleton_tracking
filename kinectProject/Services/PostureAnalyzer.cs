// PostureAnalyzer.cs
// C# 7.3 – .NET Framework 4.7.2

using System;
using System.Collections.Generic;
using Microsoft.Kinect;

namespace kinectProject
{
    /// <summary>
    /// Single-class posture analyzer for Kinect v2.
    /// Initializes sensor, reads multi-source frames (Color/Depth/Body),
    /// extracts joints and computes postural metrics similar to Kineod summaries.
    /// </summary>
    public class PostureAnalyzer : IDisposable
    {
        // ---------------------------
        // Public report (read-only)
        // ---------------------------
        public class PostureReport
        {
            // Frontal
            public double ShoulderAsymmetryMm;
            public double ShoulderAsymmetryDeg;
            public double PelvisTiltMm;
            public double PelvisTiltDeg;
            public double KneeAsymmetryMm;
            public double FootAsymmetryMm;
            public double SpineDeviationMm;   // mean lateral deviation from vertical midline
            public double GlobalTiltDeg;      // whole-body lateral tilt

            // Sagittal
            public double KyphosisAngleDeg;   // Neck–SpineShoulder–SpineMid
            public double LordosisAngleDeg;   // SpineShoulder–SpineMid–SpineBase
            public double SagittalTiltDeg;    // trunk inclination (neck to base vs vertical)

            // Depth / Morphometry (simplified)
            public double ThoracicDepthMm;    // peak forward curvature depth
            public double LumbarDepthMm;      // peak backward curvature depth
            public double SpineLengthMm;      // polyline length Neck->SpineShoulder->SpineMid->SpineBase

            // Meta
            public bool HasTrackedBody;
            public DateTime TimestampUtc;
        }

        // ---------------------------
        // Kinect fields
        // ---------------------------
        private KinectSensor _sensor;
        private MultiSourceFrameReader _reader;
        private CoordinateMapper _mapper;

        private Body[] _bodies;
        private ushort[] _depthData;          // last depth frame
        private int _depthWidth;
        private int _depthHeight;

        // Last computed report
        private PostureReport _lastReport;

        // ---------------------------
        // API
        // ---------------------------
        public bool InitializeKinect()
        {
            _sensor = KinectSensor.GetDefault();
            if (_sensor == null) return false;

            _mapper = _sensor.CoordinateMapper;

            FrameDescription depthDesc = _sensor.DepthFrameSource.FrameDescription;
            _depthWidth = depthDesc.Width;
            _depthHeight = depthDesc.Height;
            _depthData = new ushort[_depthWidth * _depthHeight];

            _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Body | FrameSourceTypes.Depth);
            _reader.MultiSourceFrameArrived += OnMultiSourceFrameArrived;

            if (!_sensor.IsOpen) _sensor.Open();

            _bodies = null;
            _lastReport = new PostureReport();
            return true;
        }

        public void StopKinect()
        {
            if (_reader != null)
            {
                _reader.MultiSourceFrameArrived -= OnMultiSourceFrameArrived;
                _reader.Dispose();
                _reader = null;
            }
            if (_sensor != null && _sensor.IsOpen) _sensor.Close();
            _sensor = null;
            _mapper = null;
        }

        public PostureReport GenerateReport()
        {
            // Return a shallow copy to avoid external mutation
            PostureReport r = new PostureReport();
            CopyReport(_lastReport, r);
            return r;
        }

        public void Dispose()
        {
            StopKinect();
        }

        // ---------------------------
        // Frame processing
        // ---------------------------
        private void OnMultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame frame = e.FrameReference.AcquireFrame();
            if (frame == null) return;

            // Depth
            try
            {
                using (DepthFrame df = frame.DepthFrameReference.AcquireFrame())
                {
                    if (df != null)
                    {
                        FrameDescription d = df.FrameDescription;
                        if (d.Width == _depthWidth && d.Height == _depthHeight)
                        {
                            df.CopyFrameDataToArray(_depthData);
                        }
                    }
                }
            }
            catch
            {
                // ignore depth copy errors for stability
            }

            // Bodies
            using (BodyFrame bf = frame.BodyFrameReference.AcquireFrame())
            {
                if (bf == null) return;

                if (_bodies == null || _bodies.Length != bf.BodyCount)
                {
                    _bodies = new Body[bf.BodyCount];
                }
                bf.GetAndRefreshBodyData(_bodies);

                Body tracked = null;
                int i;
                for (i = 0; i < _bodies.Length; i++)
                {
                    if (_bodies[i] != null && _bodies[i].IsTracked)
                    {
                        tracked = _bodies[i];
                        break;
                    }
                }

                if (tracked == null)
                {
                    _lastReport = new PostureReport();
                    _lastReport.HasTrackedBody = false;
                    _lastReport.TimestampUtc = DateTime.UtcNow;
                    return;
                }

                PostureReport report = ComputeReport(tracked);
                report.HasTrackedBody = true;
                report.TimestampUtc = DateTime.UtcNow;
                _lastReport = report;
            }
        }

        // ---------------------------
        // Computations
        // ---------------------------
        private PostureReport ComputeReport(Body body)
        {
            PostureReport r = new PostureReport();

            // Extract required joints in CameraSpace (meters)
            CameraSpacePoint shoulderL = GetJoint(body, JointType.ShoulderLeft);
            CameraSpacePoint shoulderR = GetJoint(body, JointType.ShoulderRight);
            CameraSpacePoint hipL = GetJoint(body, JointType.HipLeft);
            CameraSpacePoint hipR = GetJoint(body, JointType.HipRight);
            CameraSpacePoint kneeL = GetJoint(body, JointType.KneeLeft);
            CameraSpacePoint kneeR = GetJoint(body, JointType.KneeRight);
            CameraSpacePoint ankleL = GetJoint(body, JointType.AnkleLeft);
            CameraSpacePoint ankleR = GetJoint(body, JointType.AnkleRight);
            CameraSpacePoint footL = GetJoint(body, JointType.FootLeft);
            CameraSpacePoint footR = GetJoint(body, JointType.FootRight);
            CameraSpacePoint neck = GetJoint(body, JointType.Neck);
            CameraSpacePoint spineShoulder = GetJoint(body, JointType.SpineShoulder);
            CameraSpacePoint spineMid = GetJoint(body, JointType.SpineMid);
            CameraSpacePoint spineBase = GetJoint(body, JointType.SpineBase);

            // Safety: if any essential joint is not tracked (X = NaN), abort with empty report
            if (double.IsNaN(neck.X) || double.IsNaN(spineBase.X))
            {
                return r;
            }

            // ----- Frontal plane metrics (Y is vertical, X is lateral) -----
            r.ShoulderAsymmetryMm = Math.Abs(shoulderL.Y - shoulderR.Y) * 1000.0;
            r.ShoulderAsymmetryDeg = HorizontalLineTiltDeg(shoulderL, shoulderR);

            r.PelvisTiltMm = Math.Abs(hipL.Y - hipR.Y) * 1000.0;
            r.PelvisTiltDeg = HorizontalLineTiltDeg(hipL, hipR);

            r.KneeAsymmetryMm = Math.Abs(kneeL.Y - kneeR.Y) * 1000.0;
            r.FootAsymmetryMm = Math.Abs(footL.Y - footR.Y) * 1000.0;

            // Vertical midline = midpoint between hips projected vertically
            CameraSpacePoint hipsMid = MidPoint(hipL, hipR);
            // Mean lateral deviation of neck, spineShoulder, spineMid, spineBase from hipsMid.X
            double[] lateral = new double[4];
            lateral[0] = neck.X - hipsMid.X;
            lateral[1] = spineShoulder.X - hipsMid.X;
            lateral[2] = spineMid.X - hipsMid.X;
            lateral[3] = spineBase.X - hipsMid.X;

            double sumAbs = 0.0;
            int k;
            for (k = 0; k < lateral.Length; k++) sumAbs += Math.Abs(lateral[k]);
            r.SpineDeviationMm = (sumAbs / 4.0) * 1000.0;

            // Global lateral tilt (neck->spineBase vs gravity Y axis)
            r.GlobalTiltDeg = LineTiltFromVerticalDeg(spineBase, neck);

            // ----- Sagittal plane metrics (X lateral, Z depth forward; use Y vertical) -----
            // Kyphosis: Neck–SpineShoulder–SpineMid
            r.KyphosisAngleDeg = AngleABC_Deg(neck, spineShoulder, spineMid);
            // Lordosis: SpineShoulder–SpineMid–SpineBase
            r.LordosisAngleDeg = AngleABC_Deg(spineShoulder, spineMid, spineBase);
            // Sagittal tilt: trunk inclination in ZY plane (project line spineBase->neck)
            r.SagittalTiltDeg = SagittalInclinationDeg(spineBase, neck);

            // ----- Depth profile (simplified) -----
            DepthProfileMetrics depth = ComputeDepthProfile(spineMid);
            r.ThoracicDepthMm = depth.ThoracicDepthMm;
            r.LumbarDepthMm = depth.LumbarDepthMm;

            // Spine length (polyline)
            double lengthM = Distance(neck, spineShoulder) + Distance(spineShoulder, spineMid) + Distance(spineMid, spineBase);
            r.SpineLengthMm = lengthM * 1000.0;

            return r;
        }

        // ---------------------------
        // Helpers: joints & math
        // ---------------------------
        private CameraSpacePoint GetJoint(Body body, JointType jt)
        {
            Joint j = body.Joints[jt];
            CameraSpacePoint p = j.Position;
            // Normalize if not tracked
            if (j.TrackingState == TrackingState.NotTracked)
            {
                p.X =(float) double.NaN;
                p.Y = (float)double.NaN;
                p.Z = (float) double.NaN;
            }
            return p;
        }

        private static CameraSpacePoint MidPoint(CameraSpacePoint a, CameraSpacePoint b)
        {
            CameraSpacePoint m = new CameraSpacePoint();
            m.X = (a.X + b.X) * 0.5f;
            m.Y = (a.Y + b.Y) * 0.5f;
            m.Z = (a.Z + b.Z) * 0.5f;
            return m;
        }

        private static double Distance(CameraSpacePoint a, CameraSpacePoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // Angle ABC where B is the vertex (full 3D)
        private static double AngleABC_Deg(CameraSpacePoint a, CameraSpacePoint b, CameraSpacePoint c)
        {
            double v1x = a.X - b.X; double v1y = a.Y - b.Y; double v1z = a.Z - b.Z;
            double v2x = c.X - b.X; double v2y = c.Y - b.Y; double v2z = c.Z - b.Z;

            double d1 = Math.Sqrt(v1x * v1x + v1y * v1y + v1z * v1z);
            double d2 = Math.Sqrt(v2x * v2x + v2y * v2y + v2z * v2z);
            if (d1 < 1e-6 || d2 < 1e-6) return 0.0;

            double dot = v1x * v2x + v1y * v2y + v1z * v2z;
            double cos = dot / (d1 * d2);
            if (cos > 1.0) cos = 1.0;
            if (cos < -1.0) cos = -1.0;
            double rad = Math.Acos(cos);
            return rad * 180.0 / Math.PI;
        }

        // Tilt in frontal plane: angle between the segment AB and horizontal (X axis), measured in degrees
        private static double HorizontalLineTiltDeg(CameraSpacePoint a, CameraSpacePoint b)
        {
            double dy = a.Y - b.Y;
            double dx = a.X - b.X;
            double rad = Math.Atan2(dy, Math.Abs(dx) < 1e-6 ? 1e-6 : dx);
            return Math.Abs(rad * 180.0 / Math.PI);
        }

        // Tilt from vertical (frontal), line from base->top
        private static double LineTiltFromVerticalDeg(CameraSpacePoint baseP, CameraSpacePoint topP)
        {
            double dy = topP.Y - baseP.Y;     // vertical
            double dx = topP.X - baseP.X;     // lateral
            double rad = Math.Atan2(Math.Abs(dx), Math.Abs(dy) < 1e-6 ? 1e-6 : dy); // angle to vertical
            return rad * 180.0 / Math.PI;
        }

        // Sagittal inclination: angle in YZ plane between base->top and vertical axis
        private static double SagittalInclinationDeg(CameraSpacePoint baseP, CameraSpacePoint topP)
        {
            double dy = topP.Y - baseP.Y;
            double dz = topP.Z - baseP.Z; // forward/backward
            double rad = Math.Atan2(Math.Abs(dz), Math.Abs(dy) < 1e-6 ? 1e-6 : dy);
            return rad * 180.0 / Math.PI;
        }

        // ---------------------------
        // Depth profile (simplified)
        // ---------------------------
        private struct DepthProfileMetrics
        {
            public double ThoracicDepthMm;
            public double LumbarDepthMm;
        }

        private DepthProfileMetrics ComputeDepthProfile(CameraSpacePoint spineMid)
        {
            DepthProfileMetrics m = new DepthProfileMetrics();
            if (_depthData == null || _mapper == null) return m;

            // Map spineMid to depth space to get an approximate column to sample
            DepthSpacePoint dsp = _mapper.MapCameraPointToDepthSpace(spineMid);
            int xCenter = (int)Math.Round(dsp.X);
            if (xCenter < 0 || xCenter >= _depthWidth) return m;

            // Sample a vertical profile around xCenter (median of few columns to reduce noise)
            int half = 3; // 7 columns total
            int y;
            List<int> samples = new List<int>();
            for (y = 0; y < _depthHeight; y++)
            {
                // median of columns xCenter-3..xCenter+3
                List<int> rowVals = new List<int>();
                int dx;
                for (dx = -half; dx <= half; dx++)
                {
                    int x = xCenter + dx;
                    if (x >= 0 && x < _depthWidth)
                    {
                        int idx = y * _depthWidth + x;
                        int val = _depthData[idx];
                        if (val > 500 && val < 8000) rowVals.Add(val); // valid depth in mm
                    }
                }
                if (rowVals.Count > 0)
                {
                    rowVals.Sort();
                    int median = rowVals[rowVals.Count / 2];
                    samples.Add(median);
                }
                else
                {
                    samples.Add(0);
                }
            }

            // Smooth with simple moving average (window 9)
            List<double> smooth = new List<double>(samples.Count);
            int win = 9;
            int i;
            for (i = 0; i < samples.Count; i++)
            {
                int start = i - win / 2;
                int end = i + win / 2;
                if (start < 0) start = 0;
                if (end >= samples.Count) end = samples.Count - 1;
                int j;
                double acc = 0.0;
                int cnt = 0;
                for (j = start; j <= end; j++)
                {
                    if (samples[j] > 0) { acc += samples[j]; cnt++; }
                }
                if (cnt == 0) smooth.Add(0.0);
                else smooth.Add(acc / cnt);
            }

            // Find thoracic "peak" (more posterior -> larger depth value) above spineMid Y,
            // and lumbar "valley"/lesser depth below spineMid Y. We need spineMid depth-space Y:
            int yMid = (int)Math.Round(dsp.Y);
            if (yMid < 0) yMid = 0;
            if (yMid >= smooth.Count) yMid = smooth.Count - 1;

            // Search windows
            int topStart = Math.Max(0, yMid - 160); // ~ upper back
            int topEnd = yMid - 10;
            int botStart = yMid + 10;
            int botEnd = Math.Min(smooth.Count - 1, yMid + 200);

            double maxTop = 0.0;
            double minBot = 0.0;
            bool hasTop = false;
            bool hasBot = false;

            for (i = topStart; i <= topEnd; i++)
            {
                double v = smooth[i];
                if (v <= 0.0) continue;
                if (!hasTop || v > maxTop) { maxTop = v; hasTop = true; }
            }
            for (i = botStart; i <= botEnd; i++)
            {
                double v = smooth[i];
                if (v <= 0.0) continue;
                if (!hasBot || v < minBot) { minBot = v; hasBot = true; }
            }

            // Convert relative depths to mm difference w.r.t. depth at yMid
            double refDepth = 0.0;
            if (yMid >= 0 && yMid < smooth.Count) refDepth = smooth[yMid];

            if (hasTop && refDepth > 0.0) m.ThoracicDepthMm = Math.Abs(maxTop - refDepth);
            if (hasBot && refDepth > 0.0) m.LumbarDepthMm = Math.Abs(refDepth - minBot);

            return m;
        }

        // ---------------------------
        // Utils
        // ---------------------------
        private static void CopyReport(PostureReport src, PostureReport dst)
        {
            if (src == null || dst == null) return;

            dst.ShoulderAsymmetryMm = src.ShoulderAsymmetryMm;
            dst.ShoulderAsymmetryDeg = src.ShoulderAsymmetryDeg;
            dst.PelvisTiltMm = src.PelvisTiltMm;
            dst.PelvisTiltDeg = src.PelvisTiltDeg;
            dst.KneeAsymmetryMm = src.KneeAsymmetryMm;
            dst.FootAsymmetryMm = src.FootAsymmetryMm;
            dst.SpineDeviationMm = src.SpineDeviationMm;
            dst.GlobalTiltDeg = src.GlobalTiltDeg;

            dst.KyphosisAngleDeg = src.KyphosisAngleDeg;
            dst.LordosisAngleDeg = src.LordosisAngleDeg;
            dst.SagittalTiltDeg = src.SagittalTiltDeg;

            dst.ThoracicDepthMm = src.ThoracicDepthMm;
            dst.LumbarDepthMm = src.LumbarDepthMm;
            dst.SpineLengthMm = src.SpineLengthMm;

            dst.HasTrackedBody = src.HasTrackedBody;
            dst.TimestampUtc = src.TimestampUtc;
        }
    }
}
