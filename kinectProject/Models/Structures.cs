using System;
using System.Collections.Generic;
using System.Drawing;

namespace kinectProject
{
    #region Detection Structures

    /// <summary>
    /// Represents a detected point from color detection
    /// </summary>
    public struct DetectedPoint
    {
        public Point Location;
        public PointColor Color;
        public double Confidence;
        public int Radius;
        public int ID;

        public DetectedPoint(Point location, PointColor color, double confidence, int radius, int id)
        {
            Location = location;
            Color = color;
            Confidence = confidence;
            Radius = radius;
            ID = id;
        }
    }

    /// <summary>
    /// Represents a body landmark for posture analysis
    /// </summary>
    public struct BodyLandmark
    {
        public string Name;
        public Point Location;
        public List<string> ConnectedTo;

        public BodyLandmark(string name, Point location)
        {
            Name = name;
            Location = location;
            ConnectedTo = new List<string>();
        }
    }

    #endregion

    #region Color Structure

    /// <summary>
    /// HSV color representation
    /// </summary>
    public struct HsvColor
    {
        public float H; // Hue: 0-360
        public float S; // Saturation: 0-1
        public float V; // Value: 0-1

        public HsvColor(float h, float s, float v)
        {
            H = h;
            S = s;
            V = v;
        }

        public override string ToString()
        {
            return $"H={H:F1}°, S={S:F2}, V={V:F2}";
        }
    }

    #endregion

    #region Measurement Structure

    /// <summary>
    /// Represents a measurement on the image
    /// </summary>
    public struct Measurement
    {
        public Point Start;
        public Point End;
        public string Name;
        public MeasurementType Type;
        public bool IsSelected;
        public AxisType? Axis;
        public Point? Vertex;
        public int ID;

        // Angle-specific fields
        public double? AngleValue;
        public List<int> RelatedLineIDs;

        public Measurement(Point start, Point end, string name, MeasurementType type, int id)
        {
            Start = start;
            End = end;
            Name = name;
            Type = type;
            IsSelected = false;
            Axis = null;
            Vertex = null;
            ID = id;
            AngleValue = null;
            RelatedLineIDs = new List<int>();
        }

        /// <summary>
        /// Create an intersection angle measurement
        /// </summary>
        public static Measurement CreateIntersectionAngle(
            string name, int id, Point vertex,
            double angleValue, int line1Id, int line2Id)
        {
            var measurement = new Measurement(vertex, vertex, name, MeasurementType.Angle, id);
            measurement.Vertex = vertex;
            measurement.AngleValue = angleValue;
            measurement.RelatedLineIDs.Add(line1Id);
            measurement.RelatedLineIDs.Add(line2Id);
            return measurement;
        }

        public override string ToString()
        {
            return $"{Name} (ID:{ID}) - {Type}";
        }
    }

    #endregion

    #region Intersection Structure

    /// <summary>
    /// Represents an intersection point between lines
    /// </summary>
    public struct IntersectionPoint
    {
        public Point Location;
        public List<int> LineIDs;
        public IntersectionType Type;
        public List<Tuple<int, int, double>> Angles; // (LineID1, LineID2, Angle)
        public int ID;

        public IntersectionPoint(Point location, int id)
        {
            Location = location;
            LineIDs = new List<int>();
            Type = IntersectionType.None;
            Angles = new List<Tuple<int, int, double>>();
            ID = id;
        }

        public override string ToString()
        {
            return $"P{ID} - {Type} - {LineIDs.Count} lines - ({Location.X}, {Location.Y})";
        }
    }

    #endregion
}