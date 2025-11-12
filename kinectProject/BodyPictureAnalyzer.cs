using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Diagnostics;

namespace kinectProject
{
    public partial class BodyPictureAnalyzer : Form
    {
        // Enums
        private enum ToolMode { None, Line, Point, Angle, AngleWithAxis, Distance, Reference , Perpendicular }
        private enum EditMode { None, Move, Delete, Rename, Normal }
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
            public int ID; // Unique ID for each measurement

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
            }
        }

        private enum MeasurementType { Line, Point, Angle, AngleWithAxis, Distance, ReferenceLine, PerpendicularLine }

        // Application state
        private ToolMode currentTool = ToolMode.None;
        private EditMode currentEditMode = EditMode.Normal;
        private List<Measurement> measurements = new List<Measurement>();
        private System.Drawing.Image originalImage;
        private Point? currentStartPoint = null;
        private int measurementCounter = 1;
        private int idCounter = 1;
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
        private Point? hoverPoint = null;
        private string hoverMeasurementName = "";
        private Measurement? hoverMeasurement = null;


        private Measurement? selectedLineForPerpendicular = null;
        private bool isSelectingBaseLine = false;

        // UI Controls
        private PictureBox pictureBox;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ListView measurementsList;

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
            this.Size = new Size(1200, 800);
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            //// Toolstrip setup
            //toolStrip = new ToolStrip();
            //toolStrip.Dock = DockStyle.Top;
            //toolStrip.BackColor = Color.FromArgb(62, 62, 64);
            //toolStrip.ForeColor = Color.White;
            // Toolstrip setup - MODIFIER CETTE SECTION
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;
            toolStrip.BackColor = Color.FromArgb(62, 62, 64);
            toolStrip.ForeColor = Color.White;
            toolStrip.RenderMode = ToolStripRenderMode.Professional;

            // Utiliser un renderer personnalisé pour contrôler l'apparence
            toolStrip.Renderer = new CustomToolStripRenderer();
            // Toolstrip buttons
            AddToolButton("📁 Import Image", BtnImport_Click);
            AddToolSeparator();

            AddToolButton("🔍 Normal Mode", (s, e) => SetEditMode(EditMode.Normal));
            AddToolSeparator();

            AddToolButton("📏 Line Tool", (s, e) => SetToolMode(ToolMode.Line));
            AddToolButton("• Point Tool", (s, e) => SetToolMode(ToolMode.Point));
            AddToolButton("⟂ Perpendicular", (s, e) => SetToolMode(ToolMode.Perpendicular));
            AddToolButton("📐 Angle Tool", (s, e) => SetToolMode(ToolMode.Angle));
            AddToolButton("📊 Angle with Axis", (s, e) => SetToolMode(ToolMode.AngleWithAxis));
            AddToolButton("📐 Distance Tool", (s, e) => SetToolMode(ToolMode.Distance));
            AddToolButton("📏 Set Reference", (s, e) => SetToolMode(ToolMode.Reference));

            AddToolSeparator();

            AddToolButton("✏️ Move Mode", (s, e) => SetEditMode(EditMode.Move));
            AddToolButton("🗑️ Delete Mode", (s, e) => SetEditMode(EditMode.Delete));
            AddToolButton("🏷️ Rename Mode", (s, e) => SetEditMode(EditMode.Rename));
            AddToolButton("🧹 Clear All", BtnClear_Click);
            AddToolButton("🔲 Toggle Grid", BtnToggleGrid_Click);
            AddToolButton("📄 Export PDF", (s, e) => ExportToPdf());

            // Picture box setup
            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = Color.FromArgb(37, 37, 38);
            pictureBox.BorderStyle = BorderStyle.FixedSingle;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.MouseClick += PictureBox_MouseClick;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.Paint += PictureBox_Paint;
            pictureBox.MouseLeave += (s, e) => {
                hoverPoint = null;
                hoverMeasurement = null;
                pictureBox.Invalidate();
            };

            // Measurements list (ListView for professional look)
            measurementsList = new ListView();
            measurementsList.Dock = DockStyle.Right;
            measurementsList.Width = 350;
            measurementsList.BackColor = Color.FromArgb(37, 37, 38);
            measurementsList.ForeColor = Color.White;
            measurementsList.BorderStyle = BorderStyle.FixedSingle;
            measurementsList.View = View.Details;
            measurementsList.FullRowSelect = true;
            measurementsList.GridLines = true;
            measurementsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;

            // Add columns
            measurementsList.Columns.Add("ID", 50);
            measurementsList.Columns.Add("Type", 80);
            measurementsList.Columns.Add("Name", 80);
            measurementsList.Columns.Add("Value", 120);

            measurementsList.SelectedIndexChanged += MeasurementsList_SelectedIndexChanged;

            // Status strip
            statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Bottom;
            statusStrip.BackColor = Color.FromArgb(62, 62, 64);
            statusStrip.ForeColor = Color.White;

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
            button.BackColor = Color.FromArgb(62, 62, 64);
            button.ForeColor = Color.White;
            button.MouseEnter += (s, e) => { button.BackColor = Color.FromArgb(87, 87, 90); };
            button.MouseLeave += (s, e) => { button.BackColor = Color.FromArgb(62, 62, 64); };
            toolStrip.Items.Add(button);
        }

        private void AddToolSeparator()
        {
            var separator = new ToolStripSeparator();
            separator.ForeColor = Color.Gray;
            toolStrip.Items.Add(separator);
        }

        private void SetToolMode(ToolMode mode)
        {
            currentTool = mode;
            currentEditMode = EditMode.None;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;
            selectedLineForPerpendicular = null; // ← Ajouter cette ligne
            isSelectingBaseLine = false;         // ← Ajouter cette ligne

            string statusText = "";
            switch (mode)
            {
                case ToolMode.Line: statusText = "Line Tool: Click to place start and end points"; break;
                case ToolMode.Point: statusText = "Point Tool: Click to place a point"; break;
                case ToolMode.Angle: statusText = "Angle Tool: Click to place vertex, then two end points"; break;
                case ToolMode.AngleWithAxis: statusText = "Angle with Axis: Draw a line, then select axis"; break;
                case ToolMode.Distance: statusText = "Distance Tool: Click to measure distance"; break;
                case ToolMode.Reference: statusText = "Reference Tool: Draw a line of known length"; break;
                case ToolMode.Perpendicular: statusText = "Perpendicular Tool: Select a line first, then click to place perpendicular line";  break;
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

            selectedLineForPerpendicular = null; // ← Ajouter cette ligne
            isSelectingBaseLine = false;         // ← Ajouter cette ligne

            string statusText = "";
            switch (mode)
            {
                case EditMode.Normal:
                    statusText = "Normal Mode: Hover over measurements to see details";
                    pictureBox.Cursor = Cursors.Default;
                    break;
                case EditMode.Delete:
                    statusText = "Delete Mode: Click on measurement to delete";
                    pictureBox.Cursor = Cursors.No;
                    break;
                case EditMode.Move:
                    statusText = "Move Mode: Click and drag to move measurement";
                    pictureBox.Cursor = Cursors.Hand;
                    break;
                case EditMode.Rename:
                    statusText = "Rename Mode: Click on measurement to rename";
                    pictureBox.Cursor = Cursors.UpArrow;
                    break;
            }

            UpdateStatus(statusText);
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
                        originalImage = System.Drawing.Image.FromFile(openFileDialog.FileName);
                        pictureBox.Image = (System.Drawing.Image)originalImage.Clone();

                        // Initialize grid at center
                        gridOrigin = new Point(pictureBox.Width / 2, pictureBox.Height / 2);

                        measurements.Clear();
                        measurementsList.Items.Clear();
                        measurementCounter = 1;
                        idCounter = 1;
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
            idCounter = 1;
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

            if (measurementsList.SelectedItems.Count > 0)
            {
                int selectedId = int.Parse(measurementsList.SelectedItems[0].Text);
                int index = measurements.FindIndex(m => m.ID == selectedId);

                if (index >= 0)
                {
                    Measurement m = measurements[index];
                    m.IsSelected = true;
                    measurements[index] = m;
                    selectedMeasurementIndex = index;
                    selectedMeasurement = m;
                }
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

            // Handle selection for moving, deleting, or renaming
            if (currentEditMode != EditMode.None && currentEditMode != EditMode.Normal && e.Button == MouseButtons.Left)
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
                            MeasurementType.Line,
                            idCounter++));
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
                        MeasurementType.Point,
                        idCounter++));
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
                        int angleId = idCounter++;
                        Measurement firstSegment = new Measurement(
                            angleVertex.Value,
                            angleFirstPoint.Value,
                            $"A{measurementCounter}",
                            MeasurementType.Angle,
                            angleId);
                        firstSegment.Vertex = angleVertex.Value;
                        measurements.Add(firstSegment);

                        Measurement secondSegment = new Measurement(
                            angleVertex.Value,
                            location,
                            $"A{measurementCounter}",
                            MeasurementType.Angle,
                            angleId);
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
                            MeasurementType.AngleWithAxis,
                            idCounter++));

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
                            MeasurementType.Distance,
                            idCounter++));
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
                            MeasurementType.Distance,
                            idCounter++));
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
                case ToolMode.Perpendicular:
                    if (!isSelectingBaseLine)
                    {
                        // First click: select the base line
                        int lineIndex = FindMeasurementAtPoint(location);
                        if (lineIndex >= 0 && (measurements[lineIndex].Type == MeasurementType.Line ||
                                              measurements[lineIndex].Type == MeasurementType.Distance))
                        {
                            selectedLineForPerpendicular = measurements[lineIndex];
                            isSelectingBaseLine = true;
                            UpdateStatus("Base line selected. Now click to place perpendicular line endpoint");

                            // Highlight the selected line
                            DeselectAllMeasurements();
                            Measurement m = measurements[lineIndex];
                            m.IsSelected = true;
                            measurements[lineIndex] = m;
                            pictureBox.Invalidate();
                        }
                        else
                        {
                            UpdateStatus("Please select a valid line first");
                        }
                    }
                    else
                    {
                        // Second click: create perpendicular line
                        if (selectedLineForPerpendicular.HasValue)
                        {
                            CreatePerpendicularLine(selectedLineForPerpendicular.Value, location);
                            isSelectingBaseLine = false;
                            selectedLineForPerpendicular = null;
                            DeselectAllMeasurements();
                            UpdateMeasurementsList();
                            pictureBox.Invalidate();
                        }
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
                else if (currentEditMode == EditMode.Rename)
                {
                    RenameMeasurement(index);
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

        private void RenameMeasurement(int index)
        {
            Measurement m = measurements[index];

            using (var renameDialog = new RenameDialog(m.Name))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    m.Name = renameDialog.NewName;
                    measurements[index] = m;
                    UpdateMeasurementsList();
                    pictureBox.Invalidate();
                    UpdateStatus($"Measurement renamed to {m.Name}");
                }
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
            else
            {
                // Handle hover effect for all measurements in Normal mode
                Point? previousHoverPoint = hoverPoint;
                string previousHoverName = hoverMeasurementName;
                Measurement? previousHoverMeasurement = hoverMeasurement;

                int index = FindMeasurementAtPoint(e.Location);
                if (index >= 0)
                {
                    hoverMeasurement = measurements[index];
                    hoverPoint = GetHoverPointForMeasurement(hoverMeasurement.Value, e.Location);
                    hoverMeasurementName = GetHoverTextForMeasurement(hoverMeasurement.Value);
                }
                else
                {
                    hoverPoint = null;
                    hoverMeasurementName = "";
                    hoverMeasurement = null;
                }

                // Only invalidate if hover state changed - FIXED COMPARISON
                bool hoverPointChanged = hoverPoint != previousHoverPoint;
                bool hoverNameChanged = hoverMeasurementName != previousHoverName;
                bool hoverMeasurementChanged = !Nullable.Equals(hoverMeasurement, previousHoverMeasurement);

                if (hoverPointChanged || hoverNameChanged || hoverMeasurementChanged)
                {
                    pictureBox.Invalidate();
                }
            }
        }

        private Point GetHoverPointForMeasurement(Measurement m, Point mouseLocation)
        {
            switch (m.Type)
            {
                case MeasurementType.Point:
                    return m.Start;
                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.AngleWithAxis:
                    // Return midpoint for lines
                    return new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                case MeasurementType.Angle:
                    if (m.Vertex.HasValue)
                        return m.Vertex.Value;
                    else
                        return new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                default:
                    return mouseLocation;
            }
        }

        private string GetHoverTextForMeasurement(Measurement m)
        {
            switch (m.Type)
            {
                case MeasurementType.Point:
                    return $"{m.Name} (ID: {m.ID}) - ({m.Start.X}, {m.Start.Y})";
                case MeasurementType.Line:
                    double lineLength = CalculateDistance(m.Start, m.End);
                    return $"{m.Name} (ID: {m.ID}): {lineLength:F1} px";
                case MeasurementType.Distance:
                    double pixels = CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = pixels / pixelToRealRatio;
                        return $"{m.Name} (ID: {m.ID}): {pixels:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{m.Name} (ID: {m.ID}): {pixels:F1} px";
                case MeasurementType.ReferenceLine:
                    double refPixels = CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{m.Name} (ID: {m.ID}): {refPixels:F1} px ({refUnits:F2} cm) [Reference]";
                case MeasurementType.Angle:
                    double angle = CalculateAngle(m);
                    return $"{m.Name} (ID: {m.ID}): {angle:F1}°";
                case MeasurementType.AngleWithAxis:
                    double axisAngle = CalculateAngleWithAxis(m);
                    return $"{m.Name} (ID: {m.ID}): {axisAngle:F1}° to {m.Axis}-axis";
                default:
                    return $"{m.Name} (ID: {m.ID})";
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
                case MeasurementType.PerpendicularLine: // ← Ajouter cette ligne
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

        private List<Measurement> FindAngleSegments(Point vertex, int id)
        {
            return measurements.Where(m =>
                m.Type == MeasurementType.Angle &&
                m.Vertex.HasValue &&
                m.Vertex.Value == vertex &&
                m.ID == id).ToList();
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
            measurementsList.SelectedItems.Clear();
        }

        private void UpdateMeasurementsList()
        {
            measurementsList.Items.Clear();

            // Group angle measurements by ID to avoid duplicates
            var groupedMeasurements = measurements
                .GroupBy(m => m.ID)
                .Select(g => g.First())
                .OrderBy(m => m.ID)
                .ToList();

            foreach (var m in groupedMeasurements)
            {
                string valueText = GetMeasurementValueText(m);

                ListViewItem item = new ListViewItem(m.ID.ToString());
                item.SubItems.Add(GetMeasurementTypeString(m.Type));
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

        private string GetMeasurementTypeString(MeasurementType type)
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

        private string GetMeasurementValueText(Measurement m)
        {
            switch (m.Type)
            {
                case MeasurementType.Line:
                    double lineLength = CalculateDistance(m.Start, m.End);
                    return $"{lineLength:F1} px";

                case MeasurementType.Distance:
                    double pixels = CalculateDistance(m.Start, m.End);
                    if (isReferenceSet)
                    {
                        double realUnits = pixels / pixelToRealRatio;
                        return $"{pixels:F1} px ({realUnits:F2} cm)";
                    }
                    return $"{pixels:F1} px";

                case MeasurementType.ReferenceLine:
                    double refPixels = CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{refPixels:F1} px ({refUnits:F2} cm)";

                case MeasurementType.Angle:
                    double angle = CalculateAngle(m);
                    return $"{angle:F1}°";

                case MeasurementType.AngleWithAxis:
                    double axisAngle = CalculateAngleWithAxis(m);
                    return $"{axisAngle:F1}° to {m.Axis}";

                case MeasurementType.Point:
                    return $"({m.Start.X}, {m.Start.Y})";
                case MeasurementType.PerpendicularLine: // ← Ajouter ce cas
                    double perpLength = CalculateDistance(m.Start, m.End);
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

            // Draw hover label for all measurements in Normal mode
            if (currentEditMode == EditMode.Normal && hoverPoint.HasValue && !string.IsNullOrEmpty(hoverMeasurementName))
            {
                DrawHoverLabel(g, hoverPoint.Value, hoverMeasurementName);
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
                    else if (currentTool == ToolMode.Perpendicular && isSelectingBaseLine && selectedLineForPerpendicular.HasValue)
                    {
                        Point currentPosi = pictureBox.PointToClient(Cursor.Position);

                        // Draw preview of the perpendicular line
                        if (selectedLineForPerpendicular.HasValue)
                        {
                            Point foot = CalculatePerpendicularFoot(selectedLineForPerpendicular.Value, currentPosi);

                            using (Pen previewPen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                            {
                                g.DrawLine(previewPen, foot, currentPosi);
                            }

                            // Draw perpendicular symbol (small square at the intersection)
                            using (Brush symbolBrush = new SolidBrush(Color.Cyan))
                            {
                                g.FillRectangle(symbolBrush, foot.X - 3, foot.Y - 3, 6, 6);
                            }
                        }
                    }
                }
            }
        }

        private Point CalculatePerpendicularFoot(Measurement baseLine, Point point)
        {
            Point A = baseLine.Start;
            Point B = baseLine.End;

            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0) return A;

            double t = ((point.X - A.X) * dx + (point.Y - A.Y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));

            return new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy)
            );
        }

        private void DrawHoverLabel(Graphics g, Point point, string text)
        {
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 9, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(text, font);

                // Position label above the point
                RectangleF textRect = new RectangleF(
                    point.X - textSize.Width / 2,
                    point.Y - textSize.Height - 15,
                    textSize.Width + 8,
                    textSize.Height + 4);

                // Draw background with rounded corners
                g.FillRectangle(bgBrush, textRect);
                g.DrawRectangle(Pens.White, textRect.X, textRect.Y, textRect.Width, textRect.Height);

                // Draw text
                g.DrawString(text, font, textBrush,
                    point.X - textSize.Width / 2 + 4,
                    point.Y - textSize.Height - 13);
            }
        }

        private void DrawAngleArcPreview(Graphics g, Point vertex, Point point1, Point point2)
        {
            // Vectors from vertex
            Point v1 = new Point(point1.X - vertex.X, point1.Y - vertex.Y);
            Point v2 = new Point(point2.X - vertex.X, point2.Y - vertex.Y);

            // Check: avoid case where point1 == vertex or point2 == vertex
            if ((v1.X == 0 && v1.Y == 0) || (v2.X == 0 && v2.Y == 0))
                return;

            // Initial angles
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

            // Check: valid angles before DrawArc
            if (float.IsNaN(startAngle) || float.IsNaN(sweepAngle) ||
                float.IsInfinity(startAngle) || float.IsInfinity(sweepAngle) ||
                Math.Abs(sweepAngle) < 0.01f)
            {
                return;
            }

            // Calculate angle value
            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            double angleValue = 0;
            if (mag1 > 0 && mag2 > 0)
            {
                double cosTheta = dotProduct / (mag1 * mag2);

                // Clamp to avoid NaN due to floating point inaccuracies
                cosTheta = Math.Max(-1, Math.Min(1, cosTheta));

                angleValue = Math.Acos(cosTheta) * (180 / Math.PI);
            }

            // Draw arc
            using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 2))
            {
                arcPen.DashStyle = DashStyle.Dash;
                g.DrawArc(arcPen, vertex.X - 30, vertex.Y - 30, 60, 60, startAngle, sweepAngle);
            }

            // Draw angle text
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 9))
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
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 9))
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
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 9, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);

                        // Draw ID near the point
                        string pointId = m.ID.ToString();
                        SizeF idSize = g.MeasureString(pointId, font);
                        RectangleF idRect = new RectangleF(
                            m.Start.X + 8, m.Start.Y - idSize.Height / 2,
                            idSize.Width + 4, idSize.Height);
                        g.FillRectangle(bgBrush, idRect);
                        g.DrawString(pointId, font, textBrush, m.Start.X + 10, m.Start.Y - idSize.Height / 2);
                        break;

                    case MeasurementType.Line:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw ID at midpoint
                        string lineId = m.ID.ToString();
                        SizeF lineIdSize = g.MeasureString(lineId, font);
                        Point lineMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        RectangleF lineIdRect = new RectangleF(
                            lineMidPoint.X - lineIdSize.Width / 2, lineMidPoint.Y - lineIdSize.Height - 10,
                            lineIdSize.Width + 4, lineIdSize.Height);
                        g.FillRectangle(bgBrush, lineIdRect);
                        g.DrawString(lineId, font, textBrush, lineMidPoint.X - lineIdSize.Width / 2 + 2, lineMidPoint.Y - lineIdSize.Height - 8);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw measurement value with ID
                        double distance = CalculateDistance(m.Start, m.End);
                        string distText = m.Type == MeasurementType.ReferenceLine ?
                            $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                            isReferenceSet ?
                                $"{m.ID}" :
                                $"{m.ID}";

                        Point midPoint = new Point(
                            (m.Start.X + m.End.X) / 2,
                            (m.Start.Y + m.End.Y) / 2);

                        SizeF textSize = g.MeasureString(distText, font);
                        RectangleF textRect = new RectangleF(
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

                            // Find the other segment that shares the same vertex and ID
                            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                meas.Type == MeasurementType.Angle &&
                                meas.Vertex.HasValue &&
                                meas.Vertex.Value == m.Vertex.Value &&
                                meas.ID == m.ID &&
                                meas.End != m.End);

                            if (otherSegment.Type == MeasurementType.Angle)
                            {
                                // Draw angle value at vertex with ID
                                double angle = CalculateAngle(m, otherSegment);
                                string angleText = $"{m.ID}";

                                SizeF angleTextSize = g.MeasureString(angleText, font);
                                RectangleF angleTextRect = new RectangleF(
                                    m.Vertex.Value.X - angleTextSize.Width / 2,
                                    m.Vertex.Value.Y - angleTextSize.Height - 20,
                                    angleTextSize.Width + 4,
                                    angleTextSize.Height);
                                g.FillRectangle(bgBrush, angleTextRect);
                                g.DrawString(angleText, font, textBrush,
                                    m.Vertex.Value.X - angleTextSize.Width / 2 + 2,
                                    m.Vertex.Value.Y - angleTextSize.Height - 18);

                                // Draw angle arc
                                DrawAngleArc(g, m, otherSegment);
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw angle value with ID
                        double axisAngle = CalculateAngleWithAxis(m);
                        string axisAngleText = $"{m.ID}";

                        SizeF axisTextSize = g.MeasureString(axisAngleText, font);
                        Point lineMidPoint1 = new Point(
                            (m.Start.X + m.End.X) / 2,
                            (m.Start.Y + m.End.Y) / 2);
                        RectangleF axisTextRect = new RectangleF(
                            lineMidPoint1.X - axisTextSize.Width / 2,
                            lineMidPoint1.Y - axisTextSize.Height - 10,
                            axisTextSize.Width + 4,
                            axisTextSize.Height);
                        g.FillRectangle(bgBrush, axisTextRect);
                        g.DrawString(axisAngleText, font, textBrush,
                            lineMidPoint1.X - axisTextSize.Width / 2 + 2,
                            lineMidPoint1.Y - axisTextSize.Height - 8);

                        // Draw angle arc relative to axis
                        DrawAxisAngleArc(g, m);
                        break;

                    case MeasurementType.PerpendicularLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // Draw perpendicular symbol at the intersection point
                        using (Pen perpendicularPen = new Pen(Color.White, 1))
                        {
                            int symbolSize = 4;
                            g.DrawRectangle(perpendicularPen, m.Start.X - symbolSize, m.Start.Y - symbolSize, symbolSize * 2, symbolSize * 2);
                        }

                        // Draw ID
                        string perpId = m.ID.ToString();
                        SizeF perpTextSize = g.MeasureString(perpId, font);
                        Point perpMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        RectangleF perpTextRect = new RectangleF(
                            perpMidPoint.X - perpTextSize.Width / 2, perpMidPoint.Y - perpTextSize.Height - 10,
                            perpTextSize.Width + 4, perpTextSize.Height);
                        g.FillRectangle(bgBrush, perpTextRect);
                        g.DrawString(perpId, font, textBrush, perpMidPoint.X - perpTextSize.Width / 2 + 2, perpMidPoint.Y - perpTextSize.Height - 8);
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
                case MeasurementType.PerpendicularLine: return Color.Violet;
                default: return Color.White;
            }
        }

        private void ExportToPdf()
        {
            if (pictureBox.Image == null)
            {
                MessageBox.Show("Please load an image first.", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF Files|*.pdf";
                saveDialog.Title = "Export Measurements as PDF";
                saveDialog.FileName = $"Measurement_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CreatePdfReport(saveDialog.FileName);
                        MessageBox.Show($"PDF exported successfully to:\n{saveDialog.FileName}",
                            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Optionally open the PDF after creation
                        if (MessageBox.Show("Would you like to open the PDF now?", "Open PDF",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            Process.Start(saveDialog.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error creating PDF: {ex.Message}", "Export Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CreatePdfReport(string filePath)
        {
            // Create document with margins
            Document document = new Document(PageSize.A4, 36, 36, 36, 36);
            PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));
            document.Open();

            // Add title
            iTextSharp.text.Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
            Paragraph title = new Paragraph("Body Measurement Analysis Report", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 20;
            document.Add(title);

            // Add creation date
            iTextSharp.text.Font dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
            Paragraph date = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", dateFont);
            date.Alignment = Element.ALIGN_CENTER;
            date.SpacingAfter = 20;
            document.Add(date);

            // Prepare the image first to calculate its height
            iTextSharp.text.Image pdfImage = null;
            if (pictureBox.Image != null && originalImage != null)
            {
                try
                {
                    // Calculate scaling factors between PictureBox and original image
                    float scaleX = (float)originalImage.Width / pictureBox.ClientSize.Width;
                    float scaleY = (float)originalImage.Height / pictureBox.ClientSize.Height;

                    // For PictureBoxSizeMode.Zoom, we need to calculate the actual image display area
                    Size imageSize = originalImage.Size;
                    Size containerSize = pictureBox.ClientSize;

                    float ratioX = (float)containerSize.Width / imageSize.Width;
                    float ratioY = (float)containerSize.Height / imageSize.Height;
                    float ratio = Math.Min(ratioX, ratioY);

                    int newWidth = (int)(imageSize.Width * ratio);
                    int newHeight = (int)(imageSize.Height * ratio);

                    int offsetX = (containerSize.Width - newWidth) / 2;
                    int offsetY = (containerSize.Height - newHeight) / 2;

                    // Calculate the inverse scaling for measurements
                    float inverseScaleX = (float)originalImage.Width / newWidth;
                    float inverseScaleY = (float)originalImage.Height / newHeight;

                    // Create a bitmap with the original image dimensions
                    using (Bitmap bmp = new Bitmap(originalImage.Width, originalImage.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                            // Draw measurements on the image with scaled coordinates
                            foreach (var m in measurements)
                            {
                                DrawMeasurementOnBitmap(g, m, offsetX, offsetY, inverseScaleX, inverseScaleY);
                            }
                        }

                        // Save to temporary file
                        string tempImagePath = Path.GetTempFileName() + ".png";
                        bmp.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);

                        // Create PDF image and scale it
                        pdfImage = iTextSharp.text.Image.GetInstance(tempImagePath);
                        pdfImage.Alignment = Element.ALIGN_CENTER;

                        // Scale image to fit page width while maintaining aspect ratio
                        float maxWidth = document.PageSize.Width - 72; // 1 inch margins
                        float maxHeight = document.PageSize.Height - 200; // Leave more space

                        if (pdfImage.Width > maxWidth || pdfImage.Height > maxHeight)
                        {
                            pdfImage.ScaleToFit(maxWidth, maxHeight);
                        }

                        // Check if there's enough space on current page for the image
                        float imageHeight = pdfImage.ScaledHeight + 40; // Add some margin
                        float currentVerticalPosition = writer.GetVerticalPosition(false);

                        if (currentVerticalPosition - imageHeight < document.BottomMargin)
                        {
                            // Not enough space - add new page
                            document.NewPage();
                        }

                        pdfImage.SpacingAfter = 20;
                        document.Add(pdfImage);

                        // Clean up temporary file
                        File.Delete(tempImagePath);
                    }
                }
                catch (Exception ex)
                {
                    document.Add(new Paragraph($"Error adding image to PDF: {ex.Message}"));
                }
            }

            // Add measurements table if there are any
            if (measurements.Any())
            {
                // Check if we need a new page for the table
                float tableEstimatedHeight = measurements.Count * 20 + 50; // Estimate table height
                float currentVerticalPosition = writer.GetVerticalPosition(false);

                if (currentVerticalPosition - tableEstimatedHeight < document.BottomMargin + 100)
                {
                    // Not enough space - add new page
                    document.NewPage();
                }

                // Add measurements header
                iTextSharp.text.Font tableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.DARK_GRAY);
                Paragraph measurementsHeader = new Paragraph("Measurement Summary", tableHeaderFont);
                measurementsHeader.SpacingBefore = 10;
                measurementsHeader.SpacingAfter = 10;
                document.Add(measurementsHeader);

                // Create measurements table with ID column
                PdfPTable table = new PdfPTable(5);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1, 2, 3, 2, 3 });

                // Add table headers
                iTextSharp.text.Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                AddTableHeaderCell(table, "ID", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Type", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Name", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Pixel Value", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Real Value", headerFont, BaseColor.DARK_GRAY);

                // Group measurements by ID to avoid duplicates
                var groupedMeasurements = measurements
                    .GroupBy(m => m.ID)
                    .Select(g => g.First())
                    .OrderBy(m => m.ID)
                    .ToList();

                // Add measurement rows
                iTextSharp.text.Font cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                foreach (var m in groupedMeasurements)
                {
                    AddMeasurementToTable(table, m, cellFont);
                }

                document.Add(table);
            }
            else
            {
                Paragraph noMeasurements = new Paragraph("No measurements recorded.",
                    FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.GRAY));
                noMeasurements.SpacingBefore = 10;
                document.Add(noMeasurements);
            }

            // Add reference scale information if available
            if (isReferenceSet)
            {
                // Check if we need a new page
                float currentVerticalPosition = writer.GetVerticalPosition(false);
                if (currentVerticalPosition < document.BottomMargin + 50)
                {
                    document.NewPage();
                }

                Paragraph scaleInfo = new Paragraph($"Reference Scale: 1 cm = {pixelToRealRatio:F2} pixels",
                    FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.GRAY));
                scaleInfo.SpacingBefore = 10;
                document.Add(scaleInfo);
            }

            // Add footer
            Paragraph footer = new Paragraph("Generated by Body Measurement Analysis Tool",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.LIGHT_GRAY));
            footer.Alignment = Element.ALIGN_RIGHT;
            footer.SpacingBefore = 20;
            document.Add(footer);

            document.Close();
        }
        private void DrawMeasurementOnBitmap(Graphics g, Measurement m, int offsetX, int offsetY, float scaleX, float scaleY)
        {
            // Scale the measurement coordinates from PictureBox coordinates to original image coordinates
            Point ScalePoint(Point point)
            {
                // Adjust for zoom and center offset, then scale to original image size
                int scaledX = (int)((point.X - offsetX) * scaleX);
                int scaledY = (int)((point.Y - offsetY) * scaleY);

                // Ensure coordinates are within image bounds
                scaledX = Math.Max(0, Math.Min(scaledX, originalImage.Width - 1));
                scaledY = Math.Max(0, Math.Min(scaledY, originalImage.Height - 1));

                return new Point(scaledX, scaledY);
            }

            // Similar to DrawMeasurement but for the PDF export bitmap with scaled coordinates
            Color color = GetMeasurementColor(m.Type);
            int lineWidth = 2;
            int pointSize = 6;

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 10, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                // Scale all points
                Point scaledStart = ScalePoint(m.Start);
                Point scaledEnd = ScalePoint(m.End);
                Point? scaledVertex = m.Vertex.HasValue ? ScalePoint(m.Vertex.Value) : (Point?)null;

                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, scaledStart.X - pointSize / 2, scaledStart.Y - pointSize / 2, pointSize, pointSize);
                        g.DrawString(m.ID.ToString(), font, textBrush, scaledStart.X + 5, scaledStart.Y - 10);
                        break;

                    case MeasurementType.Line:
                        g.DrawLine(pen, scaledStart, scaledEnd);
                        g.FillEllipse(brush, scaledStart.X - pointSize / 2, scaledStart.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, scaledEnd.X - pointSize / 2, scaledEnd.Y - pointSize / 2, pointSize, pointSize);
                        Point lineMidPoint = new Point((scaledStart.X + scaledEnd.X) / 2, (scaledStart.Y + scaledEnd.Y) / 2);
                        g.DrawString(m.ID.ToString(), font, textBrush, lineMidPoint.X, lineMidPoint.Y - 15);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, scaledStart, scaledEnd);
                        g.FillEllipse(brush, scaledStart.X - pointSize / 2, scaledStart.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, scaledEnd.X - pointSize / 2, scaledEnd.Y - pointSize / 2, pointSize, pointSize);

                        double distance = CalculateDistance(m.Start, m.End);
                        string distText = m.Type == MeasurementType.ReferenceLine ?
                            $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                            isReferenceSet ?
                                $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                                $"{m.ID}: {distance:F1} px";

                        Point midPoint = new Point((scaledStart.X + scaledEnd.X) / 2, (scaledStart.Y + scaledEnd.Y) / 2);
                        g.DrawString(distText, font, textBrush, midPoint.X, midPoint.Y - 15);
                        break;

                    case MeasurementType.Angle:
                        if (scaledVertex.HasValue)
                        {
                            g.DrawLine(pen, scaledVertex.Value, scaledEnd);
                            g.FillEllipse(brush, scaledVertex.Value.X - pointSize / 2, scaledVertex.Value.Y - pointSize / 2, pointSize, pointSize);
                            g.FillEllipse(brush, scaledEnd.X - pointSize / 2, scaledEnd.Y - pointSize / 2, pointSize, pointSize);

                            // Find the other segment that shares the same vertex and ID
                            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                meas.Type == MeasurementType.Angle &&
                                meas.Vertex.HasValue &&
                                meas.ID == m.ID &&
                                meas.End != m.End);

                            if (otherSegment.Type == MeasurementType.Angle)
                            {
                                Point scaledOtherEnd = ScalePoint(otherSegment.End);
                                double angle = CalculateAngle(m, otherSegment);
                                string angleText = $"{m.ID}: {angle:F1}°";
                                g.DrawString(angleText, font, textBrush, scaledVertex.Value.X, scaledVertex.Value.Y - 20);
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, scaledStart, scaledEnd);
                        g.FillEllipse(brush, scaledStart.X - pointSize / 2, scaledStart.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, scaledEnd.X - pointSize / 2, scaledEnd.Y - pointSize / 2, pointSize, pointSize);

                        double axisAngle = CalculateAngleWithAxis(m);
                        string axisAngleText = $"{m.ID}: {axisAngle:F1}° to {m.Axis}";
                        Point axisMidPoint = new Point((scaledStart.X + scaledEnd.X) / 2, (scaledStart.Y + scaledEnd.Y) / 2);
                        g.DrawString(axisAngleText, font, textBrush, axisMidPoint.X, axisMidPoint.Y - 15);
                        break;
                    case MeasurementType.PerpendicularLine: // ← Ajouter ce cas
                        g.DrawLine(pen, scaledStart, scaledEnd);
                        g.FillEllipse(brush, scaledStart.X - pointSize / 2, scaledStart.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, scaledEnd.X - pointSize / 2, scaledEnd.Y - pointSize / 2, pointSize, pointSize);

                        double perpLength = CalculateDistance(m.Start, m.End);
                        string perpText = $"{m.ID}: "; 

                        Point perpMidPoint = new Point((scaledStart.X + scaledEnd.X) / 2, (scaledStart.Y + scaledEnd.Y) / 2);
                        g.DrawString(perpText, font, textBrush, perpMidPoint.X, perpMidPoint.Y - 15);

                        // Draw perpendicular symbol
                        using (Pen symbolPen = new Pen(Color.Black, 1))
                        {
                            g.DrawRectangle(symbolPen, scaledStart.X - 2, scaledStart.Y - 2, 4, 4);
                        }
                        break;
                }
            }
        }

        private void AddTableHeaderCell(PdfPTable table, string text, iTextSharp.text.Font font, BaseColor bgColor)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        private void AddMeasurementToTable(PdfPTable table, Measurement m, iTextSharp.text.Font font)
        {
            // ID column
            table.AddCell(new PdfPCell(new Phrase(m.ID.ToString(), font)) { Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });

            // Type column
            table.AddCell(new PdfPCell(new Phrase(GetMeasurementTypeString(m.Type), font)) { Padding = 5 });

            // Name column
            table.AddCell(new PdfPCell(new Phrase(m.Name, font)) { Padding = 5 });

            // Pixel Value column
            string pixelValue = GetPixelValueString(m);
            table.AddCell(new PdfPCell(new Phrase(pixelValue, font)) { Padding = 5, HorizontalAlignment = Element.ALIGN_RIGHT });

            // Real Value column
            string realValue = GetRealValueString(m);
            table.AddCell(new PdfPCell(new Phrase(realValue, font)) { Padding = 5, HorizontalAlignment = Element.ALIGN_RIGHT });
        }

        private string GetPixelValueString(Measurement m)
        {
            switch (m.Type)
            {
                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.PerpendicularLine: // ← Ajouter cette ligne
                    double pixels = CalculateDistance(m.Start, m.End);
                    return $"{pixels:F1} px";

                case MeasurementType.Angle:
                    double angle = CalculateAngle(m);
                    return $"{angle:F1}°";

                case MeasurementType.AngleWithAxis:
                    double axisAngle = CalculateAngleWithAxis(m);
                    return $"{axisAngle:F1}°";

                case MeasurementType.Point:
                    return $"({m.Start.X}, {m.Start.Y})";

                default:
                    return "-";
            }
        }


        private string GetRealValueString(Measurement m)
        {
            if (!isReferenceSet && m.Type != MeasurementType.ReferenceLine)
                return "-";

            switch (m.Type)
            {
                case MeasurementType.Distance:
                case MeasurementType.PerpendicularLine: // ← Ajouter cette ligne
                    double pixels = CalculateDistance(m.Start, m.End);
                    double realUnits = pixels / pixelToRealRatio;
                    return $"{realUnits:F2} cm";

                case MeasurementType.ReferenceLine:
                    double refPixels = CalculateDistance(m.Start, m.End);
                    double refUnits = refPixels / pixelToRealRatio;
                    return $"{refUnits:F2} cm (Reference)";

                case MeasurementType.Angle:
                case MeasurementType.AngleWithAxis:
                    // Angles are the same in real world as in pixels
                    return GetPixelValueString(m);

                default:
                    return "-";
            }
        }

        private void CreatePerpendicularLine(Measurement baseLine, Point endPoint)
        {
            Point A = baseLine.Start;
            Point B = baseLine.End;
            Point C = endPoint;

            // Calculate the perpendicular projection of point C onto line AB
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0) return; // Avoid division by zero

            // Calculate projection parameter t
            double t = ((C.X - A.X) * dx + (C.Y - A.Y) * dy) / lengthSquared;

            // Clamp t to [0,1] to ensure the perpendicular foot is on the line segment
            t = Math.Max(0, Math.Min(1, t));

            // Calculate the perpendicular foot point
            Point perpendicularFoot = new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy)
            );

            // Create the perpendicular line measurement
            Measurement perpendicularLine = new Measurement(
                perpendicularFoot,
                C,
                $"P{measurementCounter++}",
                MeasurementType.PerpendicularLine,
                idCounter++
            );

            measurements.Add(perpendicularLine);
            UpdateStatus($"Perpendicular line created (ID: {perpendicularLine.ID})");
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

        // Dialog for renaming measurements
        private class RenameDialog : Form
        {
            private TextBox textBox;

            public string NewName { get; private set; }

            public RenameDialog(string currentName)
            {
                InitializeComponent(currentName);
            }

            private void InitializeComponent(string currentName)
            {
                this.Text = "Rename Measurement";
                this.Size = new Size(300, 150);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label label = new Label();
                label.Text = "Enter new name for measurement:";
                label.Location = new Point(20, 20);
                label.Size = new Size(250, 20);

                textBox = new TextBox();
                textBox.Text = currentName;
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
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    NewName = textBox.Text.Trim();
                }
                else
                {
                    MessageBox.Show("Please enter a valid name.");
                    this.DialogResult = DialogResult.None;
                }
            }
        }

        private void BodyPictureAnalyzer_Load(object sender, EventArgs e)
        {

        }
    }


     class CustomColorTable : ProfessionalColorTable
    {
        // Couleur de fond des menus
        public override Color ToolStripDropDownBackground
        {
            get { return Color.FromArgb(62, 62, 64); }
        }

        // Bordure des menus
        public override Color MenuBorder
        {
            get { return Color.FromArgb(100, 100, 100); }
        }

        // Fond des items au survol
        public override Color MenuItemSelected
        {
            get { return Color.FromArgb(87, 87, 90); }
        }

        // Dégradé pour les items sélectionnés (unie)
        public override Color MenuItemSelectedGradientBegin
        {
            get { return Color.FromArgb(87, 87, 90); }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return Color.FromArgb(87, 87, 90); }
        }

        // Dégradé pour les items pressés (unie)
        public override Color MenuItemPressedGradientBegin
        {
            get { return Color.FromArgb(75, 75, 78); }
        }

        public override Color MenuItemPressedGradientMiddle
        {
            get { return Color.FromArgb(75, 75, 78); }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get { return Color.FromArgb(75, 75, 78); }
        }

        // Image margin
        public override Color ImageMarginGradientBegin
        {
            get { return Color.FromArgb(55, 55, 58); }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return Color.FromArgb(55, 55, 58); }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return Color.FromArgb(55, 55, 58); }
        }
    }

    class CustomToolStripRenderer : ToolStripProfessionalRenderer
    {
        public CustomToolStripRenderer() : base(new CustomColorTable()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White; // Flèche blanche pour les menus overflow
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White; // Texte toujours blanc
            base.OnRenderItemText(e);
        }

        protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e)
        {
            // Style personnalisé pour le bouton overflow
            var rect = new System.Drawing.Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1);
            var buttonRect = new System.Drawing.Rectangle(rect.X - 5, rect.Y, rect.Width + 5, rect.Height);

            // Fond du bouton overflow
            using (var brush = new SolidBrush(Color.FromArgb(62, 62, 64)))
                e.Graphics.FillRectangle(brush, buttonRect);

            // Bordure
            using (var pen = new Pen(Color.FromArgb(100, 100, 100)))
                e.Graphics.DrawRectangle(pen, buttonRect);

            // Dessiner les flèches
            using (var pen = new Pen(Color.White, 2))
            {
                int arrowSize = 4;
                int centerX = buttonRect.Width / 2 - 3;
                int centerY = buttonRect.Height / 2;

                // Flèche droite
                e.Graphics.DrawLine(pen,
                    centerX, centerY - arrowSize,
                    centerX + arrowSize, centerY);
                e.Graphics.DrawLine(pen,
                    centerX + arrowSize, centerY,
                    centerX, centerY + arrowSize);

                // Flèche droite supplémentaire
                e.Graphics.DrawLine(pen,
                    centerX + 3, centerY - arrowSize,
                    centerX + 3 + arrowSize, centerY);
                e.Graphics.DrawLine(pen,
                    centerX + 3 + arrowSize, centerY,
                    centerX + 3, centerY + arrowSize);
            }
        }
    }
}