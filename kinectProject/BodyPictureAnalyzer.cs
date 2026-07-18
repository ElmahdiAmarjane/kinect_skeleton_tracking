using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;
using Image = System.Drawing.Image;
using Font = System.Drawing.Font;


namespace kinectProject
{
    public partial class BodyPictureAnalyzer : Form
    {
        #region Services

        private CalculationService calcService;
        private DetectionService detectionService;
        private ImageProcessingService imageService;
        private IntersectionService intersectionService;
        private MeasurementService measurementService;
        private PdfExportService pdfService;

        #endregion

        #region Application State

        // Detection state
        private DetectionMode currentDetectionMode = DetectionMode.None;
        private List<DetectedPoint> detectedPoints = new List<DetectedPoint>();
        private List<BodyLandmark> bodyLandmarks = new List<BodyLandmark>();
        private int detectionTolerance = 30;
        private bool showDetectionPreview = true;
        private Bitmap processedImage;
        private Dictionary<PointColor, Color> colorMap = new Dictionary<PointColor, Color>()
        {
            { PointColor.Red, Color.Red },
            { PointColor.Green, Color.Green },
            { PointColor.Blue, Color.Blue },
            { PointColor.Yellow, Color.Yellow },
            { PointColor.White, Color.White }
        };
        private PointColor selectedColor = PointColor.Red;
        private Color customColor = Color.Red;
        private int minPointSize = 5;
        private int maxPointSize = 30;

        // Tool and edit state
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

        // Zoom state
        private float zoomFactor = 1.0f;
        private PointF panOffset = PointF.Empty;
        private bool isPanning = false;
        private Point panStart;
        private Matrix transformMatrix = new Matrix();
        private Matrix inverseTransform = new Matrix();

        // Intersection state
        private List<IntersectionPoint> intersectionPoints = new List<IntersectionPoint>();
        private int intersectionCounter = 1;
        private IntersectionPoint? hoveredIntersection = null;
        private IntersectionPoint? selectedIntersection = null;
        private const int intersectionTolerance = 10;

        // Line creation state
        private bool autoRenameEnabled = true;
        private Point? selectedPointForLine = null;
        private bool isCreatingLineBetweenPoints = false;
        private Point? highlightedPoint = null;

        // Color picking state
        private bool isPickingReferenceColor = false;
        private Color? referenceColor = null;
        private Point? pickedPointLocation = null;

        #endregion

        #region UI Controls

        protected DoubleBufferedPanel drawingPanel;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ListView measurementsList;

        #endregion

        #region Constructor

        public BodyPictureAnalyzer()
        {
            InitializeServices();
            InitializeComponents();
            this.DoubleBuffered = true;
            SetupUI();
            UpdateStatus("Ready to import an image");
        }

        private void InitializeServices()
        {
            calcService = new CalculationService();
            detectionService = new DetectionService();
            imageService = new ImageProcessingService();
            intersectionService = new IntersectionService();
            measurementService = new MeasurementService();
            pdfService = new PdfExportService();
        }

        #endregion

        #region Form Initialization

        private void InitializeComponents()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1200, 800);
            this.Name = "BodyPictureAnalyzer";
            this.Text = "Advanced Image Measurement Tool with Zoom";
            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            this.Text = "Advanced Image Measurement Tool with Zoom";
            this.Size = new Size(1200, 800);
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            SetupToolStrip();
            SetupDrawingPanel();
            SetupMeasurementsList();
            SetupStatusStrip();

            this.Controls.Add(drawingPanel);
            this.Controls.Add(measurementsList);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);

            gridOrigin = new Point(drawingPanel.Width / 2, drawingPanel.Height / 2);
            UpdateTransformationMatrices();
        }

        private void SetupToolStrip()
        {
            toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new CustomToolStripRenderer()
            };

            // File operations
            AddToolButton("📁 Import Image", BtnImport_Click);
            AddToolSeparator();

            // Tool modes
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

            // Edit modes
            AddToolButton("✏️ Move Mode", (s, e) => SetEditMode(EditMode.Move));
            AddToolButton("🗑️ Delete Mode", (s, e) => SetEditMode(EditMode.Delete));
            AddToolButton("🏷️ Rename Mode", (s, e) => SetEditMode(EditMode.Rename));
            AddToolButton("🧹 Clear All", BtnClear_Click);
            AddToolButton("🔲 Toggle Grid", BtnToggleGrid_Click);
            AddToolButton("📄 Export PDF", (s, e) => pdfService.ExportToPdf(originalImage, measurements, intersectionPoints, isReferenceSet, pixelToRealRatio));

            // Detection
            AddToolButton("🔴 Simple Test", (s, e) => SimpleDetectionTest());
            AddToolSeparator();

            // Zoom controls
            AddToolButton("🔍 Zoom In", BtnZoomIn_Click);
            AddToolButton("🔍 Zoom Out", BtnZoomOut_Click);
            AddToolButton("🔍 Zoom Fit", BtnZoomFit_Click);
            AddToolButton("🔍 Zoom 100%", BtnZoomReset_Click);
            AddToolButton("✋ Pan", BtnPan_Click);

            // Auto-rename
            AddToolButton("🏷️ Auto-Rename", BtnToggleAutoRename_Click);
            AddToolSeparator();

            // Point operations
            AddToolButton("🎯 Detect Points", BtnDetectPoints_Click);
            AddToolButton("📏 Connect Points", BtnConnectPoints_Click);
        }

        private void SetupDrawingPanel()
        {
            drawingPanel = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(37, 37, 38),
                BorderStyle = BorderStyle.FixedSingle
            };

            drawingPanel.Paint += DrawingPanel_Paint;
            drawingPanel.MouseClick += DrawingPanel_MouseClick;
            drawingPanel.MouseDown += DrawingPanel_MouseDown;
            drawingPanel.MouseMove += DrawingPanel_MouseMove;
            drawingPanel.MouseUp += DrawingPanel_MouseUp;
            drawingPanel.MouseWheel += DrawingPanel_MouseWheel;
            drawingPanel.MouseLeave += DrawingPanel_MouseLeave;
            drawingPanel.Resize += DrawingPanel_Resize;
        }

        private void SetupMeasurementsList()
        {
            measurementsList = new ListView
            {
                Dock = DockStyle.Right,
                Width = 350,
                BackColor = Color.FromArgb(37, 37, 38),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };

            measurementsList.Columns.Add("ID", 50);
            measurementsList.Columns.Add("Type", 80);
            measurementsList.Columns.Add("Name", 80);
            measurementsList.Columns.Add("Value", 120);
            measurementsList.SelectedIndexChanged += MeasurementsList_SelectedIndexChanged;
        }

        private void SetupStatusStrip()
        {
            statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White
            };
        }

        #endregion

        #region ToolStrip Helpers

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
            toolStrip.Items.Add(new ToolStripSeparator());
        }

        #endregion

        #region Tool and Edit Mode Management

        private void SetToolMode(ToolMode mode)
        {
            currentTool = mode;
            currentEditMode = EditMode.None;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;
            selectedLineForPerpendicular = null;
            isSelectingBaseLine = false;
            isPanning = false;

            string statusText = "";
            switch (mode)
            {
                case ToolMode.Line:
                    statusText = "Line Tool: Click to place start and end points";
                    break;
                case ToolMode.Point:
                    statusText = "Point Tool: Click to place a point";
                    break;
                case ToolMode.Angle:
                    statusText = "Angle Tool: Click to place vertex, then two end points";
                    break;
                case ToolMode.AngleWithAxis:
                    statusText = "Angle with Axis: Draw a line, then select axis";
                    break;
                case ToolMode.Distance:
                    statusText = "Distance Tool: Click to measure distance";
                    break;
                case ToolMode.Reference:
                    statusText = "Reference Tool: Draw a line of known length";
                    break;
                case ToolMode.Perpendicular:
                    statusText = "Perpendicular Tool: Select a line first, then click to place perpendicular line";
                    break;
            }

            UpdateStatus(statusText);
            drawingPanel.Cursor = Cursors.Cross;
            DeselectAllMeasurements();
        }

        private void SetEditMode(EditMode mode)
        {
            currentEditMode = mode;
            currentTool = ToolMode.None;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;
            selectedLineForPerpendicular = null;
            isSelectingBaseLine = false;
            isPanning = false;

            string statusText = "";
            Cursor cursor = Cursors.Default;

            switch (mode)
            {
                case EditMode.Normal:
                    statusText = "Normal Mode: Hover over measurements to see details";
                    cursor = Cursors.Default;
                    break;
                case EditMode.Delete:
                    statusText = "Delete Mode: Click on measurement to delete";
                    cursor = Cursors.No;
                    break;
                case EditMode.Move:
                    statusText = "Move Mode: Click and drag to move measurement";
                    cursor = Cursors.Hand;
                    break;
                case EditMode.Rename:
                    statusText = "Rename Mode: Click on measurement to rename";
                    cursor = Cursors.UpArrow;
                    break;
            }

            UpdateStatus(statusText);
            drawingPanel.Cursor = cursor;
            DeselectAllMeasurements();
        }

        private void UpdateStatus(string message)
        {
            if (statusStrip.Items.Count == 0)
                statusStrip.Items.Add(new ToolStripStatusLabel());

            string zoomInfo = $" | Zoom: {zoomFactor * 100:F0}%";
            statusStrip.Items[0].Text = message + zoomInfo;
        }

        #endregion

        #region Button Click Handlers

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
                        zoomFactor = 1.0f;
                        panOffset = PointF.Empty;
                        UpdateTransformationMatrices();

                        measurements.Clear();
                        detectedPoints.Clear();
                        intersectionPoints.Clear();
                        measurementsList.Items.Clear();
                        measurementCounter = 1;
                        idCounter = 1;
                        isReferenceSet = false;
                        pixelToRealRatio = 1.0f;
                        isSettingReference = false;

                        UpdateStatus("Image loaded. Select a measurement tool.");
                        drawingPanel.Invalidate();
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
            measurementService.ClearAll(measurements, detectedPoints, ref measurementCounter, ref idCounter);
            intersectionPoints.Clear();
            intersectionCounter = 1;
            selectedIntersection = null;
            hoveredIntersection = null;
            currentStartPoint = null;
            angleVertex = null;
            angleFirstPoint = null;
            isReferenceSet = false;
            pixelToRealRatio = 1.0f;
            isSettingReference = false;
            measurementsList.Items.Clear();

            UpdateStatus("All measurements and points cleared.");
            drawingPanel.Invalidate();
        }

        private void BtnToggleGrid_Click(object sender, EventArgs e)
        {
            showGrid = !showGrid;
            drawingPanel.Invalidate();
        }

        private void BtnToggleAutoRename_Click(object sender, EventArgs e)
        {
            autoRenameEnabled = !autoRenameEnabled;

            var button = sender as ToolStripButton;
            if (button != null)
            {
                button.Text = autoRenameEnabled ? "🏷️ Auto-Rename: ON" : "🏷️ Auto-Rename: OFF";
            }

            UpdateStatus($"Auto-rename: {(autoRenameEnabled ? "Enabled" : "Disabled")}");
        }

        private void BtnDetectPoints_Click(object sender, EventArgs e)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Please load an image first.", "No Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var detectionDialog = new DetectionSettingsDialog(
                selectedColor, customColor, detectionTolerance, minPointSize, maxPointSize))
            {
                if (detectionDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = detectionDialog.SelectedColor;
                    customColor = detectionDialog.CustomColor;
                    detectionTolerance = detectionDialog.Tolerance;
                    minPointSize = detectionDialog.MinSize;
                    maxPointSize = detectionDialog.MaxSize;

                    // Use automatic detection
                    detectionService.DetectColoredPointsFlexible(
                        null, new Bitmap(originalImage), selectedColor,
                        detectionTolerance, minPointSize, maxPointSize, customColor,
                        out detectedPoints);

                    if (detectedPoints.Count > 0)
                    {
                        bool accepted = detectionService.ShowDetectionConfirmation(detectedPoints, new Bitmap(originalImage));

                        if (accepted)
                        {
                            measurementService.CreateMeasurementsFromDetectedPoints(
                                detectedPoints, measurements, ref idCounter,
                                autoRenameEnabled, ref autoRenameEnabled);
                            UpdateMeasurementsList();
                            drawingPanel.Invalidate();
                        }
                        else
                        {
                            detectedPoints.Clear();
                        }
                    }
                }
            }
        }

        private void BtnConnectPoints_Click(object sender, EventArgs e)
        {
            if (detectedPoints.Count == 0)
            {
                MessageBox.Show("No points detected. Use point detection first.",
                               "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isCreatingLineBetweenPoints = true;
            selectedPointForLine = null;
            UpdateStatus("Connection Mode: Click first point, then second point");
            drawingPanel.Cursor = Cursors.Hand;
            drawingPanel.Invalidate();
        }

        private void SimpleDetectionTest()
        {
            if (originalImage == null) return;

            detectionService.SimpleDetectionTest(new Bitmap(originalImage), out detectedPoints);

            MessageBox.Show($"Simple detection found {detectedPoints.Count} red pixels");

            if (detectedPoints.Count > 0)
            {
                measurementService.CreateMeasurementsFromDetectedPoints(
                    detectedPoints, measurements, ref idCounter,
                    autoRenameEnabled, ref autoRenameEnabled);
                UpdateMeasurementsList();
                drawingPanel.Invalidate();
            }
        }

        #endregion

        #region Zoom and Pan Methods

        private void BtnZoomIn_Click(object sender, EventArgs e) => ZoomAtCenter(1.25f);
        private void BtnZoomOut_Click(object sender, EventArgs e) => ZoomAtCenter(0.8f);

        private void BtnZoomReset_Click(object sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            panOffset = PointF.Empty;
            UpdateTransformationMatrices();
            drawingPanel.Invalidate();
            UpdateStatus("Zoom reset to 100%");
        }

        private void BtnZoomFit_Click(object sender, EventArgs e)
        {
            if (originalImage == null) return;

            zoomFactor = calcService.CalculateFitZoom(originalImage.Size, drawingPanel.Size);
            panOffset = PointF.Empty;
            UpdateTransformationMatrices();
            drawingPanel.Invalidate();
            UpdateStatus($"Zoom fit: {zoomFactor * 100:F0}%");
        }

        private void BtnPan_Click(object sender, EventArgs e)
        {
            isPanning = !isPanning;
            drawingPanel.Cursor = isPanning ? Cursors.SizeAll : Cursors.Default;
            UpdateStatus(isPanning ? "Pan mode: Click and drag to move the view" : "Pan mode disabled");
        }

        private void DrawingPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (originalImage == null) return;
            float zoom = e.Delta > 0 ? 1.25f : 0.8f;
            ZoomAtPoint(e.Location, zoom);
        }

        private void ZoomAtCenter(float zoom)
        {
            ZoomAtPoint(new PointF(drawingPanel.Width / 2, drawingPanel.Height / 2), zoom);
        }

        private void ZoomAtPoint(PointF point, float zoom)
        {
            float oldZoom = zoomFactor;
            zoomFactor = calcService.ClampZoom(zoomFactor * zoom);

            if (oldZoom != zoomFactor)
            {
                PointF imagePointBefore = calcService.TransformPointToImage(point, inverseTransform);
                UpdateTransformationMatrices();
                PointF imagePointAfter = calcService.TransformPointToImage(point, inverseTransform);

                panOffset.X += (imagePointAfter.X - imagePointBefore.X) * zoomFactor;
                panOffset.Y += (imagePointAfter.Y - imagePointBefore.Y) * zoomFactor;

                UpdateTransformationMatrices();
                drawingPanel.Invalidate();
                UpdateStatus($"Zoom: {zoomFactor * 100:F0}%");
            }
        }

        private void UpdateTransformationMatrices()
        {
            transformMatrix = new Matrix();
            transformMatrix.Translate(panOffset.X, panOffset.Y);
            transformMatrix.Scale(zoomFactor, zoomFactor);

            inverseTransform = transformMatrix.Clone();
            inverseTransform.Invert();
        }

        #endregion

        #region Mouse Event Handlers

        private void DrawingPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (originalImage == null) return;

            if (e.Button == MouseButtons.Left && isPanning)
            {
                panStart = e.Location;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                PointF imagePointF = calcService.TransformPointToImage(e.Location, inverseTransform);
                Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

                // Check if clicking near grid origin for dragging
                PointF screenGridOrigin = calcService.TransformPointToScreen(gridOrigin, transformMatrix);
                if (calcService.IsNearPoint(e.Location, new Point((int)screenGridOrigin.X, (int)screenGridOrigin.Y), gridGrabRadius))
                {
                    isDraggingGrid = true;
                    drawingPanel.Cursor = Cursors.SizeAll;
                    return;
                }

                // Handle measurement selection for moving
                if (currentEditMode == EditMode.Move)
                {
                    int index = measurementService.FindMeasurementAtPoint(imagePoint, measurements);
                    if (index >= 0)
                    {
                        measurementService.SelectMeasurement(index, measurements, measurementsList,
                            ref selectedMeasurement, ref selectedMeasurementIndex);

                        Measurement m = measurements[index];
                        if (m.Type == MeasurementType.Point)
                        {
                            dragOffset = new Point(imagePoint.X - m.Start.X, imagePoint.Y - m.Start.Y);
                        }
                        else
                        {
                            double distanceToStart = calcService.CalculateDistance(imagePoint, m.Start);
                            double distanceToEnd = calcService.CalculateDistance(imagePoint, m.End);

                            if (distanceToStart < distanceToEnd)
                                dragOffset = new Point(imagePoint.X - m.Start.X, imagePoint.Y - m.Start.Y);
                            else
                                dragOffset = new Point(imagePoint.X - m.End.X, imagePoint.Y - m.End.Y);
                        }

                        isDraggingMeasurement = true;
                        drawingPanel.Cursor = Cursors.SizeAll;
                        drawingPanel.Invalidate();
                    }
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
                isPanning = true;
                panStart = e.Location;
                drawingPanel.Cursor = Cursors.SizeAll;
            }
        }

        private void DrawingPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (originalImage == null) return;

            // Handle panning
            if (isPanning && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle))
            {
                int deltaX = e.X - panStart.X;
                int deltaY = e.Y - panStart.Y;
                panOffset.X += deltaX;
                panOffset.Y += deltaY;
                panStart = e.Location;
                UpdateTransformationMatrices();
                drawingPanel.Invalidate();
                return;
            }

            // Handle grid dragging
            if (isDraggingGrid)
            {
                PointF newGridOrigin = calcService.TransformPointToImage(e.Location, inverseTransform);
                gridOrigin = new Point((int)newGridOrigin.X, (int)newGridOrigin.Y);
                drawingPanel.Invalidate();
                return;
            }

            PointF imagePointF = calcService.TransformPointToImage(e.Location, inverseTransform);
            Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

            if (isDraggingMeasurement && selectedMeasurement.HasValue && selectedMeasurementIndex >= 0)
            {
                measurementService.MoveMeasurement(selectedMeasurementIndex, imagePoint, dragOffset, measurements);
                drawingPanel.Invalidate();
            }
            else
            {
                UpdateHoverInfo(imagePoint);

                PointF screenGridOrigin = calcService.TransformPointToScreen(gridOrigin, transformMatrix);
                if (calcService.IsNearPoint(e.Location, new Point((int)screenGridOrigin.X, (int)screenGridOrigin.Y), gridGrabRadius))
                {
                    drawingPanel.Cursor = Cursors.SizeAll;
                }
                else if (currentTool != ToolMode.None)
                {
                    drawingPanel.Cursor = Cursors.Cross;
                }
                else if (currentEditMode == EditMode.Move)
                {
                    drawingPanel.Cursor = Cursors.Hand;
                }
                else
                {
                    drawingPanel.Cursor = Cursors.Default;
                }

                drawingPanel.Invalidate();
            }
        }

        private void DrawingPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingMeasurement)
            {
                isDraggingMeasurement = false;
                drawingPanel.Cursor = Cursors.Hand;
                UpdateMeasurementsList();
            }

            if (isDraggingGrid)
            {
                isDraggingGrid = false;
                drawingPanel.Cursor = Cursors.Default;
            }

            if (e.Button == MouseButtons.Middle)
            {
                isPanning = false;
                drawingPanel.Cursor = Cursors.Default;
            }
        }

        private void DrawingPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (originalImage == null) return;

            PointF imagePointF = calcService.TransformPointToImage(e.Location, inverseTransform);
            Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

            // 1. LINE CREATION MODE (PRIORITY)
            if (isCreatingLineBetweenPoints && e.Button == MouseButtons.Left)
            {
                HandlePointConnection(imagePoint);
                return;
            }

            // 2. RIGHT CLICK: Intersections + Points
            if (e.Button == MouseButtons.Right)
            {
                var intersection = intersectionService.FindIntersectionAtPoint(
                    imagePoint, intersectionPoints, intersectionTolerance);

                if (intersection.HasValue)
                {
                    selectedIntersection = intersection;
                    intersectionService.ShowAngleContextMenu(e.Location, intersection.Value, drawingPanel);
                    return;
                }

                int index = measurementService.FindMeasurementAtPoint(imagePoint, measurements);
                if (index >= 0)
                {
                    Measurement m = measurements[index];
                    if (m.Type == MeasurementType.Point)
                    {
                        measurementService.ShowPointContextMenu(e.Location, m, measurements, detectedPoints);
                        return;
                    }
                }
                return;
            }

            if (isDraggingGrid) return;

            // 3. COLOR PICKING MODE
            if (isPickingReferenceColor && e.Button == MouseButtons.Left)
            {
                using (Bitmap bmp = new Bitmap(originalImage))
                {
                    Color pickedColor = imageService.PickColorFromImage(bmp, imagePoint);
                    referenceColor = pickedColor;
                    pickedPointLocation = imagePoint;

                    detectionService.ShowColorPreviewAndDetect(
                        pickedColor, imagePoint, new Bitmap(originalImage),
                        detectionTolerance, selectedColor, customColor,
                        out detectedPoints);
                }

                isPickingReferenceColor = false;
                drawingPanel.Cursor = Cursors.Default;
                return;
            }

            // 4. MANUAL POINT ADDITION
            if (currentTool == ToolMode.Point && e.Button == MouseButtons.Left)
            {
                detectionService.HandleManualPointDetection(
                    imagePoint, selectedColor, customColor,
                    autoRenameEnabled, detectedPoints, measurements, ref idCounter);
                UpdateMeasurementsList();
                drawingPanel.Invalidate();
                return;
            }

            // 5. MEASUREMENT CREATION
            if (currentTool != ToolMode.None && e.Button == MouseButtons.Left)
            {
                HandleMeasurementCreation(imagePoint);
                return;
            }

            // 6. EDIT MODE
            if (currentEditMode != EditMode.None && currentEditMode != EditMode.Normal && e.Button == MouseButtons.Left)
            {
                HandleSelection(imagePoint);
            }
        }

        private void DrawingPanel_MouseLeave(object sender, EventArgs e)
        {
            hoverPoint = null;
            hoverMeasurement = null;
            hoverMeasurementName = "";
            drawingPanel.Invalidate();
        }

        private void DrawingPanel_Resize(object sender, EventArgs e)
        {
            drawingPanel.Invalidate();
        }

        #endregion

        #region Measurement Creation and Handling

        private void HandleMeasurementCreation(Point location)
        {
            string measurementName = "";
            Measurement newMeasurement;
            int newId = idCounter++;

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
                        measurementName = $"L{measurementCounter++}";
                        newMeasurement = measurementService.CreateLineMeasurement(
                            currentStartPoint.Value, location, measurementName, newId);

                        measurementName = measurementService.PromptForRename(
                            measurementName, autoRenameEnabled, ref autoRenameEnabled);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);
                        intersectionService.FindAllIntersections(
                            measurements, intersectionPoints, ref intersectionCounter, intersectionTolerance);

                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Line created: {measurementName}");
                    }
                    break;

                case ToolMode.Point:
                    measurementName = $"P{measurementCounter++}";
                    newMeasurement = measurementService.CreatePointMeasurement(location, measurementName, newId);

                    measurementName = measurementService.PromptForRename(
                        measurementName, autoRenameEnabled, ref autoRenameEnabled);
                    newMeasurement.Name = measurementName;

                    measurements.Add(newMeasurement);
                    UpdateMeasurementsList();
                    drawingPanel.Invalidate();
                    UpdateStatus($"Point created: {measurementName}");
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
                        measurementName = $"A{measurementCounter}";
                        measurementName = measurementService.PromptForRename(
                            measurementName, autoRenameEnabled, ref autoRenameEnabled);

                        Measurement firstSegment = measurementService.CreateAngleMeasurement(
                            angleVertex.Value, angleFirstPoint.Value, measurementName, newId);
                        measurements.Add(firstSegment);

                        Measurement secondSegment = measurementService.CreateAngleMeasurement(
                            angleVertex.Value, location, measurementName, newId);
                        measurements.Add(secondSegment);

                        measurementCounter++;
                        angleVertex = null;
                        angleFirstPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Angle created: {measurementName}");
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
                        measurementName = $"AA{measurementCounter++}";
                        newMeasurement = measurementService.CreateAngleWithAxisMeasurement(
                            currentStartPoint.Value, location, measurementName, newId, null);

                        measurementName = measurementService.PromptForRename(
                            measurementName, autoRenameEnabled, ref autoRenameEnabled);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);

                        var axisDialog = new AxisSelectionDialog();
                        if (axisDialog.ShowDialog() == DialogResult.OK)
                        {
                            Measurement m = measurements[measurements.Count - 1];
                            m.Axis = axisDialog.SelectedAxis;
                            measurements[measurements.Count - 1] = m;
                        }

                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Angle with axis created: {measurementName}");
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
                        measurementName = $"D{measurementCounter++}";
                        newMeasurement = measurementService.CreateDistanceMeasurement(
                            currentStartPoint.Value, location, measurementName, newId);

                        measurementName = measurementService.PromptForRename(
                            measurementName, autoRenameEnabled, ref autoRenameEnabled);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);
                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Distance measurement created: {measurementName}");
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
                        measurementName = $"R{measurementCounter++}";
                        newMeasurement = measurementService.CreateDistanceMeasurement(
                            currentStartPoint.Value, location, measurementName, newId);

                        measurementName = measurementService.PromptForRename(
                            measurementName, autoRenameEnabled, ref autoRenameEnabled);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);
                        currentStartPoint = null;
                        isSettingReference = true;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();

                        using (var inputDialog = new ReferenceInputDialog())
                        {
                            if (inputDialog.ShowDialog() == DialogResult.OK)
                            {
                                measurementService.SetScaleFromReference(
                                    measurements[measurements.Count - 1],
                                    inputDialog.ReferenceLength,
                                    ref pixelToRealRatio,
                                    ref isReferenceSet,
                                    measurements);
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
                        int lineIndex = measurementService.FindMeasurementAtPoint(location, measurements);
                        if (lineIndex >= 0 &&
                            (measurements[lineIndex].Type == MeasurementType.Line ||
                             measurements[lineIndex].Type == MeasurementType.Distance ||
                             measurements[lineIndex].Type == MeasurementType.ReferenceLine ||
                             measurements[lineIndex].Type == MeasurementType.Angle))
                        {
                            selectedLineForPerpendicular = measurements[lineIndex];
                            isSelectingBaseLine = true;
                            UpdateStatus("Base line selected. Now click to place perpendicular line endpoint");

                            measurementService.SelectMeasurement(lineIndex, measurements, measurementsList,
                                ref selectedMeasurement, ref selectedMeasurementIndex);
                            drawingPanel.Invalidate();
                        }
                        else
                        {
                            UpdateStatus("Please select a valid line first");
                        }
                    }
                    else
                    {
                        if (selectedLineForPerpendicular.HasValue)
                        {
                            measurementName = $"P{measurementCounter++}";

                            Point foot;
                            Measurement perpLine = measurementService.CreatePerpendicularLine(
                                selectedLineForPerpendicular.Value, location, newId, measurementName, out foot);

                            if (perpLine.Type != MeasurementType.None)
                            {
                                measurementName = measurementService.PromptForRename(
                                    measurementName, autoRenameEnabled, ref autoRenameEnabled);
                                perpLine.Name = measurementName;

                                measurements.Add(perpLine);
                            }

                            isSelectingBaseLine = false;
                            selectedLineForPerpendicular = null;
                            measurementService.DeselectAllMeasurements(measurements, measurementsList,
                                ref selectedMeasurement, ref selectedMeasurementIndex);
                            UpdateMeasurementsList();
                            drawingPanel.Invalidate();
                        }
                    }
                    break;
            }
        }

        private void HandleSelection(Point location)
        {
            int index = measurementService.FindMeasurementAtPoint(location, measurements);

            if (index >= 0)
            {
                if (currentEditMode == EditMode.Delete)
                {
                    measurementService.DeleteMeasurement(index, measurements, detectedPoints);
                    intersectionService.FindAllIntersections(
                        measurements, intersectionPoints, ref intersectionCounter, intersectionTolerance);
                    UpdateMeasurementsList();
                    drawingPanel.Invalidate();
                    UpdateStatus("Measurement deleted");
                }
                else if (currentEditMode == EditMode.Rename)
                {
                    string currentName = measurements[index].Name;
                    string newName = measurementService.PromptForRename(
                        currentName, autoRenameEnabled, ref autoRenameEnabled);

                    if (newName != currentName)
                    {
                        measurementService.RenameMeasurement(index, newName, measurements);
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Measurement renamed to {newName}");
                    }
                }
            }
            else
            {
                measurementService.DeselectAllMeasurements(measurements, measurementsList,
                    ref selectedMeasurement, ref selectedMeasurementIndex);
                drawingPanel.Invalidate();
            }
        }

        private void HandlePointConnection(Point clickPoint)
        {
            DetectedPoint? nearestDetectedPoint = null;
            double minDistance = double.MaxValue;

            foreach (var point in detectedPoints)
            {
                double distance = calcService.CalculateDistance(clickPoint, point.Location);
                if (distance < 20 && distance < minDistance)
                {
                    minDistance = distance;
                    nearestDetectedPoint = point;
                }
            }

            if (nearestDetectedPoint == null)
            {
                foreach (var measurement in measurements)
                {
                    if (measurement.Type == MeasurementType.Point)
                    {
                        double distance = calcService.CalculateDistance(clickPoint, measurement.Start);
                        if (distance < 20 && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestDetectedPoint = new DetectedPoint(
                                measurement.Start, PointColor.Red, 1.0, 10, measurement.ID);
                        }
                    }
                }
            }

            if (nearestDetectedPoint == null)
            {
                UpdateStatus("No point found near click. Click on a detected point.");
                return;
            }

            HighlightSelectedPoint(nearestDetectedPoint.Value);

            if (selectedPointForLine == null)
            {
                selectedPointForLine = nearestDetectedPoint.Value.Location;
                UpdateStatus($"First point selected (P{nearestDetectedPoint.Value.ID}). Click second point.");
            }
            else
            {
                measurementService.CreateLineBetweenPoints(
                    selectedPointForLine.Value, nearestDetectedPoint.Value,
                    measurements, detectedPoints, ref idCounter, ref measurementCounter);

                intersectionService.FindAllIntersections(
                    measurements, intersectionPoints, ref intersectionCounter, intersectionTolerance);

                selectedPointForLine = null;
                UpdateMeasurementsList();

                var result = MessageBox.Show("Line created! Create another line?",
                    "Connection Successful", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    isCreatingLineBetweenPoints = false;
                    drawingPanel.Cursor = Cursors.Default;
                    UpdateStatus("Connection mode ended.");
                }
                else
                {
                    UpdateStatus("Connection Mode: Click first point, then second point");
                }
            }

            drawingPanel.Invalidate();
        }

        private void HighlightSelectedPoint(DetectedPoint point)
        {
            highlightedPoint = point.Location;

            Timer highlightTimer = new Timer();
            highlightTimer.Interval = 1000;
            highlightTimer.Tick += (s, e) =>
            {
                highlightedPoint = null;
                highlightTimer.Stop();
                drawingPanel.Invalidate();
            };
            highlightTimer.Start();
        }

        private void DeselectAllMeasurements()
        {
            measurementService.DeselectAllMeasurements(measurements, measurementsList,
                ref selectedMeasurement, ref selectedMeasurementIndex);
        }

        #endregion

        #region Hover Info

        private void UpdateHoverInfo(Point imagePoint)
        {
            var intersection = intersectionService.FindIntersectionAtPoint(
                imagePoint, intersectionPoints, intersectionTolerance);

            if (intersection.HasValue)
            {
                hoveredIntersection = intersection;
                hoverPoint = intersection.Value.Location;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Point P{intersection.Value.ID} ({intersection.Value.Type})");
                sb.AppendLine($"Lines: {string.Join(", ", intersection.Value.LineIDs.Select(id => $"L{id}"))}");

                if (intersection.Value.Angles.Count > 0)
                {
                    sb.AppendLine("Angles:");
                    foreach (var angle in intersection.Value.Angles)
                    {
                        sb.AppendLine($"  L{angle.Item1}-L{angle.Item2}: {angle.Item3:F1}°");
                    }
                }

                hoverMeasurementName = sb.ToString();
                hoverMeasurement = null;
                return;
            }

            hoveredIntersection = null;
            int index = measurementService.FindMeasurementAtPoint(imagePoint, measurements);

            if (index >= 0)
            {
                hoverMeasurement = measurements[index];
                hoverPoint = measurementService.GetHoverPointForMeasurement(hoverMeasurement.Value, imagePoint);
                hoverMeasurementName = measurementService.GetHoverTextForMeasurement(
                    hoverMeasurement.Value, isReferenceSet, pixelToRealRatio, measurements); // ✅ Pass full list
            }
            else
            {
                hoverPoint = null;
                hoverMeasurementName = "";
                hoverMeasurement = null;
            }
        }

        #endregion

        #region ListView Management

        private void MeasurementsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            measurementService.DeselectAllMeasurements(measurements, measurementsList,
                ref selectedMeasurement, ref selectedMeasurementIndex);

            if (measurementsList.SelectedItems.Count > 0)
            {
                int selectedId = int.Parse(measurementsList.SelectedItems[0].Text);
                int index = measurements.FindIndex(m => m.ID == selectedId);

                if (index >= 0)
                {
                    measurementService.SelectMeasurement(index, measurements, measurementsList,
                        ref selectedMeasurement, ref selectedMeasurementIndex);
                }
            }

            drawingPanel.Invalidate();
        }

        private void UpdateMeasurementsList()
        {
            measurementService.UpdateMeasurementsList(
                measurementsList, measurements, isReferenceSet, pixelToRealRatio);
        }

        #endregion

        #region Drawing Methods

        private void DrawingPanel_Paint(object sender, PaintEventArgs e)
        {
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.Clear(drawingPanel.BackColor);

                if (originalImage == null)
                {
                    DrawPlaceholder(e.Graphics);
                    return;
                }

                e.Graphics.Transform = transformMatrix;
                e.Graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                if (showGrid) DrawGrid(e.Graphics);
                DrawAllMeasurements(e.Graphics);
                DrawDetectedPoints(e.Graphics);

                intersectionService.DrawIntersectionPoints(
                    e.Graphics, intersectionPoints, hoveredIntersection,
                    selectedIntersection, zoomFactor, measurements);

                if (currentTool != ToolMode.None) DrawCurrentToolPreview(e.Graphics);

                e.Graphics.ResetTransform();

                if (hoverPoint.HasValue && !string.IsNullOrEmpty(hoverMeasurementName))
                {
                    PointF screenHoverPoint = calcService.TransformPointToScreen(hoverPoint.Value, transformMatrix);
                    DrawHoverLabel(e.Graphics, new Point((int)screenHoverPoint.X, (int)screenHoverPoint.Y),
                                 hoverMeasurementName);
                }

                DrawZoomLevel(e.Graphics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drawing error: {ex.Message}");
            }
        }

        private void DrawPlaceholder(Graphics g)
        {
            string text = "Import an image to begin measurements";
            using (Font font = new Font("Arial", 14))
            using (Brush brush = new SolidBrush(Color.Gray))
            {
                SizeF textSize = g.MeasureString(text, font);
                g.DrawString(text, font, brush,
                    (drawingPanel.Width - textSize.Width) / 2,
                    (drawingPanel.Height - textSize.Height) / 2);
            }
        }

        private void DrawAllMeasurements(Graphics g)
        {
            foreach (var m in measurements)
            {
                DrawMeasurement(g, m);
            }
        }

        private void DrawMeasurement(Graphics g, Measurement m)
        {
            Color color = m.IsSelected ? Color.Yellow : measurementService.GetMeasurementColor(m.Type);
            int lineWidth = Math.Max(1, (int)((m.IsSelected ? 3 : 2) / zoomFactor));
            int pointSize = Math.Max(3, (int)((m.IsSelected ? 8 : 6) / zoomFactor));

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
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
                        break;

                    case MeasurementType.Angle:
                        if (m.Vertex.HasValue)
                        {
                            if (m.AngleValue.HasValue)
                            {
                                g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);

                                using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 1))
                                {
                                    float arcRadius = 15f / zoomFactor;
                                    g.DrawArc(arcPen,
                                        m.Vertex.Value.X - arcRadius, m.Vertex.Value.Y - arcRadius,
                                        arcRadius * 2, arcRadius * 2, 0, 120);
                                }
                            }
                            else
                            {
                                g.DrawLine(pen, m.Vertex.Value, m.End);
                                g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);
                                g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                                Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                    meas.Type == MeasurementType.Angle &&
                                    meas.Vertex.HasValue &&
                                    meas.Vertex.Value == m.Vertex.Value &&
                                    meas.ID == m.ID &&
                                    meas.End != m.End);

                                if (otherSegment.Type == MeasurementType.Angle)
                                {
                                    DrawAngleArc(g, m, otherSegment);
                                }
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);
                        DrawAxisAngleArc(g, m);
                        break;

                    case MeasurementType.PerpendicularLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        using (Pen perpendicularPen = new Pen(Color.White, 1))
                        {
                            int symbolSize = Math.Max(2, (int)(4 / zoomFactor));
                            g.DrawRectangle(perpendicularPen,
                                m.Start.X - symbolSize, m.Start.Y - symbolSize,
                                symbolSize * 2, symbolSize * 2);
                        }
                        break;
                }
            }
        }

        private void DrawDetectedPoints(Graphics g)
        {
            if (detectedPoints == null || detectedPoints.Count == 0) return;

            foreach (var point in detectedPoints)
            {
                Color pointColor = colorMap.ContainsKey(point.Color) ? colorMap[point.Color] : Color.Red;
                int pointSize = Math.Max(3, (int)(point.Radius / zoomFactor));

                bool isHighlighted = highlightedPoint.HasValue && point.Location == highlightedPoint.Value;

                if (isHighlighted)
                {
                    using (Pen highlightPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawEllipse(highlightPen,
                            point.Location.X - pointSize - 5, point.Location.Y - pointSize - 5,
                            (pointSize + 5) * 2, (pointSize + 5) * 2);
                    }
                }

                using (Brush brush = new SolidBrush(pointColor))
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    g.FillEllipse(brush, point.Location.X - pointSize / 2, point.Location.Y - pointSize / 2, pointSize, pointSize);
                    g.DrawEllipse(pen, point.Location.X - pointSize / 2, point.Location.Y - pointSize / 2, pointSize, pointSize);

                    using (Font font = new Font("Arial", Math.Max(8, 10 / zoomFactor), FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    {
                        string idText = $"P{point.ID}";
                        SizeF textSize = g.MeasureString(idText, font);
                        RectangleF textRect = new RectangleF(
                            point.Location.X - textSize.Width / 2,
                            point.Location.Y + pointSize + 2,
                            textSize.Width + 4, textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(idText, font, textBrush,
                            point.Location.X - textSize.Width / 2 + 2,
                            point.Location.Y + pointSize + 4);
                    }
                }
            }

            DrawBodyLandmarks(g);
        }

        private void DrawBodyLandmarks(Graphics g)
        {
            if (bodyLandmarks.Count == 0) return;

            foreach (var landmark in bodyLandmarks)
            {
                int pointSize = Math.Max(4, (int)(8 / zoomFactor));

                using (Brush brush = new SolidBrush(Color.Orange))
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    g.FillEllipse(brush, landmark.Location.X - pointSize / 2, landmark.Location.Y - pointSize / 2, pointSize, pointSize);
                    g.DrawEllipse(pen, landmark.Location.X - pointSize / 2, landmark.Location.Y - pointSize / 2, pointSize, pointSize);

                    using (Font font = new Font("Arial", Math.Max(8, 10 / zoomFactor), FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Blue)))
                    {
                        SizeF textSize = g.MeasureString(landmark.Name, font);
                        RectangleF textRect = new RectangleF(
                            landmark.Location.X - textSize.Width / 2,
                            landmark.Location.Y - textSize.Height - pointSize - 5,
                            textSize.Width + 4, textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(landmark.Name, font, textBrush,
                            landmark.Location.X - textSize.Width / 2 + 2,
                            landmark.Location.Y - textSize.Height - pointSize - 3);
                    }
                }
            }
        }

        private void DrawGrid(Graphics g)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(100, Color.LightBlue)))
            using (Pen axisPen = new Pen(Color.Red, 1.5f))
            {
                gridPen.DashStyle = DashStyle.Dot;

                PointF topLeft = calcService.TransformPointToImage(new Point(0, 0), inverseTransform);
                PointF bottomRight = calcService.TransformPointToImage(
                    new Point(drawingPanel.Width, drawingPanel.Height), inverseTransform);

                int startX = (int)(topLeft.X / 50) * 50 - 100;
                int endX = (int)(bottomRight.X / 50) * 50 + 100;
                int startY = (int)(topLeft.Y / 50) * 50 - 100;
                int endY = (int)(bottomRight.Y / 50) * 50 + 100;

                for (int x = startX; x <= endX; x += 50)
                    if (x >= -1000 && x <= 10000)
                        g.DrawLine(gridPen, x, startY, x, endY);

                for (int y = startY; y <= endY; y += 50)
                    if (y >= -1000 && y <= 10000)
                        g.DrawLine(gridPen, startX, y, endX, y);

                g.DrawLine(axisPen, gridOrigin.X, startY, gridOrigin.X, endY);
                g.DrawLine(axisPen, startX, gridOrigin.Y, endX, gridOrigin.Y);
                g.FillEllipse(Brushes.Red, gridOrigin.X - 5, gridOrigin.Y - 5, 10, 10);
            }
        }

        private void DrawCurrentToolPreview(Graphics g)
        {
            Point currentPos = drawingPanel.PointToClient(Cursor.Position);
            PointF imageCurrentPos = calcService.TransformPointToImage(currentPos, inverseTransform);

            if (!calcService.IsValidPoint(imageCurrentPos)) return;

            Point imagePoint = new Point((int)imageCurrentPos.X, (int)imageCurrentPos.Y);

            // Connection line preview
            if (isCreatingLineBetweenPoints && selectedPointForLine.HasValue)
            {
                using (Pen connectionPen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(connectionPen, selectedPointForLine.Value, imagePoint);
                }
            }

            // Tool previews
            using (Pen tempPen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash })
            {
                if (currentTool == ToolMode.Angle)
                {
                    if (angleVertex.HasValue && angleFirstPoint.HasValue)
                    {
                        if (calcService.IsValidPoint(angleVertex.Value) && calcService.IsValidPoint(angleFirstPoint.Value))
                        {
                            g.DrawLine(tempPen, angleVertex.Value, angleFirstPoint.Value);
                            g.DrawLine(tempPen, angleVertex.Value, imagePoint);
                            DrawAngleArcPreview(g, angleVertex.Value, angleFirstPoint.Value, imagePoint);
                        }
                    }
                    else if (angleVertex.HasValue && calcService.IsValidPoint(angleVertex.Value))
                    {
                        g.DrawLine(tempPen, angleVertex.Value, imagePoint);
                    }
                }
                else if (currentTool == ToolMode.AngleWithAxis)
                {
                    if (currentStartPoint.HasValue && calcService.IsValidPoint(currentStartPoint.Value))
                    {
                        g.DrawLine(tempPen, currentStartPoint.Value, imagePoint);
                    }
                }
                else if (currentStartPoint.HasValue && calcService.IsValidPoint(currentStartPoint.Value))
                {
                    g.DrawLine(tempPen, currentStartPoint.Value, imagePoint);

                    if (currentTool == ToolMode.Line || currentTool == ToolMode.Distance)
                    {
                        DrawAngleHelpers(g, currentStartPoint.Value, imagePoint);
                    }
                }
                else if (currentTool == ToolMode.Perpendicular && isSelectingBaseLine && selectedLineForPerpendicular.HasValue)
                {
                    Point foot = calcService.CalculatePerpendicularFoot(selectedLineForPerpendicular.Value, imagePoint);

                    if (calcService.IsValidPoint(foot))
                    {
                        using (Pen previewPen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                        {
                            g.DrawLine(previewPen, foot, imagePoint);
                        }

                        using (Brush symbolBrush = new SolidBrush(Color.Cyan))
                        {
                            g.FillRectangle(symbolBrush, foot.X - 3, foot.Y - 3, 6, 6);
                        }
                    }
                }
            }
        }

        private void DrawAngleArcPreview(Graphics g, PointF vertex, PointF point1, PointF point2)
        {
            try
            {
                float startAngle, sweepAngle;
                calcService.CalculateArcAngles(vertex, point1, point2, out startAngle, out sweepAngle);

                if (!float.IsNaN(startAngle) && !float.IsNaN(sweepAngle) &&
                    !float.IsInfinity(startAngle) && !float.IsInfinity(sweepAngle))
                {
                    using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 2))
                    {
                        arcPen.DashStyle = DashStyle.Dash;
                        float radius = 30f;
                        RectangleF arcRect = new RectangleF(
                            vertex.X - radius, vertex.Y - radius, radius * 2, radius * 2);

                        if (arcRect.Width > 0 && arcRect.Height > 0)
                        {
                            g.DrawArc(arcPen, arcRect, startAngle, sweepAngle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing angle arc: {ex.Message}");
            }
        }

        private void DrawAngleHelpers(Graphics g, Point start, Point end)
        {
            Point horizontalEnd = new Point(end.X, start.Y);
            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Green)) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(helperPen, start, horizontalEnd);
            }

            Point verticalEnd = new Point(start.X, end.Y);
            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Blue)) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(helperPen, start, verticalEnd);
            }

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * (180 / Math.PI);
            using (Font font = new Font("Arial", 9))
            using (Brush brush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
            {
                string angleText = $"{angle:F1}°";
                SizeF textSize = g.MeasureString(angleText, font);
                Point midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
                RectangleF textRect = new RectangleF(
                    midPoint.X - textSize.Width / 2, midPoint.Y - textSize.Height - 5,
                    textSize.Width + 4, textSize.Height);

                g.FillRectangle(bgBrush, textRect);
                g.DrawString(angleText, font, brush,
                    midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 3);
            }
        }

        private void DrawAngleArc(Graphics g, Measurement m1, Measurement m2)
        {
            if (m1.Type != MeasurementType.Angle || !m1.Vertex.HasValue ||
                m2.Type != MeasurementType.Angle || !m2.Vertex.HasValue) return;

            Point v1 = new Point(m1.End.X - m1.Vertex.Value.X, m1.End.Y - m1.Vertex.Value.Y);
            Point v2 = new Point(m2.End.X - m2.Vertex.Value.X, m2.End.Y - m2.Vertex.Value.Y);

            double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
            double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

            if (angle1 < 0) angle1 += 360;
            if (angle2 < 0) angle2 += 360;

            float startAngle, sweepAngle;
            double diff = Math.Abs(angle1 - angle2);

            if (diff <= 180)
            {
                startAngle = (float)Math.Min(angle1, angle2);
                sweepAngle = (float)Math.Abs(angle1 - angle2);
            }
            else
            {
                startAngle = (float)Math.Max(angle1, angle2);
                sweepAngle = (float)(360 - Math.Abs(angle1 - angle2));
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

            double angle = calcService.CalculateAngleWithAxis(m);
            float startAngle = m.Axis.Value == AxisType.X ? 0 : 90;
            float sweepAngle = (float)angle;

            Point lineMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);

            using (Pen arcPen = new Pen(Color.FromArgb(100, Color.Orange), 2))
            {
                arcPen.DashStyle = DashStyle.Dash;
                g.DrawArc(arcPen, lineMidPoint.X - 30, lineMidPoint.Y - 30, 60, 60, startAngle, sweepAngle);
            }
        }

        private void DrawHoverLabel(Graphics g, Point point, string text)
        {
            using (Font font = new Font("Arial", 9, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(text, font);
                RectangleF textRect = new RectangleF(
                    point.X - textSize.Width / 2, point.Y - textSize.Height - 15,
                    textSize.Width + 8, textSize.Height + 4);

                g.FillRectangle(bgBrush, textRect);
                g.DrawRectangle(Pens.White, textRect.X, textRect.Y, textRect.Width, textRect.Height);
                g.DrawString(text, font, textBrush,
                    point.X - textSize.Width / 2 + 4, point.Y - textSize.Height - 13);
            }
        }

        private void DrawZoomLevel(Graphics g)
        {
            string zoomText = $"Zoom: {zoomFactor * 100:F0}%";
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(zoomText, font);
                RectangleF textRect = new RectangleF(10, 10, textSize.Width + 8, textSize.Height + 4);
                g.FillRectangle(bgBrush, textRect);
                g.DrawString(zoomText, font, brush, 12, 12);
            }
        }

        #endregion

        #region Form Events

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (drawingPanel != null)
            {
                UpdateTransformationMatrices();
                drawingPanel.Invalidate();
            }
        }

        private void BodyPictureAnalyzer_Load(object sender, EventArgs e)
        {
            UpdateStatus("Application started. Import an image to begin.");
        }

        #endregion
    }
}