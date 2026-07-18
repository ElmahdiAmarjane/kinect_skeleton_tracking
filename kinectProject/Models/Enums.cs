using System;

namespace kinectProject
{
    #region Detection Enums

    /// <summary>
    /// Detection mode for point/sticker detection
    /// </summary>
    public enum DetectionMode
    {
        None,
        SinglePoint,
        MultiplePoints,
        BodyContour,
        ManualPick,
        Automatic
    }

    /// <summary>
    /// Predefined colors for point detection
    /// </summary>
    public enum PointColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        White,
        Custom
    }

    #endregion

    #region Tool and Edit Enums

    /// <summary>
    /// Available measurement tools
    /// </summary>
    public enum ToolMode
    {
        None,
        Line,
        Point,
        Angle,
        AngleWithAxis,
        Distance,
        Reference,
        Perpendicular
    }

    /// <summary>
    /// Available edit modes
    /// </summary>
    public enum EditMode
    {
        None,
        Move,
        Delete,
        Rename,
        Normal
    }

    #endregion

    #region Axis Enums

    /// <summary>
    /// Axis type for angle measurements
    /// </summary>
    public enum AxisType
    {
        X,
        Y
    }

    #endregion

    #region Measurement Enums

    /// <summary>
    /// Types of measurements
    /// </summary>
    public enum MeasurementType
    {
        Line,
        Point,
        Angle,
        AngleWithAxis,
        Distance,
        ReferenceLine,
        PerpendicularLine,
        None
    }

    #endregion

    #region Intersection Enums

    /// <summary>
    /// Types of intersection points
    /// </summary>
    public enum IntersectionType
    {
        Exact,
        Proximity,
        Terminal,
        None
    }

    #endregion
}