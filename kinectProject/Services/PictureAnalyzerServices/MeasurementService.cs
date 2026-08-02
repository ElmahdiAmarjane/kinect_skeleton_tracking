using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static kinectProject.BodyPictureAnalyzer;

namespace kinectProject
{
    public class MeasurementService
    {
        private CalculationService calcService;

        public MeasurementService()
        {
            calcService = new CalculationService();
        }

        #region Measurement Creation

        /// <summary>
        /// Create a line measurement between two points
        /// </summary>
        public Measurement CreateLineMeasurement(Point start, Point end, string name, int id)
        {
            return new Measurement(start, end, name, MeasurementType.Line, id);
        }

        /// <summary>
        /// Create a point measurement
        /// </summary>
        public Measurement CreatePointMeasurement(Point location, string name, int id)
        {
            return new Measurement(location, location, name, MeasurementType.Point, id);
        }

        /// <summary>
        /// Create a distance measurement
        /// </summary>
        public Measurement CreateDistanceMeasurement(Point start, Point end, string name, int id)
        {
            return new Measurement(start, end, name, MeasurementType.Distance, id);
        }

        /// <summary>
        /// Create an angle measurement segment
        /// </summary>
        public Measurement CreateAngleMeasurement(Point vertex, Point end, string name, int id)
        {
            Measurement m = new Measurement(vertex, end, name, MeasurementType.Angle, id);
            m.Vertex = vertex;
            return m;
        }

        /// <summary>
        /// Create an angle-with-axis measurement
        /// </summary>
        public Measurement CreateAngleWithAxisMeasurement(Point start, Point end, string name, int id, AxisType? axis)
        {
            Measurement m = new Measurement(start, end, name, MeasurementType.AngleWithAxis, id);
            m.Axis = axis;
            return m;
        }

        /// <summary>
        /// Create a reference line measurement
        /// </summary>
        public Measurement CreateReferenceMeasurement(Point start, Point end, string name, int id)
        {
            return new Measurement(start, end, name, MeasurementType.ReferenceLine, id);
        }

        /// <summary>
        /// Create a perpendicular line from a base line to a point
        /// </summary>
        public Measurement CreatePerpendicularLine(
            Measurement baseLine,
            Point endPoint,
            int id,
            string name,
            out Point perpendicularFoot)
        {
            Point A, B;

            // Handle different line types
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

            Point C = endPoint;

            // Calculate the perpendicular projection
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (Math.Abs(lengthSquared) < 0.0001)
            {
                perpendicularFoot = A;
                return new Measurement(perpendicularFoot, C, name, MeasurementType.PerpendicularLine, id);
            }

            // Calculate projection parameter t
            double t = ((C.X - A.X) * dx + (C.Y - A.Y) * dy) / lengthSquared;

            // For angle segments, allow perpendiculars beyond the segment
            if (baseLine.Type == MeasurementType.Angle)
            {
                t = Math.Max(-2, Math.Min(3, t));
            }
            else
            {
                t = Math.Max(0, Math.Min(1, t));
            }

            // Calculate the perpendicular foot point
            perpendicularFoot = new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy)
            );

            // Only create if the perpendicular line has minimum length
            if (calcService.CalculateDistance(perpendicularFoot, C) > 5)
            {
                return new Measurement(perpendicularFoot, C, name, MeasurementType.PerpendicularLine, id);
            }

            return default(Measurement);
        }

        /// <summary>
        /// Create a measurement from detected points
        /// </summary>
        public void CreateMeasurementsFromDetectedPoints(
      List<DetectedPoint> detectedPoints,
      List<Measurement> measurements,
      ref int idCounter,
      ref bool autoRenameDisabled)
        {
            // Get the next available point number
            int existingPointCount = measurements.Count(m => m.Type == MeasurementType.Point);

            foreach (var detectedPoint in detectedPoints)
            {
                // Use continuous numbering
                string pointName = $"P{existingPointCount + 1}";
                existingPointCount++;

                if (!autoRenameDisabled)
                {
                    using (var renameDialog = new AutoRenameDialog(pointName))
                    {
                        if (renameDialog.ShowDialog() == DialogResult.OK)
                        {
                            pointName = string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                                       pointName : renameDialog.NewName.Trim();

                            if (renameDialog.DontAskAgain)
                            {
                                autoRenameDisabled = true;
                            }
                        }
                        // If Cancel, keep default name
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
        /// <summary>
        /// Create a line between two detected points
        /// </summary>
        public void CreateLineBetweenPoints(
            Point point1,
            DetectedPoint point2,
            List<Measurement> measurements,
            List<DetectedPoint> detectedPoints,
            ref int idCounter,
            ref int measurementCounter)
        {
            // Find IDs of the points
            int point1Id = 0;
            int point2Id = point2.ID;

            // Search for point1 in measurements
            foreach (var measurement in measurements)
            {
                if (measurement.Type == MeasurementType.Point &&
                    measurement.Start == point1)
                {
                    point1Id = measurement.ID;
                    break;
                }
            }

            // If point1 comes from a detected point
            if (point1Id == 0)
            {
                foreach (var point in detectedPoints)
                {
                    if (point.Location == point1)
                    {
                        point1Id = point.ID;
                        break;
                    }
                }
            }

            // Create line name
            string lineName = $"L{measurementCounter++}";

            // Ask for custom name (optional)
            using (var renameDialog = new CustomRenameDialog(lineName,
                $"Create line between point {point1Id} and point {point2Id}"))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    lineName = string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                              lineName : renameDialog.NewName.Trim();
                }
            }

            // Create the line measurement
            Measurement lineMeasurement = new Measurement(
                point1,
                point2.Location,
                lineName,
                MeasurementType.Line,
                idCounter++);

            measurements.Add(lineMeasurement);
        }

        #endregion

        #region Measurement Deletion

        /// <summary>
        /// Delete a measurement at the given index
        /// </summary>
        public bool DeleteMeasurement(
            int index,
            List<Measurement> measurements,
            List<DetectedPoint> detectedPoints)
        {
            if (index < 0 || index >= measurements.Count) return false;

            Measurement m = measurements[index];

            // If it's a point measurement, also remove from detectedPoints
            if (m.Type == MeasurementType.Point)
            {
                var detectedPoint = detectedPoints.FirstOrDefault(dp =>
                    dp.Location == m.Start && Math.Abs(dp.Location.X - m.Start.X) < 5);

                if (detectedPoint.ID != 0)
                {
                    detectedPoints.Remove(detectedPoint);
                }
            }

            // If it's an angle measurement, find and remove its pair segment
            if (m.Type == MeasurementType.Angle && !m.AngleValue.HasValue)
            {
                // Find the other segment with same ID
                for (int i = measurements.Count - 1; i >= 0; i--)
                {
                    if (i != index &&
                        measurements[i].Type == MeasurementType.Angle &&
                        measurements[i].ID == m.ID)
                    {
                        measurements.RemoveAt(i);
                        if (i < index) index--; // Adjust index if removed item was before
                        break;
                    }
                }
            }

            measurements.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Clear all measurements and detected points
        /// </summary>
        public void ClearAll(
            List<Measurement> measurements,
            List<DetectedPoint> detectedPoints,
            ref int measurementCounter,
            ref int idCounter)
        {
            measurements.Clear();
            detectedPoints.Clear();
            measurementCounter = 1;
            idCounter = 1;
        }

        #endregion

        #region Measurement Renaming

        /// <summary>
        /// Prompt for rename with dialog
        /// </summary>
        public string PromptForRename(string defaultName, ref bool autoRenameDisabled)
        {
            if (autoRenameDisabled) return defaultName;

            using (var renameDialog = new AutoRenameDialog(defaultName))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    if (renameDialog.DontAskAgain) autoRenameDisabled = true;
                    return string.IsNullOrWhiteSpace(renameDialog.NewName) ? defaultName : renameDialog.NewName.Trim();
                }
                return defaultName;
            }
        }

        /// <summary>
        /// Rename a measurement at the given index
        /// </summary>
        public bool RenameMeasurement(
            int index,
            string newName,
            List<Measurement> measurements)
        {
            if (index < 0 || index >= measurements.Count) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;

            Measurement m = measurements[index];
            m.Name = newName;

            // If it's an angle, also rename the paired segment
            if (m.Type == MeasurementType.Angle && !m.AngleValue.HasValue)
            {
                for (int i = 0; i < measurements.Count; i++)
                {
                    if (i != index &&
                        measurements[i].Type == MeasurementType.Angle &&
                        measurements[i].ID == m.ID)
                    {
                        Measurement pair = measurements[i];
                        pair.Name = newName;
                        measurements[i] = pair;
                        break;
                    }
                }
            }

            measurements[index] = m;
            return true;
        }

        #endregion

        #region Measurement Movement

        /// <summary>
        /// Move a measurement by dragging
        /// </summary>
        public void MoveMeasurement(
            int index,
            Point mouseLocation,
            Point dragOffset,
            List<Measurement> measurements)
        {
            if (index < 0 || index >= measurements.Count) return;

            Measurement m = measurements[index];

            if (m.Type == MeasurementType.Point)
            {
                // Move point to new location
                Point newLocation = new Point(
                    mouseLocation.X - dragOffset.X,
                    mouseLocation.Y - dragOffset.Y);

                m.Start = newLocation;
                m.End = newLocation;
                measurements[index] = m;
            }
            else if (m.Type == MeasurementType.Angle && m.Vertex.HasValue)
            {
                // Calculate movement delta based on vertex position
                Point newVertexPos = new Point(
                    mouseLocation.X - dragOffset.X,
                    mouseLocation.Y - dragOffset.Y);

                Point delta = new Point(
                    newVertexPos.X - m.Vertex.Value.X,
                    newVertexPos.Y - m.Vertex.Value.Y);

                // Move the current segment
                m.Start = new Point(m.Start.X + delta.X, m.Start.Y + delta.Y);
                m.End = new Point(m.End.X + delta.X, m.End.Y + delta.Y);
                m.Vertex = new Point(m.Vertex.Value.X + delta.X, m.Vertex.Value.Y + delta.Y);
                measurements[index] = m;

                // Find and move the other segment that shares the same vertex and name
                for (int i = 0; i < measurements.Count; i++)
                {
                    if (i != index &&
                        measurements[i].Type == MeasurementType.Angle &&
                        measurements[i].Vertex.HasValue &&
                        measurements[i].ID == m.ID)
                    {
                        Measurement otherSegment = measurements[i];
                        otherSegment.Start = new Point(otherSegment.Start.X + delta.X, otherSegment.Start.Y + delta.Y);
                        otherSegment.End = new Point(otherSegment.End.X + delta.X, otherSegment.End.Y + delta.Y);
                        otherSegment.Vertex = new Point(otherSegment.Vertex.Value.X + delta.X, otherSegment.Vertex.Value.Y + delta.Y);
                        measurements[i] = otherSegment;
                        break;
                    }
                }
            }
            else
            {
                // For lines and distance measurements
                Point newPosition = new Point(
                    mouseLocation.X - dragOffset.X,
                    mouseLocation.Y - dragOffset.Y);

                // Determine if we're moving from start or end point
                double distanceToStart = calcService.CalculateDistance(
                    new Point(mouseLocation.X + dragOffset.X, mouseLocation.Y + dragOffset.Y), m.Start);
                double distanceToEnd = calcService.CalculateDistance(
                    new Point(mouseLocation.X + dragOffset.X, mouseLocation.Y + dragOffset.Y), m.End);

                Point delta;
                if (distanceToStart < distanceToEnd)
                {
                    delta = new Point(
                        newPosition.X - m.Start.X,
                        newPosition.Y - m.Start.Y);
                }
                else
                {
                    delta = new Point(
                        newPosition.X - m.End.X,
                        newPosition.Y - m.End.Y);
                }

                // Move both endpoints
                m.Start = new Point(m.Start.X + delta.X, m.Start.Y + delta.Y);
                m.End = new Point(m.End.X + delta.X, m.End.Y + delta.Y);
                measurements[index] = m;
            }
        }

        #endregion

        #region Measurement Selection

        /// <summary>
        /// Find a measurement at the given point
        /// </summary>
        public int FindMeasurementAtPoint(Point point, List<Measurement> measurements)
        {
            const int tolerance = 8;

            // First check regular measurements
            for (int i = 0; i < measurements.Count; i++)
            {
                if (IsMeasurementAtPoint(measurements[i], point, tolerance))
                    return i;
            }

            // Then check angle segments
            int angleIndex = FindAngleMeasurementAtPoint(point, measurements, tolerance);
            if (angleIndex >= 0)
                return angleIndex;

            return -1;
        }

        /// <summary>
        /// Check if a point is near a measurement
        /// </summary>
        private bool IsMeasurementAtPoint(Measurement m, Point point, int tolerance)
        {
            switch (m.Type)
            {
                case MeasurementType.Point:
                    return calcService.IsNearPoint(point, m.Start, tolerance);

                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.AngleWithAxis:
                case MeasurementType.PerpendicularLine:
                    return calcService.IsPointNearLine(point, m.Start, m.End, tolerance);

                case MeasurementType.Angle:
                    if (m.Vertex.HasValue)
                    {
                        return calcService.IsPointNearLine(point, m.Vertex.Value, m.End, tolerance);
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Find an angle measurement at the given point
        /// </summary>
        private int FindAngleMeasurementAtPoint(Point point, List<Measurement> measurements, int tolerance)
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                if (measurements[i].Type == MeasurementType.Angle &&
                    measurements[i].Vertex.HasValue &&
                    calcService.IsPointNearLine(point, measurements[i].Vertex.Value, measurements[i].End, tolerance))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Deselect all measurements
        /// </summary>
        public void DeselectAllMeasurements(
            List<Measurement> measurements,
            ListView measurementsList,
            ref Measurement? selectedMeasurement,
            ref int selectedMeasurementIndex)
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                Measurement m = measurements[i];
                m.IsSelected = false;
                measurements[i] = m;
            }
            selectedMeasurement = null;
            selectedMeasurementIndex = -1;

            if (measurementsList != null)
            {
                measurementsList.SelectedItems.Clear();
            }
        }

        /// <summary>
        /// Select a measurement by index
        /// </summary>
        public void SelectMeasurement(
            int index,
            List<Measurement> measurements,
            ListView measurementsList,
            ref Measurement? selectedMeasurement,
            ref int selectedMeasurementIndex,
            ref bool isUpdatingSelection) // ✅ Add this parameter
        {
            if (index >= 0 && index < measurements.Count)
            {
                // Deselect all
                for (int i = 0; i < measurements.Count; i++)
                {
                    Measurement m = measurements[i];
                    m.IsSelected = (i == index);
                    measurements[i] = m;
                }

                selectedMeasurementIndex = index;
                selectedMeasurement = measurements[index];

                // Update ListView without triggering event
                if (measurementsList != null)
                {
                    isUpdatingSelection = true;
                    foreach (ListViewItem item in measurementsList.Items)
                    {
                        item.Selected = (item.Text == measurements[index].ID.ToString());
                        if (item.Selected) item.EnsureVisible();
                    }
                    isUpdatingSelection = false;
                }
            }
        }
        #endregion

        #region Scale and Reference

        /// <summary>
        /// Set scale from reference measurement
        /// </summary>
        public void SetScaleFromReference(
            Measurement reference,
            float referenceLength,
            ref float pixelToRealRatio,
            ref bool isReferenceSet,
            List<Measurement> measurements)
        {
            double pixelLength = calcService.CalculateDistance(reference.Start, reference.End);
            if (referenceLength > 0 && pixelLength > 0)
            {
                pixelToRealRatio = (float)(pixelLength / referenceLength);
                isReferenceSet = true;

                // Change reference measurement type
                for (int i = 0; i < measurements.Count; i++)
                {
                    if (measurements[i].ID == reference.ID)
                    {
                        Measurement m = measurements[i];
                        m.Type = MeasurementType.ReferenceLine;
                        measurements[i] = m;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Get real-world value for a measurement
        /// </summary>
        public string GetRealValueString(Measurement m, bool isReferenceSet, float pixelToRealRatio, List<Measurement> allMeasurements)
        {
            if (!isReferenceSet && m.Type != MeasurementType.ReferenceLine)
                return "-";

            switch (m.Type)
            {
                case MeasurementType.Distance:
                case MeasurementType.PerpendicularLine:
                    double pixels = calcService.CalculateDistance(m.Start, m.End);
                    double realUnits = pixels / pixelToRealRatio;
                    return $"{realUnits:F2} cm";

                case MeasurementType.ReferenceLine:
                    double refPixels = calcService.CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{refUnits:F2} cm (Reference)";

                case MeasurementType.Angle:
                case MeasurementType.AngleWithAxis:
                    return GetPixelValueString(m, isReferenceSet, pixelToRealRatio, allMeasurements); // ✅ Pass full list

                default:
                    return "-";
            }
        }
        /// <summary>
        /// Get pixel value string for a measurement
        /// </summary>
        public string GetPixelValueString(Measurement m, bool isReferenceSet, float pixelToRealRatio, List<Measurement> allMeasurements)
        {
            switch (m.Type)
            {
                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.PerpendicularLine:
                    double pixels = calcService.CalculateDistance(m.Start, m.End);
                    return $"{pixels:F1} px";

                case MeasurementType.Angle:
                    double angle = calcService.CalculateAngle(m, allMeasurements); // ✅ Pass full list
                    return $"{angle:F1}°";

                case MeasurementType.AngleWithAxis:
                    double axisAngle = calcService.CalculateAngleWithAxis(m);
                    return $"{axisAngle:F1}°";

                case MeasurementType.Point:
                    return $"({m.Start.X}, {m.Start.Y})";

                default:
                    return "-";
            }
        }
        #endregion

        #region Measurement Info for Display

        /// <summary>
        /// Get hover text for a measurement
        /// </summary>
        public string GetHoverTextForMeasurement(Measurement m, bool isReferenceSet, float pixelToRealRatio, List<Measurement> allMeasurements)
        {
            switch (m.Type)
            {
                case MeasurementType.Point:
                    return $"{m.Name} (ID: {m.ID}) - ({m.Start.X}, {m.Start.Y})";

                case MeasurementType.Line:
                    double lineLength = calcService.CalculateDistance(m.Start, m.End);
                    return $"{m.Name} (ID: {m.ID}): {lineLength:F1} px";

                case MeasurementType.Distance:
                    double pixels = calcService.CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = pixels / pixelToRealRatio;
                        return $"{m.Name} (ID: {m.ID}): {pixels:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{m.Name} (ID: {m.ID}): {pixels:F1} px";

                case MeasurementType.ReferenceLine:
                    double refPixels = calcService.CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{m.Name} (ID: {m.ID}): {refPixels:F1} px ({refUnits:F2} cm) [Reference]";

                case MeasurementType.Angle:
                    if (m.AngleValue.HasValue)
                    {
                        if (m.RelatedLineIDs.Count >= 2)
                        {
                            return $"{m.Name} (ID: {m.ID}): {m.AngleValue:F1}° between L{m.RelatedLineIDs[0]} and L{m.RelatedLineIDs[1]}";
                        }
                        else
                        {
                            return $"{m.Name} (ID: {m.ID}): {m.AngleValue:F1}°";
                        }
                    }
                    else
                    {
                        double angle = calcService.CalculateAngle(m, allMeasurements); // ✅ Pass full list
                        return $"{m.Name} (ID: {m.ID}): {angle:F1}°";
                    }

                case MeasurementType.AngleWithAxis:
                    double axisAngle = calcService.CalculateAngleWithAxis(m);
                    return $"{m.Name} (ID: {m.ID}): {axisAngle:F1}° to {m.Axis}-axis";

                case MeasurementType.PerpendicularLine:
                    double perpLength = calcService.CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = perpLength / pixelToRealRatio;
                        return $"{m.Name} (ID: {m.ID}): {perpLength:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{m.Name} (ID: {m.ID}): {perpLength:F1} px";

                default:
                    return $"{m.Name} (ID: {m.ID})";
            }
        }
        /// <summary>
        /// Get hover point for a measurement
        /// </summary>
        public Point GetHoverPointForMeasurement(Measurement m, Point mouseLocation)
        {
            switch (m.Type)
            {
                case MeasurementType.Point:
                    return m.Start;
                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.AngleWithAxis:
                    return new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                case MeasurementType.Angle:
                    if (m.Vertex.HasValue)
                        return m.Vertex.Value;
                    else
                        return new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                case MeasurementType.PerpendicularLine:
                    return new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                default:
                    return mouseLocation;
            }
        }

        /// <summary>
        /// Get measurement type string for display
        /// </summary>
        public string GetMeasurementTypeString(MeasurementType type)
        {
            switch (type)
            {
                case MeasurementType.Line: return "Line";
                case MeasurementType.Point: return "Point";
                case MeasurementType.Angle: return "Angle";
                case MeasurementType.AngleWithAxis: return "Angle Axis";
                case MeasurementType.Distance: return "Distance";
                case MeasurementType.ReferenceLine: return "Reference";
                case MeasurementType.PerpendicularLine: return "Perpendicular";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Get measurement color for display
        /// </summary>
        public Color GetMeasurementColor(MeasurementType type)
        {
            switch (type)
            {
                case MeasurementType.Line: return Color.LimeGreen;
                case MeasurementType.Point: return Color.LimeGreen;
                case MeasurementType.Angle: return Color.Cyan;
                case MeasurementType.AngleWithAxis: return Color.Blue;
                case MeasurementType.Distance: return Color.Orange;
                case MeasurementType.ReferenceLine: return Color.Red;
                case MeasurementType.PerpendicularLine: return Color.Violet;
                default: return Color.White;
            }
        }

        #endregion

        #region Context Menu

        /// <summary>
        /// Show context menu for a point measurement
        /// </summary>
        public void ShowPointContextMenu(
            Point screenLocation,
            Measurement point,
            List<Measurement> measurements,
            List<DetectedPoint> detectedPoints)
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.BackColor = Color.FromArgb(62, 62, 64);
            contextMenu.ForeColor = Color.White;
            contextMenu.Renderer = new CustomToolStripRenderer();

            // Title
            ToolStripMenuItem titleItem = new ToolStripMenuItem($"📌 {point.Name} (ID: {point.ID})");
            titleItem.Enabled = false;
            titleItem.Font = new Font("Arial", 9, FontStyle.Bold);
            contextMenu.Items.Add(titleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Rename
            ToolStripMenuItem renameItem = new ToolStripMenuItem("✏️ Rename");
            renameItem.Click += (s, ev) =>
            {
                int index = measurements.FindIndex(m => m.ID == point.ID);
                if (index >= 0)
                {
                    string currentName = measurements[index].Name;
                    using (var renameDialog = new CustomRenameDialog(currentName, "Enter new name:"))
                    {
                        if (renameDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(renameDialog.NewName))
                        {
                            Measurement m = measurements[index];
                            m.Name = renameDialog.NewName.Trim();
                            measurements[index] = m;
                        }
                    }
                }
            };
            contextMenu.Items.Add(renameItem);

            // Delete
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("🗑️ Delete");
            deleteItem.Click += (s, ev) =>
            {
                int index = measurements.FindIndex(m => m.ID == point.ID);
                if (index >= 0)
                {
                    measurements.RemoveAt(index);

                    var detectedPoint = detectedPoints.FirstOrDefault(dp =>
                        dp.Location == point.Start && Math.Abs(dp.Location.X - point.Start.X) < 5);

                    if (detectedPoint.ID != 0)
                    {
                        detectedPoints.Remove(detectedPoint);
                    }
                }
            };
            contextMenu.Items.Add(deleteItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Copy coordinates
            ToolStripMenuItem copyItem = new ToolStripMenuItem("📋 Copy Coordinates");
            copyItem.Click += (s, ev) =>
            {
                string coords = $"({point.Start.X}, {point.Start.Y})";
                Clipboard.SetText(coords);
            };
            contextMenu.Items.Add(copyItem);

            contextMenu.Show(Cursor.Position);
        }

        #endregion

        #region ListView Management

        /// <summary>
        /// Update the measurements list view
        /// </summary>
        public void UpdateMeasurementsList(
            ListView measurementsList,
            List<Measurement> measurements,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            if (measurementsList == null) return;

            measurementsList.Items.Clear();

            // Group angle segments by ID so they only show once
            var groupedMeasurements = measurements
                .GroupBy(m => m.ID)
                .Select(g => g.First()) // Take only first segment of each group
                .OrderBy(m => m.AngleValue.HasValue)
                .ThenBy(m => m.ID)
                .ToList();

            foreach (var m in groupedMeasurements)
            {
                string typeText = GetMeasurementTypeString(m.Type);

                if (m.Type == MeasurementType.Angle && m.AngleValue.HasValue)
                {
                    typeText = "Intersection Angle";
                }

                string valueText = GetMeasurementValueText(m, isReferenceSet, pixelToRealRatio, measurements);

                ListViewItem item = new ListViewItem(m.ID.ToString());
                item.SubItems.Add(typeText);
                item.SubItems.Add(m.Name);
                item.SubItems.Add(valueText);

                if (m.IsSelected)
                {
                    item.BackColor = Color.FromArgb(75, 110, 175);
                    item.ForeColor = Color.White;
                }
                else
                {
                    item.BackColor = measurementsList.BackColor;
                    item.ForeColor = measurementsList.ForeColor;
                }

                measurementsList.Items.Add(item);
            }
        }
        /// <summary>
        /// Get measurement value text for list view
        /// </summary>

        private string GetMeasurementValueText(Measurement m, bool isReferenceSet, float pixelToRealRatio, List<Measurement> allMeasurements)
        {
            switch (m.Type)
            {
                case MeasurementType.Line:
                    double lineLength = calcService.CalculateDistance(m.Start, m.End);
                    return $"{lineLength:F1} px";

                case MeasurementType.Distance:
                    double pixels = calcService.CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = pixels / pixelToRealRatio;
                        return $"{pixels:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{pixels:F1} px";

                case MeasurementType.ReferenceLine:
                    double refPixels = calcService.CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{refPixels:F1} px ({refUnits:F2} cm)";

                case MeasurementType.Angle:
                    if (m.AngleValue.HasValue)
                    {
                        if (m.RelatedLineIDs.Count >= 2)
                        {
                            return $"{m.AngleValue:F1}° (L{m.RelatedLineIDs[0]}-L{m.RelatedLineIDs[1]})";
                        }
                        else
                        {
                            return $"{m.AngleValue:F1}°";
                        }
                    }
                    else
                    {
                        double angle = calcService.CalculateAngle(m, allMeasurements); // ✅ Pass full list
                        return $"{angle:F1}°";
                    }

                case MeasurementType.AngleWithAxis:
                    double axisAngle = calcService.CalculateAngleWithAxis(m);
                    return $"{axisAngle:F1}° to {m.Axis}";

                case MeasurementType.Point:
                    return $"({m.Start.X}, {m.Start.Y})";

                case MeasurementType.PerpendicularLine:
                    double perpLength = calcService.CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = perpLength / pixelToRealRatio;
                        return $"{perpLength:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{perpLength:F1} px";

                default:
                    return "-";
            }
        }
        #endregion
    }
}