
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace kinectProject
{
    public partial class BodyPictureAnalyzer : Form
    {
        // Enums
        private enum ToolMode { None, Line, Point, Angle, AngleWithAxis, Distance, Reference }
        private enum EditMode { None, Move, Delete }
        private enum AxisType { X, Y }

        // Measurement structures
        private struct Measurement
        {
            public Point Start;
            public Point End;
            public string Name;
            public MeasurementType Type;
            public bool IsSelected;
            public AxisType? Axis; // For angle measurements
            public Point? Vertex; // For angle measurements with two segments

            public Measurement(Point start, Point end, string name, MeasurementType type)
            {
                Start = start;
                End = end;
                Name = name;
                Type = type;
                IsSelected = false;
                Axis = null;
                Vertex = null;
            }
        }

        private enum MeasurementType { Line, Point, Angle, AngleWithAxis, Distance, ReferenceLine }

        // Application state
        private ToolMode currentTool = ToolMode.None;
        private EditMode currentEditMode = EditMode.None;
        private List<Measurement> measurements = new List<Measurement>();
        private Image originalImage;
        private Point? currentStartPoint = null;
        private int measurementCounter = 1;
        private float pixelToRealRatio = 1.0f;
        private bool isReferenceSet = false;
        private bool showGrid = true;
        private Point gridOrigin;
        private bool isDraggingGrid = false;
        private const int gridGrabRadius = 10;
        private Measurement? selectedMeasurement = null;
        private int selectedMeasurementIndex = -1;
        private bool isDraggingMeasurement = false;
        private Point dragOffset;
        private Point? angleVertex = null;
        private bool isSettingReference = false;
        private Point? angleFirstPoint = null;

        // UI Controls
        private PictureBox pictureBox;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ListBox measurementsList;

        public BodyPictureAnalyzer()
        {
           // InitializeComponent();
            SetupUI();
            UpdateStatus("Ready to import an image");
        }

        private void SetupUI()
        {
            // Main form setup
            this.Text = "Advanced Image Measurement Tool";
            this.Size = new Size(1000, 700);
            this.DoubleBuffered = true;

            // Toolstrip setup
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;

            // Toolstrip buttons
            AddToolButton("Import Image", BtnImport_Click);
            AddToolSeparator();

            AddToolButton("Line Tool", (s, e) => SetToolMode(ToolMode.Line));
            AddToolButton("Point Tool", (s, e) => SetToolMode(ToolMode.Point));
            AddToolButton("Angle Tool", (s, e) => SetToolMode(ToolMode.Angle));
            AddToolButton("Angle with Axis", (s, e) => SetToolMode(ToolMode.AngleWithAxis));
            AddToolButton("Distance Tool", (s, e) => SetToolMode(ToolMode.Distance));
            AddToolButton("Set Reference", (s, e) => SetToolMode(ToolMode.Reference));

            AddToolSeparator();

            AddToolButton("Move Mode", (s, e) => SetEditMode(EditMode.Move));
            AddToolButton("Delete Mode", (s, e) => SetEditMode(EditMode.Delete));
            AddToolButton("Clear All", BtnClear_Click);
            AddToolButton("Toggle Grid", BtnToggleGrid_Click);

            // Picture box setup
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = Color.DarkGray;
            pictureBox.BorderStyle = BorderStyle.FixedSingle;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.MouseClick += PictureBox_MouseClick;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.Paint += PictureBox_Paint;

            // Measurements list
            measurementsList = new ListBox();
            measurementsList.Dock = DockStyle.Right;
            measurementsList.Width = 250;
            measurementsList.SelectedIndexChanged += MeasurementsList_SelectedIndexChanged;

            // Status strip
            statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Bottom;

            // Add controls to form
            this.Controls.Add(pictureBox);
            this.Controls.Add(measurementsList);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);
        }

        private void AddToolButton(string text, EventHandler handler)
        {
            var button = new ToolStripButton(text);
            button.Click += handler;
            toolStrip.Items.Add(button);
        }

        private void AddToolSeparator()
        {
            toolStrip.Items.Add(new ToolStripSeparator());
        }

        private void SetToolMode(ToolMode mode)
        {
            currentTool = mode;
            currentEditMode = EditMode.None;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;

            string statusText = "";
            switch (mode)
            {
                case ToolMode.Line: statusText = "Line Tool: Click to place start and end points"; break;
                case ToolMode.Point: statusText = "Point Tool: Click to place a point"; break;
                case ToolMode.Angle: statusText = "Angle Tool: Click to place vertex, then two end points"; break;
                case ToolMode.AngleWithAxis: statusText = "Angle with Axis: Draw a line, then select axis"; break;
                case ToolMode.Distance: statusText = "Distance Tool: Click to measure distance"; break;
                case ToolMode.Reference: statusText = "Reference Tool: Draw a line of known length"; break;
            }

            UpdateStatus(statusText);
            pictureBox.Cursor = Cursors.Cross;
            DeselectAllMeasurements();
        }

        private void SetEditMode(EditMode mode)
        {
            currentEditMode = mode;
            currentTool = ToolMode.None;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;

            string statusText = mode == EditMode.Delete ?
                "Delete Mode: Click on measurement to delete" :
                "Move Mode: Click and drag to move measurement";

            UpdateStatus(statusText);
            pictureBox.Cursor = mode == EditMode.Delete ? Cursors.No : Cursors.Hand;
            DeselectAllMeasurements();
        }

        private void UpdateStatus(string message)
        {
            if (statusStrip.Items.Count == 0)
                statusStrip.Items.Add(new ToolStripStatusLabel());

            statusStrip.Items[0].Text = message;
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        originalImage = Image.FromFile(openFileDialog.FileName);
                        pictureBox.Image = (Image)originalImage.Clone();

                        // Initialize grid at center
                        gridOrigin = new Point(pictureBox.Width / 2, pictureBox.Height / 2);

                        measurements.Clear();
                        measurementsList.Items.Clear();
                        measurementCounter = 1;
                        isReferenceSet = false;
                        pixelToRealRatio = 1.0f;
                        isSettingReference = false;

                        UpdateStatus("Image loaded. Select a measurement tool.");
                        pictureBox.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            measurements.Clear();
            measurementsList.Items.Clear();
            measurementCounter = 1;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;
            isReferenceSet = false;
            pixelToRealRatio = 1.0f;
            isSettingReference = false;
            UpdateStatus("All measurements cleared.");
            pictureBox.Invalidate();
        }

        private void BtnToggleGrid_Click(object sender, EventArgs e)
        {
            showGrid = !showGrid;
            pictureBox.Invalidate();
        }

        private void MeasurementsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeselectAllMeasurements();

            if (measurementsList.SelectedIndex >= 0 && measurementsList.SelectedIndex < measurements.Count)
            {
                Measurement m = measurements[measurementsList.SelectedIndex];
                m.IsSelected = true;
                measurements[measurementsList.SelectedIndex] = m;
                selectedMeasurementIndex = measurementsList.SelectedIndex;
                selectedMeasurement = m;
            }

            pictureBox.Invalidate();
        }
        private void PictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (pictureBox.Image == null) return;

            // Handle grid dragging
            if (e.Button == MouseButtons.Left && IsNearPoint(e.Location, gridOrigin, gridGrabRadius))
            {
                gridOrigin = e.Location;
                pictureBox.Invalidate();
                return;
            }

            // Handle measurement creation
            if (currentTool != ToolMode.None && e.Button == MouseButtons.Left)
            {
                HandleMeasurementCreation(e.Location);
            }

            // Handle selection for moving or deleting
            if (currentEditMode != EditMode.None && e.Button == MouseButtons.Left)
            {
                HandleSelection(e.Location);
            }
        }

        private void HandleMeasurementCreation(Point location)
        {
            switch (currentTool)
            {
                case ToolMode.Line:
                    if (currentStartPoint == null)
                    {
                        currentStartPoint = location;
                        UpdateStatus("Click endpoint for line");
                    }
                    else
                    {
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            location,
                            $"L{measurementCounter++}",
                            MeasurementType.Line));
                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        pictureBox.Invalidate();
                    }
                    break;

                case ToolMode.Point:
                    measurements.Add(new Measurement(
                        location,
                        location,
                        $"P{measurementCounter++}",
                        MeasurementType.Point));
                    UpdateMeasurementsList();
                    pictureBox.Invalidate();
                    break;

                case ToolMode.Angle:
                    if (angleVertex == null)
                    {
                        angleVertex = location;
                        UpdateStatus("Click first endpoint for angle");
                    }
                    else if (angleFirstPoint == null)
                    {
                        angleFirstPoint = location;
                        UpdateStatus("Click second endpoint for angle");
                    }
                    else
                    {
                        // Create angle measurement with two segments
                        Measurement firstSegment = new Measurement(
                            angleVertex.Value,
                            angleFirstPoint.Value,
                            $"A{measurementCounter}",
                            MeasurementType.Angle);
                        firstSegment.Vertex = angleVertex.Value;
                        measurements.Add(firstSegment);

                        Measurement secondSegment = new Measurement(
                            angleVertex.Value,
                            location,
                            $"A{measurementCounter}",
                            MeasurementType.Angle);
                        secondSegment.Vertex = angleVertex.Value;
                        measurements.Add(secondSegment);

                        measurementCounter++;

                        angleVertex = null;
                        angleFirstPoint = null;
                        UpdateMeasurementsList();
                        pictureBox.Invalidate();
                    }
                    break;
                case ToolMode.AngleWithAxis:
                    if (currentStartPoint == null)
                    {
                        currentStartPoint = location;
                        UpdateStatus("Click endpoint for line");
                    }
                    else
                    {
                        // Create the line measurement
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            location,
                            $"AA{measurementCounter++}",
                            MeasurementType.AngleWithAxis));

                        // Ask for axis reference
                        var axisDialog = new AxisSelectionDialog();
                        if (axisDialog.ShowDialog() == DialogResult.OK)
                        {
                            // Update measurement with axis info
                            Measurement m = measurements[measurements.Count - 1];
                            m.Axis = axisDialog.SelectedAxis;
                            measurements[measurements.Count - 1] = m;
                        }

                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        pictureBox.Invalidate();
                    }
                    break;

                case ToolMode.Distance:
                    if (currentStartPoint == null)
                    {
                        currentStartPoint = location;
                        UpdateStatus("Click endpoint for distance measurement");
                    }
                    else
                    {
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            location,
                            $"D{measurementCounter++}",
                            MeasurementType.Distance));
                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        pictureBox.Invalidate();
                    }
                    break;

                case ToolMode.Reference:
                    if (currentStartPoint == null)
                    {
                        currentStartPoint = location;
                        UpdateStatus("Click endpoint for reference line");
                    }
                    else
                    {
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            location,
                            $"R{measurementCounter++}",
                            MeasurementType.Distance));
                        currentStartPoint = null;
                        isSettingReference = true;
                        UpdateMeasurementsList();
                        pictureBox.Invalidate();

                        // Prompt for reference value
                        using (var inputDialog = new ReferenceInputDialog())
                        {
                            if (inputDialog.ShowDialog() == DialogResult.OK)
                            {
                                float referenceLength = inputDialog.ReferenceLength;
                                SetScaleFromReference(measurements[measurements.Count - 1], referenceLength);
                                UpdateStatus($"Reference set: 1 cm = {pixelToRealRatio:F2} pixels");
                                UpdateMeasurementsList();
                            }
                        }

                        isSettingReference = false;
                    }
                    break;
            }
        }

        private void SetScaleFromReference(Measurement reference, float referenceLength)
        {
            double pixelLength = CalculateDistance(reference.Start, reference.End);
            if (referenceLength > 0 && pixelLength > 0)
            {
                pixelToRealRatio = (float)(pixelLength / referenceLength);
                isReferenceSet = true;

                // Change reference measurement type
                for (int i = 0; i < measurements.Count; i++)
                {
                    if (measurements[i].Name == reference.Name)
                    {
                        Measurement m = measurements[i];
                        m.Type = MeasurementType.ReferenceLine;
                        measurements[i] = m;
                        break;
                    }
                }
            }
        }

        private void HandleSelection(Point location)
        {
            int index = FindMeasurementAtPoint(location);

            if (index >= 0)
            {
                if (currentEditMode == EditMode.Delete)
                {
                    measurements.RemoveAt(index);
                    UpdateMeasurementsList();
                    pictureBox.Invalidate();
                    UpdateStatus("Measurement deleted");
                }
                // Move logic is now handled in MouseDown event
            }
            else
            {
                // Clicked on empty space - deselect all
                DeselectAllMeasurements();
                pictureBox.Invalidate();
            }
        }
        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Handle grid dragging
                if (IsNearPoint(e.Location, gridOrigin, gridGrabRadius))
                {
                    isDraggingGrid = true;
                    return;
                }

                // Handle measurement selection for moving
                if (currentEditMode == EditMode.Move)
                {
                    int index = FindMeasurementAtPoint(e.Location);

                    if (index >= 0)
                    {
                        // Deselect all measurements first
                        DeselectAllMeasurements();

                        // Select the clicked measurement
                        Measurement m = measurements[index];
                        m.IsSelected = true;
                        measurements[index] = m;

                        selectedMeasurementIndex = index;
                        selectedMeasurement = m;

                        // Calculate offset based on where the user clicked on the measurement
                        if (m.Type == MeasurementType.Point)
                        {
                            dragOffset = new Point(
                                e.Location.X - m.Start.X,
                                e.Location.Y - m.Start.Y);
                        }
                        else
                        {
                            // For lines, find the closest point to where user clicked
                            double distanceToStart = CalculateDistance(e.Location, m.Start);
                            double distanceToEnd = CalculateDistance(e.Location, m.End);

                            if (distanceToStart < distanceToEnd)
                            {
                                // User clicked near the start point
                                dragOffset = new Point(
                                    e.Location.X - m.Start.X,
                                    e.Location.Y - m.Start.Y);
                            }
                            else
                            {
                                // User clicked near the end point
                                dragOffset = new Point(
                                    e.Location.X - m.End.X,
                                    e.Location.Y - m.End.Y);
                            }
                        }

                        isDraggingMeasurement = true;
                        pictureBox.Cursor = Cursors.SizeAll;
                        pictureBox.Invalidate(); // Refresh to show selection
                    }
                    else
                    {
                        // Clicked on empty space - deselect all
                        DeselectAllMeasurements();
                        pictureBox.Invalidate();
                    }
                }
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingGrid)
            {
                gridOrigin = e.Location;
                pictureBox.Invalidate();
            }

            if (isDraggingMeasurement && selectedMeasurement.HasValue && selectedMeasurementIndex >= 0)
            {
                MoveMeasurement(selectedMeasurementIndex, e.Location);
                pictureBox.Invalidate();
            }
            else if (currentTool != ToolMode.None)
            {
                // Refresh to show real-time drawing preview
                pictureBox.Invalidate();
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingGrid)
            {
                isDraggingGrid = false;
            }

            if (isDraggingMeasurement)
            {
                isDraggingMeasurement = false;
                pictureBox.Cursor = Cursors.Hand;
                UpdateMeasurementsList(); // Update the list to reflect new positions
            }
        }

        private void MoveMeasurement(int index, Point mouseLocation)
        {
            Measurement m = measurements[index];

            if (m.Type == MeasurementType.Point)
            {
                // Move point to new location (adjusting for offset)
                Point newLocation = new Point(
                    mouseLocation.X - dragOffset.X,
                    mouseLocation.Y - dragOffset.Y);

                m.Start = newLocation;
                m.End = newLocation;
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

                // Find and move the other segment that shares the same vertex and name
                for (int i = 0; i < measurements.Count; i++)
                {
                    if (i != index &&
                        measurements[i].Type == MeasurementType.Angle &&
                        measurements[i].Vertex.HasValue &&
                        measurements[i].Name == m.Name)
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
                // For lines and distance measurements, calculate movement delta
                Point newPosition = new Point(
                    mouseLocation.X - dragOffset.X,
                    mouseLocation.Y - dragOffset.Y);

                // Determine if we're moving from start or end point
                double distanceToStart = CalculateDistance(new Point(mouseLocation.X + dragOffset.X, mouseLocation.Y + dragOffset.Y), m.Start);
                double distanceToEnd = CalculateDistance(new Point(mouseLocation.X + dragOffset.X, mouseLocation.Y + dragOffset.Y), m.End);

                Point delta;
                if (distanceToStart < distanceToEnd)
                {
                    // Moving from start point
                    delta = new Point(
                        newPosition.X - m.Start.X,
                        newPosition.Y - m.Start.Y);
                }
                else
                {
                    // Moving from end point
                    delta = new Point(
                        newPosition.X - m.End.X,
                        newPosition.Y - m.End.Y);
                }

                // Move both endpoints
                m.Start = new Point(m.Start.X + delta.X, m.Start.Y + delta.Y);
                m.End = new Point(m.End.X + delta.X, m.End.Y + delta.Y);
            }

            measurements[index] = m;
        }

        private int FindMeasurementAtPoint(Point point)
        {
            // First check for points and lines
            for (int i = 0; i < measurements.Count; i++)
            {
                if (IsMeasurementAtPoint(measurements[i], point))
                    return i;
            }

            // Then specifically check for angle segments
            return FindAngleMeasurementAtPoint(point);
        }

        private bool IsMeasurementAtPoint(Measurement m, Point point)
        {
            const int tolerance = 8; // Increased tolerance for easier selection

            switch (m.Type)
            {
                case MeasurementType.Point:
                    return IsNearPoint(point, m.Start, tolerance);

                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.AngleWithAxis:
                    return IsPointNearLine(point, m.Start, m.End, tolerance);

                case MeasurementType.Angle:
                    if (m.Vertex.HasValue)
                    {
                        // For angles, check both segments
                        return IsPointNearLine(point, m.Vertex.Value, m.End, tolerance);
                    }
                    return false;

                default:
                    return false;
            }
        }
        private List<Measurement> FindAngleSegments(Point vertex, string name)
        {
            return measurements.Where(m =>
                m.Type == MeasurementType.Angle &&
                m.Vertex.HasValue &&
                m.Vertex.Value == vertex &&
                m.Name == name).ToList();
        }

        private bool IsNearPoint(Point p1, Point p2, int tolerance)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)) <= tolerance;
        }

        private bool IsPointNearLine(Point point, Point lineStart, Point lineEnd, int tolerance)
        {
            // Calculate distance from point to line segment
            double lineLength = CalculateDistance(lineStart, lineEnd);
            if (lineLength == 0) return IsNearPoint(point, lineStart, tolerance);

            // Calculate projection point
            double t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * (lineEnd.X - lineStart.X) +
                 (point.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y)) /
                (lineLength * lineLength)));

            Point projection = new Point(
                (int)(lineStart.X + t * (lineEnd.X - lineStart.X)),
                (int)(lineStart.Y + t * (lineEnd.Y - lineStart.Y)));

            return IsNearPoint(point, projection, tolerance);
        }

        private void DeselectAllMeasurements()
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                Measurement m = measurements[i];
                m.IsSelected = false;
                measurements[i] = m;
            }
            selectedMeasurement = null;
            selectedMeasurementIndex = -1;
            measurementsList.ClearSelected();
        }

        private void UpdateMeasurementsList()
        {
            measurementsList.Items.Clear();

            foreach (var m in measurements)
            {
                string itemText = $"{m.Name}: ";

                switch (m.Type)
                {
                    case MeasurementType.Line:
                        double lineLength = CalculateDistance(m.Start, m.End);
                        itemText += $"{lineLength:F1} px";
                        break;

                    case MeasurementType.Distance:
                        double pixels = CalculateDistance(m.Start, m.End);
                        itemText += $"{pixels:F1} px";

                        if (isReferenceSet)
                        {
                            double realUnits = pixels / pixelToRealRatio;
                            itemText += $" ({realUnits:F2} cm)";
                        }
                        break;

                    case MeasurementType.ReferenceLine:
                        double refPixels = CalculateDistance(m.Start, m.End);
                        double refUnits = refPixels / pixelToRealRatio;
                        itemText += $"{refPixels:F1} px ({refUnits:F2} cm) [Reference]";
                        break;

                    case MeasurementType.Angle:
                        // Only show angle value once for each pair of segments
                        if (m.Name.EndsWith("-1") || !measurements.Any(meas =>
                            meas.Type == MeasurementType.Angle &&
                            meas.Name == m.Name.Replace("-2", "-1") &&
                            meas.Vertex == m.Vertex))
                        {
                            double angle = CalculateAngle(m);
                            itemText += $"{angle:F1}°";
                        }
                        else
                        {
                            // Skip the second segment in the list
                            continue;
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        double axisAngle = CalculateAngleWithAxis(m);
                        itemText += $"{axisAngle:F1}° relative to {m.Axis}-axis";
                        break;

                    case MeasurementType.Point:
                        itemText += $"Point at ({m.Start.X}, {m.Start.Y})";
                        break;
                }

                if (m.IsSelected) itemText += " [Selected]";
                measurementsList.Items.Add(itemText);
            }
        }

        private double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private double CalculateAngle(Measurement m1, Measurement m2)
        {
            if (m1.Type != MeasurementType.Angle || !m1.Vertex.HasValue ||
                m2.Type != MeasurementType.Angle || !m2.Vertex.HasValue) return 0;

            // Calculate vectors from vertex to endpoints
            Point v1 = new Point(m1.End.X - m1.Vertex.Value.X, m1.End.Y - m1.Vertex.Value.Y);
            Point v2 = new Point(m2.End.X - m2.Vertex.Value.X, m2.End.Y - m2.Vertex.Value.Y);

            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0) return 0;

            double cosTheta = Math.Max(-1, Math.Min(1, dotProduct / (mag1 * mag2)));

            // This always returns the smaller angle between the vectors (0-180 degrees)
            return Math.Acos(cosTheta) * (180 / Math.PI);
        }
        // Method to calculate angle for a single measurement (find its pair)
        private double CalculateAngle(Measurement m)
        {
            if (m.Type != MeasurementType.Angle || !m.Vertex.HasValue) return 0;

            // Find the other segment that shares the same vertex and name
            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                meas.Type == MeasurementType.Angle &&
                meas.Vertex.HasValue &&
                meas.Vertex.Value == m.Vertex.Value &&
                meas.Name == m.Name &&
                meas.End != m.End);

            if (otherSegment.Type == MeasurementType.Angle)
            {
                return CalculateAngle(m, otherSegment);
            }

            return 0;
        }
        private double CalculateAngleWithAxis(Measurement m)
        {
            if (m.Type != MeasurementType.AngleWithAxis || !m.Axis.HasValue) return 0;

            // Calculate angle relative to specified axis
            double dx = m.End.X - m.Start.X;
            double dy = m.End.Y - m.Start.Y;

            if (m.Axis == AxisType.X)
                return Math.Abs(Math.Atan2(dy, dx) * (180 / Math.PI));
            else
                return Math.Abs(Math.Atan2(dx, dy) * (180 / Math.PI));
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBox.Image == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw grid if enabled
            if (showGrid)
            {
                DrawGrid(g);
            }

            // Draw measurements
            foreach (var m in measurements)
            {
                DrawMeasurement(g, m);
            }

            // Draw current measurement in progress
            if (currentTool != ToolMode.None)
            {
                Point currentPos = pictureBox.PointToClient(Cursor.Position);

                using (Pen tempPen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash })
                {
                    if (currentTool == ToolMode.Angle)
                    {
                        if (angleVertex.HasValue && angleFirstPoint.HasValue)
                        {
                            // Draw both segments for angle in progress
                            g.DrawLine(tempPen, angleVertex.Value, angleFirstPoint.Value);
                            g.DrawLine(tempPen, angleVertex.Value, currentPos);

                            // Draw angle arc preview
                            DrawAngleArcPreview(g, angleVertex.Value, angleFirstPoint.Value, currentPos);
                        }
                        else if (angleVertex.HasValue)
                        {
                            // Draw first segment
                            g.DrawLine(tempPen, angleVertex.Value, currentPos);
                        }
                    }
                    else if (currentTool == ToolMode.AngleWithAxis)
                    {
                        if (currentStartPoint.HasValue)
                        {
                            g.DrawLine(tempPen, currentStartPoint.Value, currentPos);
                        }
                    }
                    else if (currentStartPoint.HasValue)
                    {
                        g.DrawLine(tempPen, currentStartPoint.Value, currentPos);

                        // Draw helper for 90° angles
                        if (currentTool == ToolMode.Line || currentTool == ToolMode.Distance)
                        {
                            DrawAngleHelpers(g, currentStartPoint.Value, currentPos);
                        }
                    }
                }
            }
        }

        private void DrawAngleArcPreview(Graphics g, Point vertex, Point point1, Point point2)
        {
            // Vecteurs depuis le sommet
            Point v1 = new Point(point1.X - vertex.X, point1.Y - vertex.Y);
            Point v2 = new Point(point2.X - vertex.X, point2.Y - vertex.Y);

            // Vérif : éviter le cas point1 == vertex ou point2 == vertex
            if ((v1.X == 0 && v1.Y == 0) || (v2.X == 0 && v2.Y == 0))
                return;

            // Angles initiaux
            double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
            double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

            if (angle1 < 0) angle1 += 360;
            if (angle2 < 0) angle2 += 360;

            float startAngle, sweepAngle;

            double diff = Math.Abs(angle1 - angle2);

            if (diff <= 180)
            {
                startAngle = (float)Math.Min(angle1, angle2);
                sweepAngle = (float)diff;
            }
            else
            {
                startAngle = (float)Math.Max(angle1, angle2);
                sweepAngle = (float)(360 - diff);

                if (sweepAngle > 180)
                    sweepAngle = 360 - sweepAngle;
            }

            // Vérif : angles valides avant DrawArc
            if (float.IsNaN(startAngle) || float.IsNaN(sweepAngle) ||
                float.IsInfinity(startAngle) || float.IsInfinity(sweepAngle) ||
                Math.Abs(sweepAngle) < 0.01f)
            {
                return;
            }

            // Calcul valeur de l’angle
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            double angleValue = 0;
            if (mag1 > 0 && mag2 > 0)
            {
                double cosTheta = dotProduct / (mag1 * mag2);

                // Clamp pour éviter NaN dû à des imprécisions flottantes
                cosTheta = Math.Max(-1, Math.Min(1, cosTheta));

                angleValue = Math.Acos(cosTheta) * (180 / Math.PI);
            }

            // Dessin de l’arc
            using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 2))
            {
                arcPen.DashStyle = DashStyle.Dash;
                g.DrawArc(arcPen, vertex.X - 30, vertex.Y - 30, 60, 60, startAngle, sweepAngle);
            }

            // Dessin du texte de l’angle
            using (Font font = new Font("Arial", 9))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
            {
                string angleText = $"{angleValue:F1}°";
                SizeF textSize = g.MeasureString(angleText, font);

                RectangleF textRect = new RectangleF(
                    vertex.X - textSize.Width / 2,
                    vertex.Y - textSize.Height - 40,
                    textSize.Width + 4,
                    textSize.Height);

                g.FillRectangle(bgBrush, textRect);
                g.DrawString(angleText, font, textBrush,
                    vertex.X - textSize.Width / 2 + 2,
                    vertex.Y - textSize.Height - 38);
            }
        }

        private void DrawGrid(Graphics g)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(100, Color.LightBlue)))
            using (Pen axisPen = new Pen(Color.Red, 1.5f))
            {
                gridPen.DashStyle = DashStyle.Dot;

                // Draw vertical grid lines
                for (int x = gridOrigin.X % 50; x < pictureBox.Width; x += 50)
                {
                    g.DrawLine(gridPen, x, 0, x, pictureBox.Height);
                }

                // Draw horizontal grid lines
                for (int y = gridOrigin.Y % 50; y < pictureBox.Height; y += 50)
                {
                    g.DrawLine(gridPen, 0, y, pictureBox.Width, y);
                }

                // Draw axes
                g.DrawLine(axisPen, gridOrigin.X, 0, gridOrigin.X, pictureBox.Height); // Y-axis
                g.DrawLine(axisPen, 0, gridOrigin.Y, pictureBox.Width, gridOrigin.Y);   // X-axis

                // Draw grid origin point
                g.FillEllipse(Brushes.Red, gridOrigin.X - 5, gridOrigin.Y - 5, 10, 10);
            }
        }

        private void DrawAngleHelpers(Graphics g, Point start, Point end)
        {
            // Calculate potential perpendicular endpoints for 90° assistance
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;

            // Horizontal helper
            Point horizontalEnd = new Point(end.X, start.Y);
            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Green)) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(helperPen, start, horizontalEnd);
            }

            // Vertical helper
            Point verticalEnd = new Point(start.X, end.Y);
            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Blue)) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(helperPen, start, verticalEnd);
            }

            // Show angle information
            double angle = Math.Atan2(dy, dx) * (180 / Math.PI);
            using (Font font = new Font("Arial", 9))
            using (Brush brush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
            {
                string angleText = $"{angle:F1}°";
                SizeF textSize = g.MeasureString(angleText, font);
                Point midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

                RectangleF textRect = new RectangleF(
                    midPoint.X - textSize.Width / 2,
                    midPoint.Y - textSize.Height - 5,
                    textSize.Width + 4,
                    textSize.Height);

                g.FillRectangle(bgBrush, textRect);
                g.DrawString(angleText, font, brush, midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 3);
            }
        }

        private void DrawMeasurement(Graphics g, Measurement m)
        {
            Color color = m.IsSelected ? Color.Yellow : GetMeasurementColor(m.Type);
            int lineWidth = m.IsSelected ? 3 : 2; // Thicker line for selected measurements
            int pointSize = m.IsSelected ? 8 : 6; // Larger points for selected measurements

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            using (Font font = new Font("Arial", 9))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);

                        // Draw label
                        string label = $"{m.Name} ({m.Start.X}, {m.Start.Y})";
                        SizeF textSize = g.MeasureString(label, font);
                        RectangleF textRect = new RectangleF(
                            m.Start.X + 10, m.Start.Y - textSize.Height / 2,
                            textSize.Width + 4, textSize.Height);
                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(label, font, textBrush, m.Start.X + 12, m.Start.Y - textSize.Height / 2);
                        break;

                    case MeasurementType.Line:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw measurement value
                        double distance = CalculateDistance(m.Start, m.End);
                        string distText = m.Type == MeasurementType.ReferenceLine ?
                            $"{distance / pixelToRealRatio:F1} cm" :
                            isReferenceSet ?
                                $"{distance / pixelToRealRatio:F1} cm" :
                                $"{distance:F1} px";

                        Point midPoint = new Point(
                            (m.Start.X + m.End.X) / 2,
                            (m.Start.Y + m.End.Y) / 2);

                        textSize = g.MeasureString(distText, font);
                        textRect = new RectangleF(
                            midPoint.X - textSize.Width / 2, midPoint.Y - textSize.Height - 10,
                            textSize.Width + 4, textSize.Height);
                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(distText, font, textBrush,
                            midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 8);
                        break;

                    case MeasurementType.Angle:
                        if (m.Vertex.HasValue)
                        {
                            // Draw the segment
                            g.DrawLine(pen, m.Vertex.Value, m.End);
                            g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);
                            g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                            // Find the other segment that shares the same vertex and name
                            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                meas.Type == MeasurementType.Angle &&
                                meas.Vertex.HasValue &&
                                meas.Vertex.Value == m.Vertex.Value &&
                                meas.Name == m.Name &&
                                meas.End != m.End);

                            if (otherSegment.Type == MeasurementType.Angle)
                            {
                                // Draw angle value at vertex
                                double angle = CalculateAngle(m, otherSegment);
                                string angleText = $"{angle:F1}°";

                                textSize = g.MeasureString(angleText, font);
                                g.FillRectangle(bgBrush, m.Vertex.Value.X, m.Vertex.Value.Y, textSize.Width + 4, textSize.Height);
                                g.DrawString(angleText, font, textBrush, m.Vertex.Value.X + 2, m.Vertex.Value.Y);

                                // Draw angle arc
                                DrawAngleArc(g, m, otherSegment);
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw angle value
                        double axisAngle = CalculateAngleWithAxis(m);
                        string axisAngleText = $"{axisAngle:F1}° to {m.Axis}";

                        textSize = g.MeasureString(axisAngleText, font);
                        Point lineMidPoint = new Point(
                            (m.Start.X + m.End.X) / 2,
                            (m.Start.Y + m.End.Y) / 2);
                        g.FillRectangle(bgBrush, lineMidPoint.X, lineMidPoint.Y, textSize.Width + 4, textSize.Height);
                        g.DrawString(axisAngleText, font, textBrush, lineMidPoint.X + 2, lineMidPoint.Y);

                        // Draw angle arc relative to axis
                        DrawAxisAngleArc(g, m);
                        break;
                }
            }
        }

        private int FindAngleMeasurementAtPoint(Point point)
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                if (measurements[i].Type == MeasurementType.Angle &&
                    measurements[i].Vertex.HasValue &&
                    IsPointNearLine(point, measurements[i].Vertex.Value, measurements[i].End, 8))
                {
                    return i;
                }
            }
            return -1;
        }

        private void DrawAngleArc(Graphics g, Measurement m1, Measurement m2)
        {
            if (m1.Type != MeasurementType.Angle || !m1.Vertex.HasValue ||
                m2.Type != MeasurementType.Angle || !m2.Vertex.HasValue) return;

            // Calculate vectors from vertex to endpoints
            Point v1 = new Point(m1.End.X - m1.Vertex.Value.X, m1.End.Y - m1.Vertex.Value.Y);
            Point v2 = new Point(m2.End.X - m2.Vertex.Value.X, m2.End.Y - m2.Vertex.Value.Y);

            // Calculate angles in degrees (0 to 360)
            double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
            double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

            // Ensure angles are positive (0 to 360)
            if (angle1 < 0) angle1 += 360;
            if (angle2 < 0) angle2 += 360;

            // Determine start angle and sweep angle
            float startAngle, sweepAngle;

            // Calculate the smaller angle between the two vectors
            double diff = Math.Abs(angle1 - angle2);
            double smallerAngle = Math.Min(diff, 360 - diff);

            // Always draw the smaller angle (the actual angle between the segments)
            if (diff <= 180)
            {
                startAngle = (float)Math.Min(angle1, angle2);
                sweepAngle = (float)Math.Abs(angle1 - angle2);
            }
            else
            {
                // For angles > 180, we need to draw the complementary angle
                // but we want to show the actual smaller angle
                startAngle = (float)Math.Max(angle1, angle2);
                sweepAngle = (float)(360 - Math.Abs(angle1 - angle2));

                // Adjust to always show the interior angle
                if (sweepAngle > 180) sweepAngle = 360 - sweepAngle;
            }

            using (Pen arcPen = new Pen(Color.FromArgb(100, Color.Orange), 2))
            {
                arcPen.DashStyle = DashStyle.Dash;
                g.DrawArc(arcPen, m1.Vertex.Value.X - 30, m1.Vertex.Value.Y - 30, 60, 60, startAngle, sweepAngle);
            }
        }
        private void DrawAxisAngleArc(Graphics g, Measurement m)
        {
            if (m.Type != MeasurementType.AngleWithAxis || !m.Axis.HasValue) return;

            double angle = CalculateAngleWithAxis(m);
            float startAngle = 0;
            float sweepAngle = (float)angle;

            if (m.Axis == AxisType.X)
            {
                startAngle = 0;
            }
            else
            {
                startAngle = 90;
            }

            Point lineMidPoint = new Point(
                (m.Start.X + m.End.X) / 2,
                (m.Start.Y + m.End.Y) / 2);

            using (Pen arcPen = new Pen(Color.FromArgb(100, Color.Orange), 2))
            {
                arcPen.DashStyle = DashStyle.Dash;
                g.DrawArc(arcPen, lineMidPoint.X - 30, lineMidPoint.Y - 30, 60, 60, startAngle, sweepAngle);
            }
        }

        private Color GetMeasurementColor(MeasurementType type)
        {
            switch (type)
            {
                case MeasurementType.Line: return Color.LimeGreen;
                case MeasurementType.Point: return Color.Magenta;
                case MeasurementType.Angle: return Color.Cyan;
                case MeasurementType.AngleWithAxis: return Color.Blue;
                case MeasurementType.Distance: return Color.Orange;
                case MeasurementType.ReferenceLine: return Color.Red;
                default: return Color.White;
            }
        }

        // Dialog for axis selection
        private class AxisSelectionDialog : Form
        {
            public AxisType SelectedAxis { get; private set; }

            public AxisSelectionDialog()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Select Reference Axis";
                this.Size = new Size(250, 120);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;

                Label label = new Label();
                label.Text = "Select reference axis for angle measurement:";
                label.Location = new Point(10, 10);
                label.Size = new Size(220, 30);

                Button xAxisBtn = new Button();
                xAxisBtn.Text = "X-Axis";
                xAxisBtn.Location = new Point(20, 50);
                xAxisBtn.Size = new Size(80, 25);
                xAxisBtn.Click += (s, e) => { SelectedAxis = AxisType.X; this.DialogResult = DialogResult.OK; };

                Button yAxisBtn = new Button();
                yAxisBtn.Text = "Y-Axis";
                yAxisBtn.Location = new Point(120, 50);
                yAxisBtn.Size = new Size(80, 25);
                yAxisBtn.Click += (s, e) => { SelectedAxis = AxisType.Y; this.DialogResult = DialogResult.OK; };

                this.Controls.Add(label);
                this.Controls.Add(xAxisBtn);
                this.Controls.Add(yAxisBtn);
            }
        }

        // Dialog for reference input
        private class ReferenceInputDialog : Form
        {
            private TextBox textBox;

            public float ReferenceLength { get; private set; }

            public ReferenceInputDialog()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Set Reference Length";
                this.Size = new Size(300, 150);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label label = new Label();
                label.Text = "Enter known length in centimeters:";
                label.Location = new Point(20, 20);
                label.Size = new Size(250, 20);

                textBox = new TextBox();
                textBox.Location = new Point(20, 50);
                textBox.Size = new Size(250, 20);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(60, 80);
                okButton.Size = new Size(75, 25);
                okButton.Click += OkButton_Click;

                Button cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(150, 80);
                cancelButton.Size = new Size(75, 25);

                this.Controls.Add(label);
                this.Controls.Add(textBox);
                this.Controls.Add(okButton);
                this.Controls.Add(cancelButton);
                this.AcceptButton = okButton;
                this.CancelButton = cancelButton;
            }

            private void OkButton_Click(object sender, EventArgs e)
            {
                if (float.TryParse(textBox.Text, out float result) && result > 0)
                {
                    ReferenceLength = result;
                }
                else
                {
                    MessageBox.Show("Please enter a valid positive number.");
                    this.DialogResult = DialogResult.None;
                }
            }
        }


    }

    // Program entry point
    //internal static class Program
    //{
    //    [STAThread]
    //    static void Main()
    //    {
    //        Application.EnableVisualStyles();
    //        Application.SetCompatibleTextRenderingDefault(false);
    //        Application.Run(new MainForm());
    //    }
    //}
}