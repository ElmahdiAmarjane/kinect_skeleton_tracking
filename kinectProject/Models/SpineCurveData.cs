using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace kinectProject
{
    /// <summary>
    /// Represents a captured spine curve with all metadata
    /// </summary>
    public class SpineCurveData
    {
        public DateTime CaptureTime { get; set; }
        public List<PointFData> Points { get; set; } = new List<PointFData>();
        public int MaxZIndex { get; set; } = -1;
        public float ManualZRef { get; set; } = -1;
        public float FixedDeepestXPixel { get; set; } = -1;
        public double SpineAngle { get; set; }
        public string PatientIdentifier { get; set; } = "Unknown";
        public string FilePath { get; set; }

        // Original scaling factors used when captured
        public float OriginalOffsetX { get; set; } = 50f;
        public float OriginalScaleX { get; set; } = 0.1f;

        /// <summary>
        /// Convert Points back to System.Drawing.PointF list
        /// </summary>
        public List<PointF> GetPointFList()
        {
            return Points?.Select(p => p.ToPointF()).ToList() ?? new List<PointF>();
        }

        public override string ToString()
        {
            return $"Curve: {CaptureTime:dd/MM/yyyy HH:mm} - {Points?.Count ?? 0} points";
        }
    }

    /// <summary>
    /// Serializable version of PointF for JSON export/import
    /// </summary>
    public struct PointFData
    {
        public float X { get; set; }
        public float Y { get; set; }

        public PointFData(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static PointFData FromPointF(PointF point)
        {
            return new PointFData(point.X, point.Y);
        }

        public PointF ToPointF()
        {
            return new PointF(X, Y);
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }
    }
}