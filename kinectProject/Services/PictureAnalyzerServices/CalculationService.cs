using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using AxisType = kinectProject.AxisType;

namespace kinectProject
{
    public class CalculationService
    {
        #region Basic Geometry Calculations


        /// <summary>
        /// Calculate Euclidean distance between two points
        /// </summary>
        public double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        /// <summary>
        /// Calculate Euclidean distance between two PointF
        /// </summary>
        public double CalculateDistanceF(PointF p1, PointF p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        /// <summary>
        /// Check if two points are near each other within tolerance
        /// </summary>
        public bool IsNearPoint(Point p1, Point p2, int tolerance)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)) <= tolerance;
        }

        /// <summary>
        /// Check if a point is near a line segment within tolerance
        /// </summary>
        public bool IsPointNearLine(Point point, Point lineStart, Point lineEnd, int tolerance)
        {
            double lineLength = CalculateDistance(lineStart, lineEnd);
            if (lineLength == 0) return IsNearPoint(point, lineStart, tolerance);

            double t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * (lineEnd.X - lineStart.X) +
                 (point.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y)) /
                (lineLength * lineLength)));

            Point projection = new Point(
                (int)(lineStart.X + t * (lineEnd.X - lineStart.X)),
                (int)(lineStart.Y + t * (lineEnd.Y - lineStart.Y)));

            return IsNearPoint(point, projection, tolerance);
        }

        /// <summary>
        /// Find the closest point on a line segment to a given point
        /// </summary>
        public Point GetClosestPointOnLine(Point point, Point lineStart, Point lineEnd)
        {
            double lineLength = CalculateDistance(lineStart, lineEnd);
            if (lineLength == 0) return lineStart;

            double t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * (lineEnd.X - lineStart.X) +
                 (point.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y)) /
                (lineLength * lineLength)));

            return new Point(
                (int)(lineStart.X + t * (lineEnd.X - lineStart.X)),
                (int)(lineStart.Y + t * (lineEnd.Y - lineStart.Y)));
        }

        #endregion

        #region Angle Calculations

        /// <summary>
        /// Calculate the angle between two measurement segments at their vertex
        /// </summary>
        public double CalculateAngle(Measurement m1, Measurement m2)
        {
            if (m1.Type != MeasurementType.Angle || !m1.Vertex.HasValue ||
                m2.Type != MeasurementType.Angle || !m2.Vertex.HasValue) return 0;

            // Calculate vectors from vertex to endpoints
            Point v1 = new Point(m1.End.X - m1.Vertex.Value.X, m1.End.Y - m1.Vertex.Value.Y);
            Point v2 = new Point(m2.End.X - m2.Vertex.Value.X, m2.End.Y - m2.Vertex.Value.Y);

            return CalculateAngleBetweenVectors(v1, v2);
        }

        /// <summary>
        /// Calculate angle for a single angle measurement (finds its pair segment)
        /// </summary>
        public double CalculateAngle(Measurement m, List<Measurement> measurements)
        {
            if (m.Type != MeasurementType.Angle || !m.Vertex.HasValue) return 0;

            // Find the other segment that shares the same vertex and ID
            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                meas.Type == MeasurementType.Angle &&
                meas.Vertex.HasValue &&
                meas.Vertex.Value == m.Vertex.Value &&
                meas.ID == m.ID &&
                meas.End != m.End);

            if (otherSegment.Type == MeasurementType.Angle)
            {
                return CalculateAngle(m, otherSegment);
            }

            return 0;
        }

        /// <summary>
        /// Calculate angle between two vectors
        /// </summary>
        public double CalculateAngleBetweenVectors(Point v1, Point v2)
        {
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0) return 0;

            double cosTheta = Math.Max(-1, Math.Min(1, dotProduct / (mag1 * mag2)));

            // Returns the smaller angle between the vectors (0-180 degrees)
            return Math.Acos(cosTheta) * (180 / Math.PI);
        }

        /// <summary>
        /// Calculate angle between two PointF vectors
        /// </summary>
        public double CalculateAngleBetweenVectors(PointF v1, PointF v2)
        {
            double dot = v1.X * v2.X + v1.Y * v2.Y;
            double cross = v1.X * v2.Y - v1.Y * v2.X;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0) return 0;

            double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
            double angleRad = Math.Acos(cosTheta);
            double angleDeg = angleRad * (180 / Math.PI);

            return angleDeg;
        }

        /// <summary>
        /// Calculate angle of a measurement relative to an axis
        /// </summary>
        public double CalculateAngleWithAxis(Measurement m)
        {
            if (m.Type != MeasurementType.AngleWithAxis || !m.Axis.HasValue) return 0;

            double dx = m.End.X - m.Start.X;
            double dy = m.End.Y - m.Start.Y;

            AxisType axis = m.Axis.Value; // Extract non-nullable value

            if (axis == AxisType.X)
                return Math.Abs(Math.Atan2(dy, dx) * (180 / Math.PI));
            else
                return Math.Abs(Math.Atan2(dx, dy) * (180 / Math.PI));
        }

        /// <summary>
        /// Calculate the angle of a line in degrees (0-360)
        /// </summary>
        public double CalculateLineAngle(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double angle = Math.Atan2(dy, dx) * (180 / Math.PI);
            if (angle < 0) angle += 360;
            return angle;
        }

        #endregion

        #region Perpendicular Calculations

        /// <summary>
        /// Calculate the perpendicular foot point from a point onto a line
        /// </summary>
        public Point CalculatePerpendicularFoot(Point point, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0) return lineStart;

            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared;

            // Clamp t to the segment [0, 1]
            t = Math.Max(0, Math.Min(1, t));

            return new Point(
                (int)(lineStart.X + t * dx),
                (int)(lineStart.Y + t * dy));
        }

        /// <summary>
        /// Calculate the perpendicular foot from a measurement base line
        /// </summary>
        public Point CalculatePerpendicularFoot(Measurement baseLine, Point point)
        {
            Point A, B;

            if (baseLine.Type == MeasurementType.Angle && baseLine.Vertex.HasValue)
            {
                A = baseLine.Vertex.Value;
                B = baseLine.End;
            }
            else
            {
                A = baseLine.Start;
                B = baseLine.End;
            }

            Point foot = CalculatePerpendicularFoot(point, A, B);

            // For angle segments, allow perpendiculars beyond the segment
            if (baseLine.Type != MeasurementType.Angle)
            {
                return foot;
            }

            // For angles, don't clamp - return the actual projection
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (Math.Abs(lengthSquared) < 0.0001) return A;

            double t = ((point.X - A.X) * dx + (point.Y - A.Y) * dy) / lengthSquared;
            t = Math.Max(-2, Math.Min(3, t));

            return new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy));
        }

        #endregion

        #region Intersection Calculations

        /// <summary>
        /// Find intersection point of two line segments
        /// </summary>
        public Point? FindLineIntersection(Point p1, Point p2, Point p3, Point p4)
        {
            float denom = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);

            if (Math.Abs(denom) < 0.0001)
                return null; // Parallel lines

            float ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / denom;
            float ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / denom;

            // Check if intersection is within both segments
            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                int x = (int)(p1.X + ua * (p2.X - p1.X));
                int y = (int)(p1.Y + ua * (p2.Y - p1.Y));
                return new Point(x, y);
            }

            return null;
        }

        /// <summary>
        /// Ray casting helper for point-in-polygon test
        /// </summary>
        public bool IsIntersecting(int px, int py, Point p1, Point p2)
        {
            if (p1.Y > py && p2.Y > py) return false;
            if (p1.Y < py && p2.Y < py) return false;
            if (p1.X < px && p2.X < px) return false;

            double xIntersect = p1.X + (double)(py - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
            return xIntersect > px;
        }

        #endregion

        #region Transform Helpers

        /// <summary>
        /// Transform screen point to image coordinates
        /// </summary>
        public PointF TransformPointToImage(PointF screenPoint, Matrix inverseTransform)
        {
            PointF[] points = new PointF[] { screenPoint };
            inverseTransform.TransformPoints(points);
            return points[0];
        }

        /// <summary>
        /// Transform image point to screen coordinates
        /// </summary>
        public PointF TransformPointToScreen(PointF imagePoint, Matrix transformMatrix)
        {
            PointF[] points = new PointF[] { imagePoint };
            transformMatrix.TransformPoints(points);
            return points[0];
        }

        #endregion

        #region Point Validation

        /// <summary>
        /// Check if a Point has valid coordinates (not NaN or Infinity)
        /// </summary>
        public bool IsValidPoint(Point point)
        {
            return !float.IsNaN(point.X) && !float.IsNaN(point.Y) &&
                   !float.IsInfinity(point.X) && !float.IsInfinity(point.Y);
        }

        /// <summary>
        /// Check if a PointF has valid coordinates
        /// </summary>
        public bool IsValidPoint(PointF point)
        {
            return !float.IsNaN(point.X) && !float.IsNaN(point.Y) &&
                   !float.IsInfinity(point.X) && !float.IsInfinity(point.Y);
        }

        #endregion

        #region Statistics Helpers

        /// <summary>
        /// Calculate median of a list of doubles
        /// </summary>
        public double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count == 0) return 0;
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            else
                return sorted[count / 2];
        }

        /// <summary>
        /// Calculate mean of a list of doubles
        /// </summary>
        public double CalculateMean(List<double> values)
        {
            if (values.Count == 0) return 0;
            return values.Average();
        }

        /// <summary>
        /// Calculate standard deviation
        /// </summary>
        public double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count == 0) return 0;
            double mean = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        #endregion

        #region Midpoint and Centroid Calculations

        /// <summary>
        /// Calculate midpoint between two points
        /// </summary>
        public Point CalculateMidpoint(Point p1, Point p2)
        {
            return new Point(
                (p1.X + p2.X) / 2,
                (p1.Y + p2.Y) / 2);
        }

        /// <summary>
        /// Calculate midpoint between two PointF
        /// </summary>
        public PointF CalculateMidpointF(PointF p1, PointF p2)
        {
            return new PointF(
                (p1.X + p2.X) / 2f,
                (p1.Y + p2.Y) / 2f);
        }

        /// <summary>
        /// Calculate centroid of a list of points
        /// </summary>
        public Point CalculateCentroid(List<Point> points)
        {
            if (points.Count == 0) return Point.Empty;

            double sumX = 0, sumY = 0;
            foreach (var p in points)
            {
                sumX += p.X;
                sumY += p.Y;
            }

            return new Point(
                (int)(sumX / points.Count),
                (int)(sumY / points.Count));
        }

        #endregion

        #region Scale Conversion

        /// <summary>
        /// Convert pixel distance to real-world units
        /// </summary>
        public double PixelToReal(double pixels, float pixelToRealRatio)
        {
            if (pixelToRealRatio <= 0) return 0;
            return pixels / pixelToRealRatio;
        }

        /// <summary>
        /// Convert real-world units to pixel distance
        /// </summary>
        public double RealToPixel(double realUnits, float pixelToRealRatio)
        {
            return realUnits * pixelToRealRatio;
        }

        /// <summary>
        /// Set scale from reference line
        /// </summary>
        public float CalculatePixelToRealRatio(Point start, Point end, float referenceLength)
        {
            double pixelLength = CalculateDistance(start, end);
            if (referenceLength > 0 && pixelLength > 0)
            {
                return (float)(pixelLength / referenceLength);
            }
            return 1.0f;
        }

        #endregion

        #region Zoom Helpers

        /// <summary>
        /// Calculate zoom factor to fit image in viewport
        /// </summary>
        public float CalculateFitZoom(Size imageSize, Size viewportSize)
        {
            float scaleX = (float)viewportSize.Width / imageSize.Width;
            float scaleY = (float)viewportSize.Height / imageSize.Height;
            return Math.Min(scaleX, scaleY) * 0.95f;
        }

        /// <summary>
        /// Clamp zoom factor to valid range
        /// </summary>
        public float ClampZoom(float zoom)
        {
            return Math.Max(0.1f, Math.Min(20f, zoom));
        }

        #endregion

        #region Angle Arc Helpers

        /// <summary>
        /// Calculate start and sweep angles for drawing an arc between three points
        /// </summary>
        public void CalculateArcAngles(PointF vertex, PointF point1, PointF point2,
            out float startAngle, out float sweepAngle)
        {
            PointF v1 = new PointF(point1.X - vertex.X, point1.Y - vertex.Y);
            PointF v2 = new PointF(point2.X - vertex.X, point2.Y - vertex.Y);

            double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
            double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

            if (angle1 < 0) angle1 += 360;
            if (angle2 < 0) angle2 += 360;

            startAngle = (float)Math.Min(angle1, angle2);
            sweepAngle = (float)Math.Abs(angle1 - angle2);

            if (sweepAngle > 180)
            {
                startAngle = (float)Math.Max(angle1, angle2);
                sweepAngle = 360 - sweepAngle;
            }
        }

        /// <summary>
        /// Calculate the position for angle text at mid-arc
        /// </summary>
        public PointF CalculateAngleTextPosition(PointF vertex, float radius,
            float startAngle, float sweepAngle)
        {
            double midAngle = (startAngle + sweepAngle / 2) * Math.PI / 180;
            return new PointF(
                vertex.X + (float)(radius * 1.4 * Math.Cos(midAngle)),
                vertex.Y + (float)(radius * 1.4 * Math.Sin(midAngle)));
        }

        #endregion
    }
}