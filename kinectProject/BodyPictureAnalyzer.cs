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
        private enum ToolMode { None, Line, Point, Angle, AngleWithAxis, Distance, Reference, Perpendicular }
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
            public AxisType? Axis;
            public Point? Vertex;
            public int ID;

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

        // Zoom state
        private float zoomFactor = 1.0f;
        private PointF panOffset = PointF.Empty;
        private bool isPanning = false;
        private Point panStart;
        private Matrix transformMatrix = new Matrix();
        private Matrix inverseTransform = new Matrix();

        // UI Controls
        protected DoubleBufferedPanel drawingPanel;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ListView measurementsList;

        public BodyPictureAnalyzer()
        {
            InitializeComponents();
            this.DoubleBuffered = true;
            SetupUI();
            UpdateStatus("Ready to import an image");
        }

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
            // Main form setup
            this.Text = "Advanced Image Measurement Tool with Zoom";
            this.Size = new Size(1200, 800);
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            // Toolstrip setup
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;
            toolStrip.BackColor = Color.FromArgb(62, 62, 64);
            toolStrip.ForeColor = Color.White;
            toolStrip.RenderMode = ToolStripRenderMode.Professional;
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

            // Zoom controls
            AddToolSeparator();
            AddToolButton("🔍 Zoom In", BtnZoomIn_Click);
            AddToolButton("🔍 Zoom Out", BtnZoomOut_Click);
            AddToolButton("🔍 Zoom Fit", BtnZoomFit_Click);
            AddToolButton("🔍 Zoom 100%", BtnZoomReset_Click);
            AddToolButton("✋ Pan", BtnPan_Click);

            // Drawing panel - Using DoubleBufferedPanel for smooth zoom
            drawingPanel = new DoubleBufferedPanel();
            drawingPanel.Dock = DockStyle.Fill;
            drawingPanel.BackColor = Color.FromArgb(37, 37, 38);
            drawingPanel.BorderStyle = BorderStyle.FixedSingle;

            drawingPanel.Paint += DrawingPanel_Paint;
            drawingPanel.MouseClick += DrawingPanel_MouseClick;
            drawingPanel.MouseDown += DrawingPanel_MouseDown;
            drawingPanel.MouseMove += DrawingPanel_MouseMove;
            drawingPanel.MouseUp += DrawingPanel_MouseUp;
            drawingPanel.MouseWheel += DrawingPanel_MouseWheel;
            drawingPanel.MouseLeave += DrawingPanel_MouseLeave;
            drawingPanel.Resize += DrawingPanel_Resize;

            // Measurements list
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
            this.Controls.Add(drawingPanel);
            this.Controls.Add(measurementsList);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);

            // Initialize grid origin
            gridOrigin = new Point(drawingPanel.Width / 2, drawingPanel.Height / 2);
            UpdateTransformationMatrices();
        }

        private void DrawingPanel_Resize(object sender, EventArgs e)
        {
            drawingPanel.Invalidate();
        }

        private void BodyPictureAnalyzer_Load(object sender, EventArgs e)
        {
            UpdateStatus("Application started. Import an image to begin.");
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

        #region Zoom and Pan Methods

        private void BtnZoomIn_Click(object sender, EventArgs e)
        {
            ZoomAtCenter(1.25f);
        }

        private void BtnZoomOut_Click(object sender, EventArgs e)
        {
            ZoomAtCenter(0.8f);
        }

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

            float scaleX = (float)drawingPanel.Width / originalImage.Width;
            float scaleY = (float)drawingPanel.Height / originalImage.Height;
            zoomFactor = Math.Min(scaleX, scaleY) * 0.95f;
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
            PointF center = new PointF(drawingPanel.Width / 2, drawingPanel.Height / 2);
            ZoomAtPoint(center, zoom);
        }

        private void ZoomAtPoint(PointF point, float zoom)
        {
            float oldZoom = zoomFactor;
            zoomFactor *= zoom;
            zoomFactor = Math.Max(0.1f, Math.Min(20f, zoomFactor));

            if (oldZoom != zoomFactor)
            {
                // Calculate the point in image coordinates before zoom
                PointF imagePointBefore = TransformPointToImage(point);

                // Update transformation
                UpdateTransformationMatrices();

                // Calculate the same point in image coordinates after zoom
                PointF imagePointAfter = TransformPointToImage(point);

                // Adjust pan offset to keep the point under the mouse
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

        private PointF TransformPointToImage(PointF screenPoint)
        {
            PointF[] points = new PointF[] { screenPoint };
            inverseTransform.TransformPoints(points);
            return points[0];
        }

        private PointF TransformPointToScreen(PointF imagePoint)
        {
            PointF[] points = new PointF[] { imagePoint };
            transformMatrix.TransformPoints(points);
            return points[0];
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

                // Clear the panel first
                e.Graphics.Clear(drawingPanel.BackColor);

                if (originalImage == null)
                {
                    // Draw placeholder text when no image is loaded
                    string message = "No image loaded. Click 'Import Image' to begin.";
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 14, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.White))
                    {
                        SizeF textSize = e.Graphics.MeasureString(message, font);
                        PointF position = new PointF(
                            (drawingPanel.Width - textSize.Width) / 2,
                            (drawingPanel.Height - textSize.Height) / 2
                        );
                        e.Graphics.DrawString(message, font, brush, position);
                    }
                    return;
                }

                // Apply zoom transformation
                e.Graphics.Transform = transformMatrix;

                // Draw the image
                e.Graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                // Draw grid if enabled
                if (showGrid)
                {
                    DrawGrid(e.Graphics);
                }

                // Draw measurements
                foreach (var m in measurements)
                {
                    DrawMeasurement(e.Graphics, m);
                }

                // Draw current tool preview
                if (currentTool != ToolMode.None)
                {
                    DrawCurrentToolPreview(e.Graphics);
                }

                // Reset transformation for UI elements that need screen coordinates
                e.Graphics.ResetTransform();

                // Draw hover information
                if (hoverPoint.HasValue && !string.IsNullOrEmpty(hoverMeasurementName))
                {
                    PointF screenHoverPoint = TransformPointToScreen(hoverPoint.Value);
                    DrawHoverLabel(e.Graphics, new Point((int)screenHoverPoint.X, (int)screenHoverPoint.Y), hoverMeasurementName);
                }

                // Draw zoom level
                DrawZoomLevel(e.Graphics);
            }
            catch (Exception ex)
            {
                // Handle drawing errors gracefully
                Debug.WriteLine($"Drawing error: {ex.Message}");

                // Draw error message
                using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                using (Brush brush = new SolidBrush(Color.Red))
                {
                    e.Graphics.DrawString("Drawing error occurred", font, brush, 10, 10);
                }
            }
        }

        private void DrawCurrentToolPreview(Graphics g)
        {
            Point currentPos = drawingPanel.PointToClient(Cursor.Position);
            PointF imageCurrentPos = TransformPointToImage(currentPos);

            // Validate points before using them
            if (float.IsNaN(imageCurrentPos.X) || float.IsNaN(imageCurrentPos.Y))
                return;

            using (Pen tempPen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash })
            {
                if (currentTool == ToolMode.Angle)
                {
                    if (angleVertex.HasValue && angleFirstPoint.HasValue)
                    {
                        // Validate all points
                        if (IsValidPoint(angleVertex.Value) && IsValidPoint(angleFirstPoint.Value))
                        {
                            g.DrawLine(tempPen, angleVertex.Value, angleFirstPoint.Value);
                            g.DrawLine(tempPen, angleVertex.Value, imageCurrentPos);
                            DrawAngleArcPreview(g, angleVertex.Value, angleFirstPoint.Value, imageCurrentPos);
                        }
                    }
                    else if (angleVertex.HasValue && IsValidPoint(angleVertex.Value))
                    {
                        g.DrawLine(tempPen, angleVertex.Value, imageCurrentPos);
                    }
                }
                else if (currentTool == ToolMode.AngleWithAxis)
                {
                    if (currentStartPoint.HasValue)
                    {
                        g.DrawLine(tempPen, currentStartPoint.Value, imageCurrentPos);
                    }
                }
                else if (currentStartPoint.HasValue && IsValidPoint(currentStartPoint.Value))
                {
                    g.DrawLine(tempPen, currentStartPoint.Value, imageCurrentPos);

                    // Draw helper for 90° angles
                    if (currentTool == ToolMode.Line || currentTool == ToolMode.Distance)
                    {
                        DrawAngleHelpers(g, currentStartPoint.Value, new Point((int)imageCurrentPos.X, (int)imageCurrentPos.Y));
                    }
                }
                else if (currentTool == ToolMode.Perpendicular && isSelectingBaseLine && selectedLineForPerpendicular.HasValue)
                {
                    Point foot;

                    if (selectedLineForPerpendicular.Value.Type == MeasurementType.Angle && selectedLineForPerpendicular.Value.Vertex.HasValue)
                    {
                        // For angle segments, use vertex and endpoint
                        foot = CalculatePerpendicularFoot(
                            new Measurement(selectedLineForPerpendicular.Value.Vertex.Value,
                                          selectedLineForPerpendicular.Value.End,
                                          "", MeasurementType.Line, 0),
                            new Point((int)imageCurrentPos.X, (int)imageCurrentPos.Y));
                    }
                    else
                    {
                        // For regular lines
                        foot = CalculatePerpendicularFoot(selectedLineForPerpendicular.Value,
                                                        new Point((int)imageCurrentPos.X, (int)imageCurrentPos.Y));
                    }

                    if (IsValidPoint(foot))
                    {
                        using (Pen previewPen = new Pen(Color.Cyan, 2) { DashStyle = DashStyle.Dash })
                        {
                            g.DrawLine(previewPen, foot, imageCurrentPos);
                        }

                        // Draw perpendicular symbol
                        using (Brush symbolBrush = new SolidBrush(Color.Cyan))
                        {
                            g.FillRectangle(symbolBrush, foot.X - 3, foot.Y - 3, 6, 6);
                        }
                    }
                }

            }
        }

        // Helper method to validate points
        private bool IsValidPoint(Point point)
        {
            return !float.IsNaN(point.X) && !float.IsNaN(point.Y) &&
                   !float.IsInfinity(point.X) && !float.IsInfinity(point.Y);
        }

        private bool IsValidPoint(PointF point)
        {
            return !float.IsNaN(point.X) && !float.IsNaN(point.Y) &&
                   !float.IsInfinity(point.X) && !float.IsInfinity(point.Y);
        }

        private void DrawGrid(Graphics g)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(100, Color.LightBlue)))
            using (Pen axisPen = new Pen(Color.Red, 1.5f))
            {
                gridPen.DashStyle = DashStyle.Dot;

                // Calculate visible area in image coordinates
                PointF topLeft = TransformPointToImage(new Point(0, 0));
                PointF bottomRight = TransformPointToImage(new Point(drawingPanel.Width, drawingPanel.Height));

                // Extended grid boundaries (larger than visible area for panning)
                int startX = (int)(topLeft.X / 50) * 50 - 100;
                int endX = (int)(bottomRight.X / 50) * 50 + 100;
                int startY = (int)(topLeft.Y / 50) * 50 - 100;
                int endY = (int)(bottomRight.Y / 50) * 50 + 100;

                // Draw vertical grid lines
                for (int x = startX; x <= endX; x += 50)
                {
                    if (x >= -1000 && x <= 10000) // Reasonable limits
                    {
                        g.DrawLine(gridPen, x, startY, x, endY);
                    }
                }

                // Draw horizontal grid lines
                for (int y = startY; y <= endY; y += 50)
                {
                    if (y >= -1000 && y <= 10000) // Reasonable limits
                    {
                        g.DrawLine(gridPen, startX, y, endX, y);
                    }
                }

                // Draw axes
                g.DrawLine(axisPen, gridOrigin.X, startY, gridOrigin.X, endY);
                g.DrawLine(axisPen, startX, gridOrigin.Y, endX, gridOrigin.Y);

                // Draw grid origin point
                g.FillEllipse(Brushes.Red, gridOrigin.X - 5, gridOrigin.Y - 5, 10, 10);
            }
        }

        private void DrawMeasurement(Graphics g, Measurement m)
        {
            Color color = m.IsSelected ? Color.Yellow : GetMeasurementColor(m.Type);

            // Adjust sizes based on zoom
            int lineWidth = Math.Max(1, (int)((m.IsSelected ? 3 : 2) / zoomFactor));
            int pointSize = Math.Max(3, (int)((m.IsSelected ? 8 : 6) / zoomFactor));
            float fontSize = Math.Max(6, 9 / zoomFactor);

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", fontSize, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);

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

                        string lineId = m.ID.ToString();
                        SizeF lineIdSize = g.MeasureString(lineId, font);
                        PointF lineMidPoint = new PointF((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
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

                        double distance = CalculateDistance(m.Start, m.End);
                        string distText = m.Type == MeasurementType.ReferenceLine ?
                            $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                            isReferenceSet ? $"{m.ID}" : $"{m.ID}";

                        PointF midPoint = new PointF((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        SizeF textSize = g.MeasureString(distText, font);
                        RectangleF textRect = new RectangleF(
                            midPoint.X - textSize.Width / 2, midPoint.Y - textSize.Height - 10,
                            textSize.Width + 4, textSize.Height);
                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(distText, font, textBrush, midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 8);
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
                                string angleText = $"{m.ID}: {angle:F1}°";

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
                        string axisAngleText = $"{m.ID}: {axisAngle:F1}° to {m.Axis}";

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

        private void DrawHoverLabel(Graphics g, Point point, string text)
        {
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 9, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(text, font);

                RectangleF textRect = new RectangleF(
                    point.X - textSize.Width / 2,
                    point.Y - textSize.Height - 15,
                    textSize.Width + 8,
                    textSize.Height + 4);

                g.FillRectangle(bgBrush, textRect);
                g.DrawRectangle(Pens.White, textRect.X, textRect.Y, textRect.Width, textRect.Height);

                g.DrawString(text, font, textBrush,
                    point.X - textSize.Width / 2 + 4,
                    point.Y - textSize.Height - 13);
            }
        }

        private void DrawZoomLevel(Graphics g)
        {
            string zoomText = $"Zoom: {zoomFactor * 100:F0}%";
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 10, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                SizeF textSize = g.MeasureString(zoomText, font);
                RectangleF textRect = new RectangleF(10, 10, textSize.Width + 8, textSize.Height + 4);
                g.FillRectangle(bgBrush, textRect);
                g.DrawString(zoomText, font, brush, 12, 12);
            }
        }

        private void DrawAngleArcPreview(Graphics g, PointF vertex, PointF point1, PointF point2)
        {
            try
            {
                PointF v1 = new PointF(point1.X - vertex.X, point1.Y - vertex.Y);
                PointF v2 = new PointF(point2.X - vertex.X, point2.Y - vertex.Y);

                double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
                double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

                float startAngle = (float)Math.Min(angle1, angle2);
                float sweepAngle = (float)Math.Abs(angle1 - angle2);

                // Validate parameters before drawing
                if (!float.IsNaN(startAngle) && !float.IsNaN(sweepAngle) &&
                    !float.IsInfinity(startAngle) && !float.IsInfinity(sweepAngle))
                {
                    using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 2))
                    {
                        arcPen.DashStyle = DashStyle.Dash;

                        // Use valid rectangle dimensions
                        float radius = 30f;
                        RectangleF arcRect = new RectangleF(
                            vertex.X - radius,
                            vertex.Y - radius,
                            radius * 2,
                            radius * 2);

                        // Ensure rectangle has positive dimensions
                        if (arcRect.Width > 0 && arcRect.Height > 0)
                        {
                            g.DrawArc(arcPen, arcRect, startAngle, sweepAngle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle drawing errors to prevent crashes
                Debug.WriteLine($"Error drawing angle arc: {ex.Message}");
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
                PointF imagePointF = TransformPointToImage(e.Location);
                Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

                // FIX: Check if clicking near grid origin for dragging
                PointF screenGridOrigin = TransformPointToScreen(gridOrigin);
                if (IsNearPoint(e.Location, new Point((int)screenGridOrigin.X, (int)screenGridOrigin.Y), gridGrabRadius))
                {
                    isDraggingGrid = true;
                    drawingPanel.Cursor = Cursors.SizeAll;
                    return;
                }

                // Handle measurement selection for moving
                if (currentEditMode == EditMode.Move)
                {
                    int index = FindMeasurementAtPoint(imagePoint);
                    if (index >= 0)
                    {
                        DeselectAllMeasurements();
                        Measurement m = measurements[index];
                        m.IsSelected = true;
                        measurements[index] = m;
                        selectedMeasurementIndex = index;
                        selectedMeasurement = m;

                        // Calculate offset based on where the user clicked on the measurement
                        if (m.Type == MeasurementType.Point)
                        {
                            dragOffset = new Point(
                                imagePoint.X - m.Start.X,
                                imagePoint.Y - m.Start.Y);
                        }
                        else
                        {
                            // For lines, find the closest point to where user clicked
                            double distanceToStart = CalculateDistance(imagePoint, m.Start);
                            double distanceToEnd = CalculateDistance(imagePoint, m.End);

                            if (distanceToStart < distanceToEnd)
                            {
                                // User clicked near the start point
                                dragOffset = new Point(
                                    imagePoint.X - m.Start.X,
                                    imagePoint.Y - m.Start.Y);
                            }
                            else
                            {
                                // User clicked near the end point
                                dragOffset = new Point(
                                    imagePoint.X - m.End.X,
                                    imagePoint.Y - m.End.Y);
                            }
                        }

                        isDraggingMeasurement = true;
                        drawingPanel.Cursor = Cursors.SizeAll;
                        drawingPanel.Invalidate();
                    }
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
                // Start panning with middle mouse button
                isPanning = true;
                panStart = e.Location;
                drawingPanel.Cursor = Cursors.SizeAll;
            }
        }

        private void DrawingPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (originalImage == null) return;

            // Handle panning FIRST
            if (isPanning && (e.Button & MouseButtons.Left) == MouseButtons.Left ||
                isPanning && (e.Button & MouseButtons.Middle) == MouseButtons.Middle)
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

            // FIX: Handle grid dragging
            if (isDraggingGrid)
            {
                PointF newGridOrigin = TransformPointToImage(e.Location);
                gridOrigin = new Point((int)newGridOrigin.X, (int)newGridOrigin.Y);
                drawingPanel.Invalidate();
                return;
            }

            PointF imagePointF = TransformPointToImage(e.Location);
            Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

            if (isDraggingMeasurement && selectedMeasurement.HasValue && selectedMeasurementIndex >= 0)
            {
                MoveMeasurement(selectedMeasurementIndex, imagePoint);
                drawingPanel.Invalidate();
            }
            else
            {
                // Handle hover effect
                UpdateHoverInfo(imagePoint);

                // FIX: Update cursor when near grid origin
                PointF screenGridOrigin = TransformPointToScreen(gridOrigin);
                if (IsNearPoint(e.Location, new Point((int)screenGridOrigin.X, (int)screenGridOrigin.Y), gridGrabRadius))
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

            PointF imagePointF = TransformPointToImage(e.Location);
            Point imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

            // FIX: Don't handle measurement creation if we're dragging grid
            if (isDraggingGrid) return;

            // Handle measurement creation
            if (currentTool != ToolMode.None && e.Button == MouseButtons.Left)
            {
                HandleMeasurementCreation(imagePoint);
            }

            // Handle selection for moving, deleting, or renaming
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
                case ToolMode.Line: statusText = "Line Tool: Click to place start and end points"; break;
                case ToolMode.Point: statusText = "Point Tool: Click to place a point"; break;
                case ToolMode.Angle: statusText = "Angle Tool: Click to place vertex, then two end points"; break;
                case ToolMode.AngleWithAxis: statusText = "Angle with Axis: Draw a line, then select axis"; break;
                case ToolMode.Distance: statusText = "Distance Tool: Click to measure distance"; break;
                case ToolMode.Reference: statusText = "Reference Tool: Draw a line of known length"; break;
                case ToolMode.Perpendicular: statusText = "Perpendicular Tool: Select a line first, then click to place perpendicular line"; break;
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
            switch (mode)
            {
                case EditMode.Normal:
                    statusText = "Normal Mode: Hover over measurements to see details";
                    drawingPanel.Cursor = Cursors.Default;
                    break;
                case EditMode.Delete:
                    statusText = "Delete Mode: Click on measurement to delete";
                    drawingPanel.Cursor = Cursors.No;
                    break;
                case EditMode.Move:
                    statusText = "Move Mode: Click and drag to move measurement";
                    drawingPanel.Cursor = Cursors.Hand;
                    break;
                case EditMode.Rename:
                    statusText = "Rename Mode: Click on measurement to rename";
                    drawingPanel.Cursor = Cursors.UpArrow;
                    break;
            }

            UpdateStatus(statusText);
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

        #region Measurement Creation and Handling

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
                        drawingPanel.Invalidate();
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
                    drawingPanel.Invalidate();
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
                        drawingPanel.Invalidate();
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
                            m.Axis = (AxisType?)axisDialog.SelectedAxis;
                            measurements[measurements.Count - 1] = m;
                        }

                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
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
                        drawingPanel.Invalidate();
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
                        drawingPanel.Invalidate();

                        // Prompt for reference value
                        using (var inputDialog = new ReferenceInputDialogD())
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
                                              measurements[lineIndex].Type == MeasurementType.Distance ||
                                              measurements[lineIndex].Type == MeasurementType.ReferenceLine ||
                                              measurements[lineIndex].Type == MeasurementType.Angle)) // ADD THIS LINE
                        {
                            selectedLineForPerpendicular = measurements[lineIndex];
                            isSelectingBaseLine = true;
                            UpdateStatus("Base line selected. Now click to place perpendicular line endpoint");

                            // Highlight the selected line
                            DeselectAllMeasurements();
                            Measurement m = measurements[lineIndex];
                            m.IsSelected = true;
                            measurements[lineIndex] = m;
                            drawingPanel.Invalidate();
                        }
                        else
                        {
                            UpdateStatus("Please select a valid line first (Line, Distance, Reference, or Angle)");
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
                            drawingPanel.Invalidate();
                        }
                    }
                    break;

            }
        }

        private void CreatePerpendicularLine(Measurement baseLine, Point endPoint)
        {
            Point A, B;

            // Handle different line types
            if (baseLine.Type == MeasurementType.Angle && baseLine.Vertex.HasValue)
            {
                // For angle segments, use the vertex and endpoint as the line
                A = baseLine.Vertex.Value;
                B = baseLine.End;
            }
            else
            {
                // For regular lines, use start and end points
                A = baseLine.Start;
                B = baseLine.End;
            }

            Point C = endPoint;

            // Calculate the perpendicular projection of point C onto line AB
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (Math.Abs(lengthSquared) < 0.0001) return; // Avoid division by zero

            // Calculate projection parameter t
            double t = ((C.X - A.X) * dx + (C.Y - A.Y) * dy) / lengthSquared;

            // For angle segments, don't clamp t to [0,1] to allow perpendiculars beyond the segment
            if (baseLine.Type == MeasurementType.Angle)
            {
                // Allow perpendiculars to extend beyond the angle segment
                // t can be any value, but we'll limit it to reasonable bounds to prevent extreme lines
                t = Math.Max(-2, Math.Min(3, t)); // Allow some extension beyond the segment
            }
            else
            {
                // For regular lines, clamp to the segment
                t = Math.Max(0, Math.Min(1, t));
            }

            // Calculate the perpendicular foot point
            Point perpendicularFoot = new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy)
            );

            // Only create the perpendicular line if the foot point is different from the endpoint
            if (CalculateDistance(perpendicularFoot, C) > 5) // Minimum distance threshold
            {
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
            else
            {
                UpdateStatus("Perpendicular line too short - not created");
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
                    drawingPanel.Invalidate();
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
                drawingPanel.Invalidate();
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

            // Don't clamp t for angle segments to allow perpendiculars beyond the segment
            if (baseLine.Type != MeasurementType.Angle)
            {
                t = Math.Max(0, Math.Min(1, t));
            }

            return new Point(
                (int)(A.X + t * dx),
                (int)(A.Y + t * dy)
            );
        }
        #endregion

        #region Measurement Calculations and Utilities

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
                    drawingPanel.Invalidate();
                    UpdateStatus($"Measurement renamed to {m.Name}");
                }
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
                case MeasurementType.PerpendicularLine:
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

        private double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private void UpdateHoverInfo(Point imagePoint)
        {
            int index = FindMeasurementAtPoint(imagePoint);
            if (index >= 0)
            {
                hoverMeasurement = measurements[index];
                hoverPoint = GetHoverPointForMeasurement(hoverMeasurement.Value, imagePoint);
                hoverMeasurementName = GetHoverTextForMeasurement(hoverMeasurement.Value);
            }
            else
            {
                hoverPoint = null;
                hoverMeasurementName = "";
                hoverMeasurement = null;
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
                case MeasurementType.PerpendicularLine:
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
                case MeasurementType.PerpendicularLine:
                    double perpLength = CalculateDistance(m.Start, m.End);
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

        #endregion

        #region Image and Measurement Management

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
            drawingPanel.Invalidate();
        }

        private void BtnToggleGrid_Click(object sender, EventArgs e)
        {
            showGrid = !showGrid;
            drawingPanel.Invalidate();
        }

        #endregion

        #region ListView Management

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

            drawingPanel.Invalidate();
        }

        private void UpdateMeasurementsList()
        {
            measurementsList.Items.Clear();

            // Group measurements by ID to avoid duplicates
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

                case MeasurementType.PerpendicularLine:
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

        #endregion

        #region PDF Export

        private void ExportToPdf()
        {
            if (originalImage == null)
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
            if (originalImage != null)
            {
                try
                {
                    // Create a bitmap with the original image dimensions
                    using (Bitmap bmp = new Bitmap(originalImage.Width, originalImage.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                            // Draw measurements on the image
                            foreach (var m in measurements)
                            {
                                DrawMeasurementOnBitmap(g, m);
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

        private void DrawMeasurementOnBitmap(Graphics g, Measurement m)
        {
            // Similar to DrawMeasurement but for the PDF export bitmap
            Color color = GetMeasurementColor(m.Type);
            int lineWidth = 2;
            int pointSize = 6;

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 10, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.Black))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.DrawString(m.ID.ToString(), font, textBrush, m.Start.X + 5, m.Start.Y - 10);
                        break;

                    case MeasurementType.Line:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);
                        Point lineMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        g.DrawString(m.ID.ToString(), font, textBrush, lineMidPoint.X, lineMidPoint.Y - 15);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        double distance = CalculateDistance(m.Start, m.End);
                        string distText = m.Type == MeasurementType.ReferenceLine ?
                            $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                            isReferenceSet ?
                                $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                                $"{m.ID}: {distance:F1} px";

                        Point midPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        g.DrawString(distText, font, textBrush, midPoint.X, midPoint.Y - 15);
                        break;

                    case MeasurementType.Angle:
                        if (m.Vertex.HasValue)
                        {
                            g.DrawLine(pen, m.Vertex.Value, m.End);
                            g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);
                            g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                            // Find the other segment that shares the same vertex and ID
                            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                meas.Type == MeasurementType.Angle &&
                                meas.Vertex.HasValue &&
                                meas.ID == m.ID &&
                                meas.End != m.End);

                            if (otherSegment.Type == MeasurementType.Angle)
                            {
                                double angle = CalculateAngle(m, otherSegment);
                                string angleText = $"{m.ID}: {angle:F1}°";
                                g.DrawString(angleText, font, textBrush, m.Vertex.Value.X, m.Vertex.Value.Y - 20);
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        double axisAngle = CalculateAngleWithAxis(m);
                        string axisAngleText = $"{m.ID}: {axisAngle:F1}° to {m.Axis}";
                        Point axisMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        g.DrawString(axisAngleText, font, textBrush, axisMidPoint.X, axisMidPoint.Y - 15);
                        break;

                    case MeasurementType.PerpendicularLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        double perpLength = CalculateDistance(m.Start, m.End);
                        string perpText = $"{m.ID}: ";

                        Point perpMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        g.DrawString(perpText, font, textBrush, perpMidPoint.X, perpMidPoint.Y - 15);

                        // Draw perpendicular symbol
                        using (Pen symbolPen = new Pen(Color.Black, 1))
                        {
                            g.DrawRectangle(symbolPen, m.Start.X - 2, m.Start.Y - 2, 4, 4);
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
                case MeasurementType.PerpendicularLine:
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
                case MeasurementType.PerpendicularLine:
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

        #endregion

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (drawingPanel != null)
            {
                UpdateTransformationMatrices();
                drawingPanel.Invalidate();
            }
        }
    }

    #region Dialog Classes

    public enum AxisType { X, Y }

    public class AxisSelectionDialog : Form
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

    public class ReferenceInputDialogD : Form
    {
        private TextBox textBox;
        public float ReferenceLength { get; private set; }

        public ReferenceInputDialogD()
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

    public class RenameDialog : Form
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

    #endregion

    #region Custom ToolStrip Renderers and DoubleBufferedPanel

    public class CustomColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(62, 62, 64);
        public override Color MenuBorder => Color.FromArgb(100, 100, 100);
        public override Color MenuItemSelected => Color.FromArgb(87, 87, 90);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(87, 87, 90);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(87, 87, 90);
        public override Color ImageMarginGradientBegin => Color.FromArgb(55, 55, 58);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(55, 55, 58);
        public override Color ImageMarginGradientEnd => Color.FromArgb(55, 55, 58);
    }

    public class CustomToolStripRenderer : ToolStripProfessionalRenderer
    {
        public CustomToolStripRenderer() : base(new CustomColorTable()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
        }
    }

    #endregion
}