using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace kinectProject
{
    public class IntersectionService
    {
        private CalculationService calcService;

        public IntersectionService()
        {
            calcService = new CalculationService();
        }

        #region Main Intersection Detection

        /// <summary>
        /// Find all intersection points between all line measurements
        /// </summary>
        public void FindAllIntersections(
            List<Measurement> measurements,
            List<IntersectionPoint> intersectionPoints,
            ref int intersectionCounter,
            int intersectionTolerance)
        {
            intersectionPoints.Clear();

            // Filter only line-type measurements
            var lineMeasurements = measurements.Where(m =>
                m.Type == MeasurementType.Line ||
                m.Type == MeasurementType.Distance ||
                m.Type == MeasurementType.ReferenceLine ||
                m.Type == MeasurementType.PerpendicularLine ||
                m.Type == MeasurementType.Angle ||
                m.Type == MeasurementType.AngleWithAxis).ToList();

            // For each pair of lines
            for (int i = 0; i < lineMeasurements.Count; i++)
            {
                for (int j = i + 1; j < lineMeasurements.Count; j++)
                {
                    var line1 = lineMeasurements[i];
                    var line2 = lineMeasurements[j];

                    // Get start and end points for each line
                    Point line1Start, line1End, line2Start, line2End;
                    GetLineEndpoints(line1, out line1Start, out line1End);
                    GetLineEndpoints(line2, out line2Start, out line2End);

                    // 1. Check exact segment intersection
                    Point? exactIntersection = calcService.FindLineIntersection(
                        line1Start, line1End, line2Start, line2End);

                    if (exactIntersection.HasValue)
                    {
                        AddIntersectionPoint(exactIntersection.Value, line1.ID, line2.ID,
                            IntersectionType.Exact, intersectionPoints, ref intersectionCounter,
                            intersectionTolerance);
                    }
                    else
                    {
                        // 2. Check proximity of endpoints
                        CheckProximityIntersections(line1, line2, line1Start, line1End,
                            line2Start, line2End, intersectionPoints, ref intersectionCounter,
                            intersectionTolerance);

                        // 3. Check if lines share a terminal point
                        CheckTerminalIntersections(line1, line2, line1Start, line1End,
                            line2Start, line2End, intersectionPoints, ref intersectionCounter,
                            intersectionTolerance);
                    }
                }
            }

            // Calculate angles for all intersection points
            CalculateAllAngles(intersectionPoints, measurements);
        }

        /// <summary>
        /// Get endpoints for a measurement line
        /// </summary>
        private void GetLineEndpoints(Measurement line, out Point start, out Point end)
        {
            if (line.Type == MeasurementType.Angle && line.Vertex.HasValue)
            {
                start = line.Vertex.Value;
                end = line.End;
            }
            else
            {
                start = line.Start;
                end = line.End;
            }
        }

        #endregion

        #region Intersection Type Detection

        /// <summary>
        /// Check for proximity intersections (endpoints close to each other)
        /// </summary>
        private void CheckProximityIntersections(
            Measurement line1, Measurement line2,
            Point line1Start, Point line1End,
            Point line2Start, Point line2End,
            List<IntersectionPoint> intersectionPoints,
            ref int intersectionCounter,
            int intersectionTolerance)
        {
            if (calcService.CalculateDistance(line1Start, line2Start) < intersectionTolerance)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID,
                    IntersectionType.Proximity, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
            if (calcService.CalculateDistance(line1Start, line2End) < intersectionTolerance)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID,
                    IntersectionType.Proximity, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
            if (calcService.CalculateDistance(line1End, line2Start) < intersectionTolerance)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID,
                    IntersectionType.Proximity, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
            if (calcService.CalculateDistance(line1End, line2End) < intersectionTolerance)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID,
                    IntersectionType.Proximity, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
        }

        /// <summary>
        /// Check for terminal intersections (shared endpoints)
        /// </summary>
        private void CheckTerminalIntersections(
            Measurement line1, Measurement line2,
            Point line1Start, Point line1End,
            Point line2Start, Point line2End,
            List<IntersectionPoint> intersectionPoints,
            ref int intersectionCounter,
            int intersectionTolerance)
        {
            if (line1Start == line2Start || line1Start == line2End)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID,
                    IntersectionType.Terminal, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
            if (line1End == line2Start || line1End == line2End)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID,
                    IntersectionType.Terminal, intersectionPoints, ref intersectionCounter,
                    intersectionTolerance);
            }
        }

        /// <summary>
        /// Add or update an intersection point
        /// </summary>
        private void AddIntersectionPoint(
            Point location,
            int line1Id,
            int line2Id,
            IntersectionType type,
            List<IntersectionPoint> intersectionPoints,
            ref int intersectionCounter,
            int intersectionTolerance)
        {
            // Check if intersection point already exists at this location
            var existing = intersectionPoints.FirstOrDefault(ip =>
                calcService.CalculateDistance(ip.Location, location) < intersectionTolerance);

            if (existing.ID == 0) // New point
            {
                IntersectionPoint newPoint = new IntersectionPoint(location, intersectionCounter++);
                newPoint.Type = type;

                if (!newPoint.LineIDs.Contains(line1Id))
                    newPoint.LineIDs.Add(line1Id);
                if (!newPoint.LineIDs.Contains(line2Id))
                    newPoint.LineIDs.Add(line2Id);

                intersectionPoints.Add(newPoint);
            }
            else // Existing point - update it
            {
                int index = intersectionPoints.IndexOf(existing);
                existing = intersectionPoints[index];

                if (!existing.LineIDs.Contains(line1Id))
                    existing.LineIDs.Add(line1Id);
                if (!existing.LineIDs.Contains(line2Id))
                    existing.LineIDs.Add(line2Id);

                // Upgrade type if needed
                if (type == IntersectionType.Exact)
                    existing.Type = IntersectionType.Exact;
                else if (type == IntersectionType.Proximity && existing.Type == IntersectionType.Terminal)
                    existing.Type = IntersectionType.Proximity;

                intersectionPoints[index] = existing;
            }
        }

        #endregion

        #region Angle Calculations at Intersections

        /// <summary>
        /// Calculate all angles at all intersection points
        /// </summary>
        public void CalculateAllAngles(
            List<IntersectionPoint> intersectionPoints,
            List<Measurement> measurements)
        {
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                var ip = intersectionPoints[i];
                ip.Angles.Clear();

                if (ip.LineIDs.Count < 2) continue;

                // Get the lines at this intersection
                var lines = measurements.Where(m => ip.LineIDs.Contains(m.ID)).ToList();

                // For each pair of lines at this intersection
                for (int j = 0; j < lines.Count; j++)
                {
                    for (int k = j + 1; k < lines.Count; k++)
                    {
                        var line1 = lines[j];
                        var line2 = lines[k];

                        // Get vectors for each line at the intersection point
                        PointF vector1 = GetLineVectorAtIntersection(line1, ip.Location);
                        PointF vector2 = GetLineVectorAtIntersection(line2, ip.Location);

                        // Calculate angles between vectors
                        var angles = CalculateAnglesBetweenVectors(vector1, vector2);

                        foreach (var angle in angles)
                        {
                            ip.Angles.Add(new Tuple<int, int, double>(
                                line1.ID, line2.ID, Math.Round(angle, 1)));
                        }
                    }
                }

                intersectionPoints[i] = ip;
            }
        }

        /// <summary>
        /// Get vector direction of a line at an intersection point
        /// </summary>
        public PointF GetLineVectorAtIntersection(Measurement line, Point intersection)
        {
            Point start, end;
            GetLineEndpoints(line, out start, out end);

            // Determine which endpoint is closer to the intersection
            double distToStart = calcService.CalculateDistance(intersection, start);
            double distToEnd = calcService.CalculateDistance(intersection, end);

            // Return vector from intersection to the other endpoint
            if (distToStart < distToEnd)
            {
                return new PointF(end.X - intersection.X, end.Y - intersection.Y);
            }
            else
            {
                return new PointF(start.X - intersection.X, start.Y - intersection.Y);
            }
        }

        /// <summary>
        /// Calculate angles between two vectors (returns both acute and obtuse)
        /// </summary>
        private List<double> CalculateAnglesBetweenVectors(PointF v1, PointF v2)
        {
            List<double> angles = new List<double>();

            double dot = v1.X * v2.X + v1.Y * v2.Y;
            double cross = v1.X * v2.Y - v1.Y * v2.X;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0)
                return new List<double> { 0, 180 };

            double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
            double angleRad = Math.Acos(cosTheta);
            double angleDeg = angleRad * (180 / Math.PI);

            // Acute angle (0-90°) or right angle
            double acuteAngle = Math.Min(angleDeg, 180 - angleDeg);

            // Obtuse angle (90-180°)
            double obtuseAngle = 180 - acuteAngle;

            // If lines are perpendicular (≈90°)
            if (Math.Abs(acuteAngle - 90) < 0.1)
            {
                angles.Add(90);
                angles.Add(90);
            }
            else
            {
                angles.Add(Math.Round(acuteAngle, 1));
                angles.Add(Math.Round(obtuseAngle, 1));
            }

            return angles;
        }

        #endregion

        #region Add Intersection Angles to Measurements

        /// <summary>
        /// Add intersection angle measurements to the measurements list
        /// </summary>
        public void AddIntersectionAnglesToMeasurements(
            List<IntersectionPoint> intersectionPoints,
            List<Measurement> measurements,
            ref int idCounter)
        {
            // Remove existing intersection angles to avoid duplicates
            measurements.RemoveAll(m => m.AngleValue.HasValue);

            foreach (var ip in intersectionPoints)
            {
                if (ip.Angles.Count == 0) continue;

                // Group angles by line pairs and get distinct values
                var distinctAngles = ip.Angles
                    .GroupBy(a => new
                    {
                        Line1 = Math.Min(a.Item1, a.Item2),
                        Line2 = Math.Max(a.Item1, a.Item2),
                        Angle = a.Item3
                    })
                    .Select(g => new
                    {
                        Line1 = g.Key.Line1,
                        Line2 = g.Key.Line2,
                        Angle = g.Key.Angle
                    })
                    .ToList();

                // Create measurements for each angle
                foreach (var angleData in distinctAngles)
                {
                    string angleType = (angleData.Angle < 90) ? "A" :
                                      (Math.Abs(angleData.Angle - 90) < 0.5) ? "R" : "O";

                    string name = $"IA{idCounter}{angleType}";

                    Measurement angleMeasurement = CreateIntersectionAngleMeasurement(
                        name, idCounter, ip.Location, angleData.Angle,
                        angleData.Line1, angleData.Line2);

                    measurements.Add(angleMeasurement);
                    idCounter++;
                }
            }
        }

        /// <summary>
        /// Create a measurement for an intersection angle
        /// </summary>
        public static Measurement CreateIntersectionAngleMeasurement(
            string name,
            int id,
            Point vertex,
            double angleValue,
            int line1Id,
            int line2Id)
        {
            var measurement = new Measurement(vertex, vertex, name, MeasurementType.Angle, id);
            measurement.Vertex = vertex;
            measurement.AngleValue = angleValue;
            measurement.RelatedLineIDs = new List<int> { line1Id, line2Id };
            return measurement;
        }

        #endregion

        #region Find Intersection at Point

        /// <summary>
        /// Find an intersection point at the given location
        /// </summary>
        public IntersectionPoint? FindIntersectionAtPoint(
            Point point,
            List<IntersectionPoint> intersectionPoints,
            int intersectionTolerance)
        {
            foreach (var ip in intersectionPoints)
            {
                if (calcService.CalculateDistance(ip.Location, point) < intersectionTolerance)
                {
                    return ip;
                }
            }
            return null;
        }

        #endregion

        #region Context Menu

        /// <summary>
        /// Show context menu for an intersection point
        /// </summary>
        public void ShowAngleContextMenu(
            Point screenLocation,
            IntersectionPoint intersection,
            Control drawingPanel)
        {
            if (intersection.Equals(default(IntersectionPoint))) return;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.BackColor = Color.FromArgb(62, 62, 64);
            contextMenu.ForeColor = Color.White;
            contextMenu.Renderer = new CustomToolStripRenderer();

            // Title
            ToolStripMenuItem titleItem = new ToolStripMenuItem(
                $"📐 Intersection I{intersection.ID} - {intersection.LineIDs.Count} lines");
            titleItem.Enabled = false;
            titleItem.Font = new Font("Arial", 9, FontStyle.Bold);
            contextMenu.Items.Add(titleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Group angles by line pairs
            var angleGroups = intersection.Angles
                .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                .Select(g => new
                {
                    Line1 = g.Key.Line1,
                    Line2 = g.Key.Line2,
                    Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                })
                .ToList();

            if (angleGroups.Count == 0)
            {
                ToolStripMenuItem noAnglesItem = new ToolStripMenuItem("No angles detected");
                noAnglesItem.Enabled = false;
                contextMenu.Items.Add(noAnglesItem);
            }
            else
            {
                foreach (var group in angleGroups)
                {
                    if (group.Angles.Count == 2)
                    {
                        string angleText = $"∠(L{group.Line1}-L{group.Line2}): {group.Angles[0]:F1}° & {group.Angles[1]:F1}°";
                        ToolStripMenuItem angleItem = new ToolStripMenuItem(angleText);
                        contextMenu.Items.Add(angleItem);
                    }
                    else if (group.Angles.Count == 1)
                    {
                        string angleText = $"∠(L{group.Line1}-L{group.Line2}) = {group.Angles[0]:F1}°";
                        if (Math.Abs(group.Angles[0] - 90) < 0.1)
                        {
                            angleText += " (Right angle)";
                        }
                        contextMenu.Items.Add(new ToolStripMenuItem(angleText));
                    }
                }
            }

            contextMenu.Items.Add(new ToolStripSeparator());

            // Action buttons
            ToolStripMenuItem copyItem = new ToolStripMenuItem("📋 Copy All Data");
            copyItem.Click += (s, ev) => CopyAnglesToClipboard(intersection);
            contextMenu.Items.Add(copyItem);

            ToolStripMenuItem clearItem = new ToolStripMenuItem("❌ Clear Selection");
            clearItem.Click += (s, ev) =>
            {
                // This will be handled by the parent form through a callback
            };
            contextMenu.Items.Add(clearItem);

            // Show the menu
            contextMenu.Show(drawingPanel, screenLocation);
        }

        /// <summary>
        /// Copy all angles at an intersection to clipboard
        /// </summary>
        public void CopyAnglesToClipboard(IntersectionPoint intersection)
        {
            if (intersection.Angles.Count == 0)
            {
                Clipboard.SetText("No angles at this intersection");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== INTERSECTION POINT I{intersection.ID} ===");
            sb.AppendLine($"Type: {intersection.Type}");
            sb.AppendLine($"Coordinates: ({intersection.Location.X}, {intersection.Location.Y})");
            sb.AppendLine($"Lines involved: {string.Join(", ", intersection.LineIDs.Select(id => $"L{id}"))}");
            sb.AppendLine();
            sb.AppendLine("ANGLES:");
            sb.AppendLine("-------");

            var angleGroups = intersection.Angles
                .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                .Select(g => new
                {
                    Line1 = g.Key.Line1,
                    Line2 = g.Key.Line2,
                    Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                })
                .OrderBy(g => g.Line1).ThenBy(g => g.Line2);

            foreach (var group in angleGroups)
            {
                sb.AppendLine($"Between L{group.Line1} and L{group.Line2}:");

                if (group.Angles.Count == 2)
                {
                    sb.AppendLine($"  • Acute angle: {group.Angles[0]:F2}°");
                    sb.AppendLine($"  • Obtuse angle: {group.Angles[1]:F2}°");
                    sb.AppendLine($"  • Sum: {(group.Angles[0] + group.Angles[1]):F2}°");
                    sb.AppendLine($"  • Acute/Obtuse ratio: {group.Angles[0] / group.Angles[1]:F3}");
                }
                else if (group.Angles.Count == 1)
                {
                    sb.AppendLine($"  • Angle: {group.Angles[0]:F2}°");
                    if (Math.Abs(group.Angles[0] - 90) < 0.1)
                        sb.AppendLine("    → RIGHT ANGLE (90°)");
                }
                sb.AppendLine();
            }

            // Statistics
            var allAngles = intersection.Angles.Select(a => a.Item3).Distinct().ToList();
            sb.AppendLine("STATISTICS:");
            sb.AppendLine("-----------");
            sb.AppendLine($"Total distinct angles: {allAngles.Count}");
            sb.AppendLine($"Acute angles (<90°): {allAngles.Where(a => a < 90).Count()}");
            sb.AppendLine($"Right angles (≈90°): {allAngles.Where(a => Math.Abs(a - 90) < 0.5).Count()}");
            sb.AppendLine($"Obtuse angles (>90°): {allAngles.Where(a => a > 90).Count()}");

            if (allAngles.Count > 0)
            {
                sb.AppendLine($"Minimum: {allAngles.Min():F2}°");
                sb.AppendLine($"Maximum: {allAngles.Max():F2}°");
                sb.AppendLine($"Average: {allAngles.Average():F2}°");
                sb.AppendLine($"Median: {calcService.CalculateMedian(allAngles):F2}°");
            }

            // Special angles
            sb.AppendLine();
            sb.AppendLine("SPECIAL ANGLES:");
            sb.AppendLine("---------------");

            foreach (var angle in allAngles.OrderBy(a => a))
            {
                string special = "";
                if (Math.Abs(angle - 30) < 0.5) special = " (Common: 30°)";
                else if (Math.Abs(angle - 45) < 0.5) special = " (Half right: 45°)";
                else if (Math.Abs(angle - 60) < 0.5) special = " (Common: 60°)";
                else if (Math.Abs(angle - 90) < 0.5) special = " (Right angle: 90°)";
                else if (Math.Abs(angle - 120) < 0.5) special = " (Supplementary to 60°)";
                else if (Math.Abs(angle - 135) < 0.5) special = " (Supplementary to 45°)";
                else if (Math.Abs(angle - 150) < 0.5) special = " (Supplementary to 30°)";

                sb.AppendLine($"{angle:F2}°{special}");
            }

            Clipboard.SetText(sb.ToString());
        }

        #endregion

        #region PDF Data

        /// <summary>
        /// Get intersection data formatted for PDF export
        /// </summary>
        public string GetIntersectionDataForPdf(List<IntersectionPoint> intersectionPoints)
        {
            if (intersectionPoints.Count == 0)
                return "No intersection points detected.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INTERSECTION POINTS ANALYSIS");
            sb.AppendLine("=============================");

            foreach (var ip in intersectionPoints.OrderBy(p => p.ID))
            {
                sb.AppendLine();
                sb.AppendLine($"Intersection Point I{ip.ID}");
                sb.AppendLine($"Type: {ip.Type}");
                sb.AppendLine($"Coordinates: ({ip.Location.X}, {ip.Location.Y})");
                sb.AppendLine($"Lines involved: {string.Join(", ", ip.LineIDs.Select(id => $"L{id}"))}");

                if (ip.Angles.Count > 0)
                {
                    sb.AppendLine("Angles between lines:");

                    var angleGroups = ip.Angles
                        .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                        .Select(g => new
                        {
                            Line1 = g.Key.Line1,
                            Line2 = g.Key.Line2,
                            Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                        })
                        .OrderBy(g => g.Line1).ThenBy(g => g.Line2);

                    foreach (var group in angleGroups)
                    {
                        if (group.Angles.Count == 2)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}:");
                            sb.AppendLine($"    Acute angle: {group.Angles[0]:F1}°");
                            sb.AppendLine($"    Obtuse angle: {group.Angles[1]:F1}°");
                            sb.AppendLine($"    Sum: {(group.Angles[0] + group.Angles[1]):F1}°");
                        }
                        else if (group.Angles.Count == 1)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}: {group.Angles[0]:F1}°");
                            if (Math.Abs(group.Angles[0] - 90) < 0.1)
                                sb.AppendLine("    → RIGHT ANGLE");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("No angle measurements available");
                }

                sb.AppendLine(new string('-', 50));
            }

            return sb.ToString();
        }

        #endregion

        #region Drawing Methods

        /// <summary>
        /// Draw all intersection points
        /// </summary>
        public void DrawIntersectionPoints(
            Graphics g,
            List<IntersectionPoint> intersectionPoints,
            IntersectionPoint? hoveredIntersection,
            IntersectionPoint? selectedIntersection,
            float zoomFactor,
            List<Measurement> measurements)
        {
            foreach (var ip in intersectionPoints)
            {
                Color pointColor = GetIntersectionColor(ip.Type);
                int pointSize = Math.Max(4, (int)(8 / zoomFactor));

                using (Brush brush = new SolidBrush(pointColor))
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    g.FillEllipse(brush,
                        ip.Location.X - pointSize / 2,
                        ip.Location.Y - pointSize / 2,
                        pointSize, pointSize);
                    g.DrawEllipse(pen,
                        ip.Location.X - pointSize / 2,
                        ip.Location.Y - pointSize / 2,
                        pointSize, pointSize);
                }

                // Highlight hovered or selected point
                if ((hoveredIntersection.HasValue && hoveredIntersection.Value.ID == ip.ID) ||
                    (selectedIntersection.HasValue && selectedIntersection.Value.ID == ip.ID))
                {
                    using (Pen highlightPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawEllipse(highlightPen,
                            ip.Location.X - pointSize,
                            ip.Location.Y - pointSize,
                            pointSize * 2, pointSize * 2);
                    }

                    // Show point ID
                    using (Font font = new Font("Arial", Math.Max(8, 10 / zoomFactor)))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    {
                        string idText = $"I{ip.ID}";
                        SizeF textSize = g.MeasureString(idText, font);

                        RectangleF textRect = new RectangleF(
                            ip.Location.X - textSize.Width / 2,
                            ip.Location.Y - textSize.Height - pointSize - 5,
                            textSize.Width + 4,
                            textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(idText, font, textBrush,
                            ip.Location.X - textSize.Width / 2 + 2,
                            ip.Location.Y - textSize.Height - pointSize - 3);
                    }
                }

                // Draw angles for selected intersection
                if (selectedIntersection.HasValue && selectedIntersection.Value.ID == ip.ID)
                {
                    DrawIntersectionAngles(g, ip, measurements, zoomFactor);
                }
            }
        }

        /// <summary>
        /// Draw angle arcs for an intersection point
        /// </summary>
        public void DrawIntersectionAngles(
            Graphics g,
            IntersectionPoint ip,
            List<Measurement> measurements,
            float zoomFactor)
        {
            if (ip.LineIDs.Count < 2 || ip.Angles.Count == 0) return;

            // Get angle pair for display
            var anglePair = ip.Angles
                .GroupBy(a => new { L1 = Math.Min(a.Item1, a.Item2), L2 = Math.Max(a.Item1, a.Item2) })
                .Select(gg => gg.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList())
                .FirstOrDefault(a => a.Count >= 2);

            if (anglePair == null || anglePair.Count < 2) return;

            double acuteAngle = anglePair[0];
            double obtuseAngle = anglePair[1];

            // Get the two intersecting lines
            var lines = measurements.Where(m => ip.LineIDs.Contains(m.ID)).Take(2).ToList();
            if (lines.Count < 2) return;

            // Calculate line angles
            double[] lineAngles = new double[2];
            for (int i = 0; i < 2; i++)
            {
                Point start, end;
                GetLineEndpoints(lines[i], out start, out end);

                double dx = end.X - ip.Location.X;
                double dy = end.Y - ip.Location.Y;

                double distToStart = calcService.CalculateDistance(start, ip.Location);
                double distToEnd = calcService.CalculateDistance(end, ip.Location);

                if (distToStart > distToEnd)
                {
                    dx = start.X - ip.Location.X;
                    dy = start.Y - ip.Location.Y;
                }

                lineAngles[i] = Math.Atan2(dy, dx) * (180 / Math.PI);
                if (lineAngles[i] < 0) lineAngles[i] += 360;
            }

            // Calculate arc parameters
            double angle1 = lineAngles[0];
            double angle2 = lineAngles[1];
            double diff = Math.Abs(angle2 - angle1);
            if (diff > 180) diff = 360 - diff;

            float acuteStartAngle, obtuseStartAngle;

            if (diff < 180)
            {
                acuteStartAngle = (float)Math.Min(angle1, angle2);
                if (Math.Abs(angle2 - angle1) > 180)
                {
                    acuteStartAngle = (float)Math.Max(angle1, angle2);
                }
                obtuseStartAngle = acuteStartAngle + (float)acuteAngle;
            }
            else
            {
                acuteStartAngle = (float)Math.Max(angle1, angle2);
                obtuseStartAngle = (float)Math.Min(angle1, angle2);
            }

            // Normalize
            while (acuteStartAngle < 0) acuteStartAngle += 360;
            while (acuteStartAngle >= 360) acuteStartAngle -= 360;
            while (obtuseStartAngle < 0) obtuseStartAngle += 360;
            while (obtuseStartAngle >= 360) obtuseStartAngle -= 360;

            float acuteRadius = 28f;
            float obtuseRadius = 36f;

            using (Font angleFont = new Font("Arial", Math.Max(9, 11 / zoomFactor), FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
            {
                // Acute angle arc
                using (Pen acutePen = new Pen(Color.Cyan, 1.5f))
                {
                    RectangleF acuteRect = new RectangleF(
                        ip.Location.X - acuteRadius,
                        ip.Location.Y - acuteRadius,
                        acuteRadius * 2,
                        acuteRadius * 2);

                    g.DrawArc(acutePen, acuteRect, acuteStartAngle, (float)acuteAngle);

                    double acuteMidAngle = (acuteStartAngle + acuteAngle / 2) * Math.PI / 180;
                    PointF acuteTextPos = new PointF(
                        ip.Location.X + (float)(acuteRadius * 1.4 * Math.Cos(acuteMidAngle)),
                        ip.Location.Y + (float)(acuteRadius * 1.4 * Math.Sin(acuteMidAngle)));

                    string acuteText = $"{acuteAngle:F1}°";
                    SizeF acuteTextSize = g.MeasureString(acuteText, angleFont);

                    RectangleF acuteTextRect = new RectangleF(
                        acuteTextPos.X - acuteTextSize.Width / 2,
                        acuteTextPos.Y - acuteTextSize.Height / 2,
                        acuteTextSize.Width + 6,
                        acuteTextSize.Height + 2);

                    g.FillRectangle(bgBrush, acuteTextRect);
                    g.DrawString(acuteText, angleFont, textBrush,
                        acuteTextRect.X + 3, acuteTextRect.Y + 1);
                }

                // Obtuse angle arc
                using (Pen obtusePen = new Pen(Color.Magenta, 1.5f))
                {
                    RectangleF obtuseRect = new RectangleF(
                        ip.Location.X - obtuseRadius,
                        ip.Location.Y - obtuseRadius,
                        obtuseRadius * 2,
                        obtuseRadius * 2);

                    g.DrawArc(obtusePen, obtuseRect, obtuseStartAngle, (float)obtuseAngle);

                    double obtuseMidAngle = (obtuseStartAngle + obtuseAngle / 2) * Math.PI / 180;
                    PointF obtuseTextPos = new PointF(
                        ip.Location.X + (float)(obtuseRadius * 1.4 * Math.Cos(obtuseMidAngle)),
                        ip.Location.Y + (float)(obtuseRadius * 1.4 * Math.Sin(obtuseMidAngle)));

                    string obtuseText = $"{obtuseAngle:F1}°";
                    SizeF obtuseTextSize = g.MeasureString(obtuseText, angleFont);

                    RectangleF obtuseTextRect = new RectangleF(
                        obtuseTextPos.X - obtuseTextSize.Width / 2,
                        obtuseTextPos.Y - obtuseTextSize.Height / 2,
                        obtuseTextSize.Width + 6,
                        obtuseTextSize.Height + 2);

                    g.FillRectangle(bgBrush, obtuseTextRect);
                    g.DrawString(obtuseText, angleFont, textBrush,
                        obtuseTextRect.X + 3, obtuseTextRect.Y + 1);
                }
            }
        }

        /// <summary>
        /// Get color for intersection type
        /// </summary>
        public Color GetIntersectionColor(IntersectionType type)
        {
            switch (type)
            {
                case IntersectionType.Exact: return Color.Red;
                case IntersectionType.Proximity: return Color.Blue;
                case IntersectionType.Terminal: return Color.Green;
                default: return Color.Gray;
            }
        }

        #endregion
    }
}