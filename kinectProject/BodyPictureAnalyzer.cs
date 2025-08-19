//using Microsoft.Kinect;
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Imaging;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Windows.Forms;

//namespace kinectProject
//{
//    public partial class BodyPictureAnalyzer : Form
//    {
//        private enum MeasurementMode { Distance, Angle , OnePoint }
//        private MeasurementMode currentMode = MeasurementMode.Distance;

//        private List<Measurement> measurements = new List<Measurement>();
//        private Image loadedImage;
//        private Point? currentStartPoint = null;
//        private int measurementCounter = 1;
//        ////
//        private float pixelToMmRatio = 1.0f; // Ratio px/mm (initialisé à 1 par défaut)
//        private bool isReferenceSet = false; // Indique si l'échelle est calibrée
//        /// <summary>
//        /// //
//        /// </summary>
//        private bool showPlan = true;
//        private Point planCenter;
//        private bool isDraggingPlan = false;
//        private const int planGrabRadius = 10; // sensitivity for grabbing the center

//        /// <summary>
//        /// /
//        /// </summary>

//        private Point? lastAngleVertex = null;
//        private AxisType lastAngleAxis;
//        private double lastLineAngle = 0;

//        private List<(Point vertex, double refAngle, double lineAngle, AxisType axis)> highlightedAngles
//    = new List<(Point vertex, double refAngle, double lineAngle, AxisType axis)>();
//        //

//        private bool deleteMode = false;
//        private const int lineClickThreshold = 5; // Sensitivity in pixels for selecting a line


//        public BodyPictureAnalyzer()
//        {
//            InitializeComponent();
//            this.DoubleBuffered = true;
//            UpdateModeDisplay();
//            this.DoubleBuffered = true;
//          //  pictureBox1.MouseDown += PictureBox1_MouseDown; // Nouvel événement

//        }


//        private void btnSetReferenceScale_Click(object sender, EventArgs e)
//        {
//            if (pictureBox1.Image == null)
//            {
//                MessageBox.Show("Please load an image first.");
//                return;
//            }

//            if (measurements.Count == 0 || measurements[measurements.Count - 1].Type != MeasurementType.Distance)
//            {
//                MessageBox.Show("The last measurement must be a distance line to set the reference scale.");
//                return;
//            }

//            using (var input = new ForReferenceInputDialog())
//            {
//                if (input.ShowDialog(this) == DialogResult.OK)
//                {
//                    SetScaleFromReference(input.ReferenceLength);
//                    pictureBox1.Invalidate();
//                }
//            }
//        }

//        public void SetScaleFromReference(float referenceLengthMm)
//        {
//            if (measurements.Count == 0 || measurements[measurements.Count - 1].Type != MeasurementType.Distance)
//            {
//                MessageBox.Show("La dernière mesure doit être une ligne de référence en mode Distance.");
//                return;
//            }

//            var referenceLine = measurements[measurements.Count - 1];
//            float pixelLength = (float)CalculatePixelDistance(referenceLine.Start, referenceLine.End);

//            if (referenceLengthMm <= 0 || pixelLength <= 0)
//            {
//                MessageBox.Show("Mesure invalide. Vérifiez les points sélectionnés et la valeur entrée.");
//                return;
//            }

//            pixelToMmRatio = pixelLength / referenceLengthMm;
//            isReferenceSet = true;

//            toolStripStatusLabel1.Text = $"Échelle calibrée : 1 mm = {pixelToMmRatio:F2} px";
//            CalculateMeasurements(); // Recalculer toutes les mesures
//        }


//        private double CalculatePixelDistance(Point p1, Point p2)
//        {
//            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
//        }

//        private double CalculateRealDistance(Point p1, Point p2)
//        {
//            double pixels = CalculatePixelDistance(p1, p2);
//            return pixels / pixelToMmRatio; // Conversion en mm
//        }

//        private void UpdateModeDisplay()
//        {
//            lblMode.Text = $"Mode: {currentMode}";
//            btnSwitchMode.Text = $"Switch to {(currentMode == MeasurementMode.Distance ? "Angle" : "Distance")}";
//            toolStripStatusLabel1.Text = currentMode == MeasurementMode.Distance
//                ? "Click to place start and end points for distance measurement"
//                : "Click to place connected segments for angle measurement";
//        }

//        private void btnImport_Click(object sender, EventArgs e)
//        {
//            using (OpenFileDialog openFileDialog = new OpenFileDialog())
//            {
//                openFileDialog.Filter = "Image Files|*.jpg;*.png;*.bmp";
//                openFileDialog.Title = "Select Body Image";

//                if (openFileDialog.ShowDialog() == DialogResult.OK)
//                {
//                    try
//                    {
//                        loadedImage = Image.FromFile(openFileDialog.FileName);
//                        pictureBox1.Image = (Image)loadedImage.Clone();
//                        measurements.Clear();
//                        lstMeasurements.Items.Clear();
//                        measurementCounter = 1;
//                        currentStartPoint = null;
//                        isReferenceSet = false; // Réinitialiser l'échelle
//                        pixelToMmRatio = 1.0f; // Réinitialiser le ratio
//                        UpdateModeDisplay();

//                        // ✅ Initialize plan center at image center
//                        planCenter = new Point(pictureBox1.Width / 2, pictureBox1.Height / 2);

//                        UpdateModeDisplay();
//                        pictureBox1.Invalidate();
//                    }
//                    catch (Exception ex)
//                    {
//                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
//                            MessageBoxButtons.OK, MessageBoxIcon.Error);
//                    }
//                }
//            }
//        }
//        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
//        {
//            if (e.Button != MouseButtons.Left || pictureBox1.Image == null) return;



//            // ✅ Delete mode
//            if (deleteMode)
//            {
//                int indexToRemove = FindMeasurementAtPoint(e.Location);
//                if (indexToRemove >= 0)
//                {
//                    string targetName = measurements[indexToRemove].Name;
//                    measurements.RemoveAt(indexToRemove);

//                    // 🧠 Find and remove from ListBox by name
//                    for (int i = 0; i < lstMeasurements.Items.Count; i++)
//                    {
//                        if (lstMeasurements.Items[i].ToString().StartsWith(targetName))
//                        {
//                            lstMeasurements.Items.RemoveAt(i);
//                            break;
//                        }
//                    }

//                    // 🧠 Also remove from highlight if needed
//                    int highlightIndex = FindHighlightedAngleAtPoint(e.Location);
//                    if (highlightIndex >= 0)
//                    {
//                        highlightedAngles.RemoveAt(highlightIndex);
//                    }

//                    pictureBox1.Invalidate();
//                }

//                return;
//            }
//            // ✅ DRAW POINT MODE
//            if (currentMode == MeasurementMode.OnePoint)
//            {
//                measurements.Add(new Measurement(
//                    e.Location,
//                    e.Location, // same start/end
//                    $"P{measurementCounter++}",
//                    MeasurementType.OnePoint));

//                lstMeasurements.Items.Add($"Point at ({e.X}, {e.Y})");
//                pictureBox1.Invalidate();
//                return;
//            }


//            // ✅ 1) If clicking on the plan center, ignore normal measurement click
//            double dist = Math.Sqrt(Math.Pow(e.X - planCenter.X, 2) + Math.Pow(e.Y - planCenter.Y, 2));
//            if (dist <= planGrabRadius)
//            {
//                // Plan drag click → do NOT create a point
//                return;
//            }

//            // ✅ 2) Normal behavior for placing points
//            if (currentStartPoint == null)
//            {
//                currentStartPoint = e.Location;
//                toolStripStatusLabel1.Text = currentMode == MeasurementMode.Distance
//                    ? "Click to place end point for distance measurement"
//                    : "Click to place second point of first segment";
//            }
//            else
//            {
//                if (angleWithPlanMode)
//                {
//                    if (currentStartPoint == null)
//                    {
//                        currentStartPoint = e.Location;
//                    }
//                    else
//                    {
//                        // Complete the line
//                        Point start = currentStartPoint.Value;
//                        Point end = e.Location;

//                        // Ask which axis to compare
//                        var result = MessageBox.Show("Use X-axis? (No = Y-axis)",
//                            "Choose Axis", MessageBoxButtons.YesNo);
//                        bool useXAxis = (result == DialogResult.Yes);

//                        double angle = CalculateAngleWithPlan(start, end, useXAxis);

//                        //////////////////

//                        // Store for drawing later
//                        //lastAngleVertex = start;
//                        //lastAngleAxis = useXAxis ? AxisType.X : AxisType.Y;
//                        //lastLineAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);
//                        double refAngle = (useXAxis ? 0 : Math.PI / 2);
//                        double lineAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);

//                        // ✅ Store all highlights
//                        highlightedAngles.Add((start, refAngle, lineAngle, useXAxis ? AxisType.X : AxisType.Y));


//                        /////////////////

//                        // ✅ Store as AngleWithPlan directly
//                        measurements.Add(new Measurement(start, end, $"P{measurementCounter++}", MeasurementType.AngleWithPlan));

//                        lstMeasurements.Items.Add($"P{measurementCounter++} Angle vs {(useXAxis ? "X-axis" : "Y-axis")}: {angle:F1}°");

//                        currentStartPoint = null;
//                      //  angleWithPlanMode = false;
//                        pictureBox1.Invalidate();
//                    }
//                    return; // ✅ Stop here to avoid adding a Distance line
//                }


//                if (currentMode == MeasurementMode.Distance)
//                {
//                    measurements.Add(new Measurement(
//                        currentStartPoint.Value,
//                        e.Location,
//                        $"D{measurementCounter++}",
//                        MeasurementType.Distance));

//                    currentStartPoint = null;
//                    CalculateMeasurements();
//                }
//                else
//                {
//                    if (measurements.Count > 0 && measurements[measurements.Count - 1].Type == MeasurementType.AngleSegment1)
//                    {
//                        measurements.Add(new Measurement(
//                            measurements[measurements.Count - 1].End,
//                            e.Location,
//                            $"A{measurementCounter++}",
//                            MeasurementType.AngleSegment2));

//                        CalculateMeasurements();
//                        currentStartPoint = null;
//                    }
//                    else
//                    {
//                        measurements.Add(new Measurement(
//                            currentStartPoint.Value,
//                            e.Location,
//                            $"A{measurementCounter++}",
//                            MeasurementType.AngleSegment1));

//                        currentStartPoint = e.Location;
//                        toolStripStatusLabel1.Text = "Click to place end point of second segment";
//                    }
//                }
//            }

//            pictureBox1.Invalidate();
//        }

//        private void CalculateMeasurements()
//        {
//            lstMeasurements.Items.Clear();

//            for (int i = 0; i < measurements.Count; i++)
//            {
//                var m = measurements[i];

//                if (m.Type == MeasurementType.Distance)
//                {
//                    double pixels = CalculatePixelDistance(m.Start, m.End);
//                    string itemText = $"{m.Name}: {pixels:F0} px";

//                    if (isReferenceSet)
//                    {
//                        double mm = CalculateRealDistance(m.Start, m.End);
//                        itemText += $" ({mm:F1} mm)";
//                    }

//                    lstMeasurements.Items.Add(itemText);
//                }
//                else if (m.Type == MeasurementType.AngleSegment2)
//                {
//                    // Look for a previous AngleSegment1
//                    if (i > 0 && measurements[i - 1].Type == MeasurementType.AngleSegment1)
//                    {
//                        var seg1 = measurements[i - 1];
//                        var seg2 = m;

//                        double angle = CalculateAngle(seg1.End, seg1.Start, seg2.End);
//                        lstMeasurements.Items.Add($"{m.Name}: {angle:F1}°");
//                    }
//                    else
//                    {
//                        lstMeasurements.Items.Add($"{m.Name}: Invalid angle (missing segment 1)");
//                    }
//                }
//            }
//        }

//        private void btnCalculate_Click(object sender, EventArgs e)
//        {
//            if (measurements.Count == 0)
//            {
//                toolStripStatusLabel1.Text = "No measurements to calculate";
//                return;
//            }

//            CalculateMeasurements();
//            pictureBox1.Invalidate();
//            toolStripStatusLabel1.Text = "Measurements calculated";
//        }

//        private double CalculateDistance(Point p1, Point p2)
//        {
//            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
//        }

//        private double CalculateAngle(Point vertex, Point p1, Point p2)
//        {
//            Vector2 v1 = new Vector2(p1.X - vertex.X, p1.Y - vertex.Y);
//            Vector2 v2 = new Vector2(p2.X - vertex.X, p2.Y - vertex.Y);

//            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
//            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
//            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

//            if (mag1 == 0 || mag2 == 0)
//                return 0;

//            double cosTheta = Math.Max(-1, Math.Min(1, dotProduct / (mag1 * mag2)));
//            return Math.Acos(cosTheta) * (180.0 / Math.PI);
//        }

//        private void pictureBox1_Paint(object sender, PaintEventArgs e)
//        {
//            // ✅ Draw all highlighted angles
//            foreach (var angleData in highlightedAngles)
//            {
//                DrawAngleHighlight(e.Graphics, angleData.vertex, angleData.refAngle, angleData.lineAngle);
//            }



//            if (pictureBox1.Image == null)
//            {
//                using (var font = new Font("Segoe UI", 10))
//                {
//                    e.Graphics.DrawString("Please import an image first",
//                        font, Brushes.White, new System.Drawing.PointF(10, 10));
//                }
//                return;
//            }


//            // === ✅ Draw XY Plan ===
//            if (showPlan)
//            {
//                using (Pen axisPen = new Pen(Color.Red, 1.5f))
//                {
//                    axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

//                    // Horizontal line (X-axis)
//                    e.Graphics.DrawLine(axisPen, 0, planCenter.Y, pictureBox1.Width, planCenter.Y);

//                    // Vertical line (Y-axis)
//                    e.Graphics.DrawLine(axisPen, planCenter.X, 0, planCenter.X, pictureBox1.Height);
//                }

//                // Draw draggable center point
//                using (Brush grabBrush = new SolidBrush(Color.Yellow))
//                {
//                    e.Graphics.FillEllipse(grabBrush, planCenter.X - 5, planCenter.Y - 5, 10, 10);
//                }
//            }


//            // Draw all measurements
//            foreach (var m in measurements)
//            {
//                Color lineColor = m.Type == MeasurementType.Distance ? Color.LimeGreen :
//                                 m.Type == MeasurementType.AngleSegment1 ? Color.Cyan : Color.Black;

//                using (var pen = new Pen(lineColor, 2f))
//                {
//                    e.Graphics.DrawLine(pen, m.Start, m.End);
//                }

//                // Draw points
//                e.Graphics.FillEllipse(Brushes.Red, m.Start.X - 3, m.Start.Y - 3, 6, 6);
//                e.Graphics.FillEllipse(Brushes.Red, m.End.X - 3, m.End.Y - 3, 6, 6);

//                // Calculate midpoint for label
//                Point midPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
//                if (m.Type == MeasurementType.OnePoint)
//                {
//                    // Draw just one point in blue
//                    e.Graphics.FillEllipse(Brushes.Blue, m.Start.X - 4, m.Start.Y - 4, 8, 8);

//                    // Label next to it
//                    using (var font = new Font("Segoe UI", 9))
//                    using (var textBrush = new SolidBrush(Color.White))
//                    {
//                        e.Graphics.DrawString(m.Name, font, textBrush, m.Start.X + 8, m.Start.Y - 8);
//                    }

//                    continue; // Skip line/angle drawing
//                }
//                // Prepare measurement text
//                string measurementText = m.Name;
//                if (m.Type == MeasurementType.Distance)
//                {
//                    double pixels = CalculatePixelDistance(m.Start, m.End);
//                   // measurementText += $": {pixels:F0} px";

//                    if (isReferenceSet)
//                    {
//                        double mm = CalculateRealDistance(m.Start, m.End);
//                        measurementText += $" {mm:F1} mm"; // Nouvelle ligne pour les mm
//                    }
//                }

//                // Draw label slightly offset from the line (not overlapping or under)
//                using (var font = new Font("Segoe UI", 9, FontStyle.Regular))
//                using (var bgBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
//                using (var textBrush = new SolidBrush(Color.White))
//                {
//                    if (string.IsNullOrWhiteSpace(measurementText))
//                        return;

//                    SizeF textSize = e.Graphics.MeasureString(measurementText, font);

//                    // Midpoint of the line
//                    float midX = (m.Start.X + m.End.X) / 2f;
//                    float midY = (m.Start.Y + m.End.Y) / 2f;

//                    // Offset direction: perpendicular to the line
//                    double angle = Math.Atan2(m.End.Y - m.Start.Y, m.End.X - m.Start.X);
//                    float offsetX = (float)(-Math.Sin(angle) * 15); // 15 px away perpendicular
//                    float offsetY = (float)(Math.Cos(angle) * 15);

//                    // Final position: a bit away from the line
//                    float labelX = midX + offsetX - textSize.Width / 2;
//                    float labelY = midY + offsetY - textSize.Height / 2;

//                    //RectangleF textBg = new RectangleF(
//                    //    labelX - 3,
//                    //    labelY - 2,
//                    //    textSize.Width + 6,
//                    //    textSize.Height + 4);

//                    //e.Graphics.FillRectangle(bgBrush, textBg);
//                    e.Graphics.DrawString(measurementText, font, textBrush, labelX, labelY);
//                }

//            }

//            // Draw current measurement in progress
//            if (currentStartPoint != null)
//            {
//                Point currentPos = pictureBox1.PointToClient(Cursor.Position);
//                using (var tempPen = new Pen(Color.Yellow, 1.5f))
//                {
//                    e.Graphics.DrawLine(tempPen, currentStartPoint.Value, currentPos);
//                }
//                e.Graphics.FillEllipse(Brushes.Red, currentStartPoint.Value.X - 3, currentStartPoint.Value.Y - 3, 6, 6);
//            }
//        }

//        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
//        {
//            if (showPlan && e.Button == MouseButtons.Left)
//            {
//                double dist = Math.Sqrt(Math.Pow(e.X - planCenter.X, 2) + Math.Pow(e.Y - planCenter.Y, 2));
//                if (dist <= planGrabRadius)
//                {
//                    isDraggingPlan = true;
//                    Cursor = Cursors.Hand;
//                }
//            }
//        }

//        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
//        {
//            if (isDraggingPlan)
//            {
//                planCenter = e.Location;
//                pictureBox1.Invalidate();
//            }
//        }

//        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
//        {
//            if (isDraggingPlan)
//            {
//                isDraggingPlan = false;
//                Cursor = Cursors.Default;
//            }
//        }


//        private void btnClear_Click(object sender, EventArgs e)
//        {
//            measurements.Clear();
//            lstMeasurements.Items.Clear();
//            currentStartPoint = null;
//            measurementCounter = 1;
//            //
//            // ✅ Reset highlighted angles
//            highlightedAngles.Clear();


//            //
//            toolStripStatusLabel1.Text = "Ready for new measurements";
//            pictureBox1.Invalidate();
//        }

//        private void btnSwitchMode_Click(object sender, EventArgs e)
//        {
//            currentMode = currentMode == MeasurementMode.Distance
//                ? MeasurementMode.Angle
//                : MeasurementMode.Distance;

//            currentStartPoint = null;
//            UpdateModeDisplay();
//        }

//        // Helper classes
//        private enum MeasurementType { Distance, AngleSegment1, AngleSegment2, AngleWithPlan , OnePoint }

//        private struct Measurement
//        {
//            public Point Start { get; }
//            public Point End { get; }
//            public string Name { get; }
//            public MeasurementType Type { get; }

//            public Measurement(Point start, Point end, string name, MeasurementType type)
//            {
//                Start = start;
//                End = end;
//                Name = name;
//                Type = type;
//            }
//        }

//        private struct Vector2
//        {
//            public double X { get; }
//            public double Y { get; }

//            public Vector2(double x, double y)
//            {
//                X = x;
//                Y = y;
//            }
//        }

//        private void BodyPictureAnalyzer_Load(object sender, EventArgs e)
//        {

//        }

//        private void panelTop_Paint(object sender, PaintEventArgs e)
//        {

//        }



//        private void btnTogglePlan_Click(object sender, EventArgs e)
//        {
//            showPlan = !showPlan;
//            pictureBox1.Invalidate(); // Refresh image
//        }


//        private bool angleWithPlanMode = false;

//        private void btnAngleWithPlan_Click(object sender, EventArgs e)
//        {
//            currentMode = MeasurementMode.Distance; // we draw a single line
//            angleWithPlanMode = !angleWithPlanMode;
//            toolStripStatusLabel1.Text = "Draw a line, then select axis (X or Y)";
//        }
//        private void btnOnePointMode_Click(object sender, EventArgs e)
//        {
//            currentMode = MeasurementMode.OnePoint; // we draw a single line

//            toolStripStatusLabel1.Text = "Draw a point ";
//        }

//        private void DrawAngleHighlight(Graphics g, Point vertex, double refAngle, double lineAngle)
//        {
//            // Compute start and sweep angles (degrees)
//            float startAngle = (float)(Math.Min(refAngle, lineAngle) * 180 / Math.PI);
//            float sweepAngle = (float)(Math.Abs(lineAngle - refAngle) * 180 / Math.PI);

//            // Draw semi-transparent arc
//            using (Brush semiBrush = new SolidBrush(Color.FromArgb(80, Color.LightBlue)))
//            {
//                float radius = 50; // size of the arc highlight
//                g.FillPie(semiBrush,
//                    vertex.X - radius,
//                    vertex.Y - radius,
//                    radius * 2,
//                    radius * 2,
//                    startAngle,
//                    sweepAngle);
//            }
//        }

//        private enum AxisType
//        {
//            X,
//            Y
//        }

//        private void btnDeleteMode_Click(object sender, EventArgs e)
//        {
//            deleteMode = !deleteMode;
//            btnDeleteMode.Text = deleteMode ? "Delete: ON" : "Delete: OFF";
//            Cursor = deleteMode ? Cursors.Cross : Cursors.Default;
//        }

//        private int FindMeasurementAtPoint(Point clickPoint)
//        {
//            for (int i = 0; i < measurements.Count; i++)
//            {
//                if (IsPointNearLine(clickPoint, measurements[i].Start, measurements[i].End))
//                    return i;
//            }
//            return -1;
//        }

//        private int FindHighlightedAngleAtPoint(Point clickPoint)
//        {
//            for (int i = 0; i < highlightedAngles.Count; i++)
//            {
//                Point vertex = highlightedAngles[i].vertex;
//                if (Math.Sqrt(Math.Pow(clickPoint.X - vertex.X, 2) + Math.Pow(clickPoint.Y - vertex.Y, 2)) <= 8)
//                    return i;
//            }
//            return -1;
//        }

//        private bool IsPointNearLine(Point p, Point a, Point b)
//        {
//            double dx = b.X - a.X;
//            double dy = b.Y - a.Y;

//            if (dx == 0 && dy == 0)
//                return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2)) < lineClickThreshold;

//            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
//            t = Math.Max(0, Math.Min(1, t));

//            double projX = a.X + t * dx;
//            double projY = a.Y + t * dy;

//            double distance = Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
//            return distance <= lineClickThreshold;
//        }


//        private void DetectCobbAngleFromStickers(Bitmap bitmap)
//    {
//        if (bitmap == null)
//        {
//            MessageBox.Show("Aucune image chargée.");
//            return;
//        }

//        // 1. Liste pour stocker les points rouges détectés
//        List<Point> redPoints = new List<Point>();

//        // 2. Scanner l’image pixel par pixel
//        for (int y = 0; y < bitmap.Height; y += 2) // +2 pour aller plus vite
//        {
//            for (int x = 0; x < bitmap.Width; x += 2)
//            {
//                Color pixel = bitmap.GetPixel(x, y);

//                // 3. Détection de rouge : intensité R élevée, G et B faibles
//                //if (pixel.R > 180 && pixel.G < 80 && pixel.B < 80)
//                //{
//                //    redPoints.Add(new Point(x, y));
//                //}
//                    if (pixel.R > 220 && pixel.G > 220 && pixel.B > 220)
//                    {
//                        redPoints.Add(new Point(x, y));
//                    }

//                }
//            }

//        if (redPoints.Count < 50)
//        {
//            MessageBox.Show("Pas assez de points rouges détectés.");
//            return;
//        }

//        // 4. Grouper les points en 3 clusters (k-means simple)
//        var clusteredPoints = redPoints
//            .OrderBy(p => p.Y) // du haut vers le bas (axe Y)
//            .GroupBy(p => redPoints.IndexOf(p) * 3 / redPoints.Count) // 3 groupes approximés
//            .Select(g => new Point(
//                (int)g.Average(p => p.X),
//                (int)g.Average(p => p.Y)
//            ))
//            .ToList();

//        if (clusteredPoints.Count < 3)
//        {
//            MessageBox.Show("Impossible de trouver 3 clusters rouges.");
//            return;
//        }

//        // 5. Trier les points du haut vers le bas (vertèbres sup -> inf)
//        clusteredPoints = clusteredPoints.OrderBy(p => p.Y).ToList();

//        Point upper = clusteredPoints[0];
//        Point middle = clusteredPoints[1];
//        Point lower = clusteredPoints[2];

//        // 6. Calcul de l’angle de Cobb (angle entre 2 lignes)
//        double angle = CalculateAngle(middle, upper, lower); // vertex = milieu

//        MessageBox.Show($"Angle de Cobb estimé : {angle:F1}°", "Résultat");

//        // 7. Affichage visuel (optionnel mais utile)
//        using (Graphics g = Graphics.FromImage(bitmap))
//        {
//            using (Pen pen = new Pen(Color.Yellow, 3))
//            {
//                g.DrawLine(pen, upper, middle);
//                g.DrawLine(pen, middle, lower);
//            }

//            foreach (var pt in clusteredPoints)
//            {
//                g.FillEllipse(Brushes.Red, pt.X - 5, pt.Y - 5, 10, 10);
//            }
//        }

//        pictureBox1.Image = (Bitmap)bitmap.Clone(); // rafraîchir l’image avec lignes
//    }

//        private double CalculateAngleWithPlan(Point p1, Point p2, bool useXAxis)
//        {
//            // Convert line into vector
//            double dx = p2.X - p1.X;
//            double dy = p2.Y - p1.Y;

//            // Axis vector based on draggable plan
//            double ax, ay;
//            if (useXAxis)
//            {
//                ax = 1;
//                ay = 0;
//            }
//            else
//            {
//                ax = 0;
//                ay = 1;
//            }

//            // Dot product and magnitudes
//            double dot = dx * ax + dy * ay;
//            double mag1 = Math.Sqrt(dx * dx + dy * dy);
//            double mag2 = Math.Sqrt(ax * ax + ay * ay);

//            if (mag1 == 0 || mag2 == 0)
//                return 0;

//            double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
//            return Math.Acos(cosTheta) * (180.0 / Math.PI);
//        }


//    }
//}

//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.IO;
//using System.Linq;
//using System.Windows.Forms;

//namespace kinectProject
//{
//    public partial class BodyPictureAnalyzer : Form
//    {
//        // Enums
//        private enum ToolMode { None, Line, Point, Angle, Distance, Reference }
//        private enum EditMode { None, Move, Delete }
//        private enum AxisType { X, Y }

//        // Measurement structures
//        private struct Measurement
//        {
//            public Point Start;
//            public Point End;
//            public string Name;
//            public MeasurementType Type;
//            public bool IsSelected;
//            public AxisType? Axis; // For angle measurements

//            public Measurement(Point start, Point end, string name, MeasurementType type)
//            {
//                Start = start;
//                End = end;
//                Name = name;
//                Type = type;
//                IsSelected = false;
//                Axis = null;
//            }
//        }

//        private enum MeasurementType { Line, Point, Angle, Distance, ReferenceLine }

//        // Application state
//        private ToolMode currentTool = ToolMode.None;
//        private EditMode currentEditMode = EditMode.None;
//        private List<Measurement> measurements = new List<Measurement>();
//        private Image originalImage;
//        private Point? currentStartPoint = null;
//        private int measurementCounter = 1;
//        private float pixelToRealRatio = 1.0f;
//        private bool isReferenceSet = false;
//        private bool showGrid = true;
//        private Point gridOrigin;
//        private bool isDraggingGrid = false;
//        private const int gridGrabRadius = 10;
//        private Measurement? selectedMeasurement = null;
//        private int selectedMeasurementIndex = -1;
//        private bool isDraggingMeasurement = false;
//        private Point dragOffset;
//        private Point? angleVertex = null;
//        private bool isSettingReference = false;

//        // UI Controls
//        private PictureBox pictureBox;
//        private ToolStrip toolStrip;
//        private StatusStrip statusStrip;
//        private ListBox measurementsList;

//        public BodyPictureAnalyzer()
//        {
//          //  InitializeComponent();
//            SetupUI();
//            UpdateStatus("Ready to import an image");
//        }

//        private void SetupUI()
//        {
//            // Main form setup
//            this.Text = "Advanced Image Measurement Tool";
//            this.Size = new Size(1000, 700);
//            this.DoubleBuffered = true;

//            // Toolstrip setup
//            toolStrip = new ToolStrip();
//            toolStrip.Dock = DockStyle.Top;

//            // Toolstrip buttons
//            AddToolButton("Import Image", BtnImport_Click);
//            AddToolSeparator();

//            AddToolButton("Line Tool", (s, e) => SetToolMode(ToolMode.Line));
//            AddToolButton("Point Tool", (s, e) => SetToolMode(ToolMode.Point));
//            AddToolButton("Angle Tool", (s, e) => SetToolMode(ToolMode.Angle));
//            AddToolButton("Distance Tool", (s, e) => SetToolMode(ToolMode.Distance));
//            AddToolButton("Set Reference", (s, e) => SetToolMode(ToolMode.Reference));

//            AddToolSeparator();

//            AddToolButton("Move Mode", (s, e) => SetEditMode(EditMode.Move));
//            AddToolButton("Delete Mode", (s, e) => SetEditMode(EditMode.Delete));
//            AddToolButton("Clear All", BtnClear_Click);
//            AddToolButton("Toggle Grid", BtnToggleGrid_Click);

//            // Picture box setup
//            pictureBox = new PictureBox();
//            pictureBox.Dock = DockStyle.Fill;
//            pictureBox.BackColor = Color.DarkGray;
//            pictureBox.BorderStyle = BorderStyle.FixedSingle;
//            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
//            pictureBox.MouseClick += PictureBox_MouseClick;
//            pictureBox.MouseDown += PictureBox_MouseDown;
//            pictureBox.MouseMove += PictureBox_MouseMove;
//            pictureBox.MouseUp += PictureBox_MouseUp;
//            pictureBox.Paint += PictureBox_Paint;

//            // Measurements list
//            measurementsList = new ListBox();
//            measurementsList.Dock = DockStyle.Right;
//            measurementsList.Width = 250;
//            measurementsList.SelectedIndexChanged += MeasurementsList_SelectedIndexChanged;

//            // Status strip
//            statusStrip = new StatusStrip();
//            statusStrip.Dock = DockStyle.Bottom;

//            // Add controls to form
//            this.Controls.Add(pictureBox);
//            this.Controls.Add(measurementsList);
//            this.Controls.Add(toolStrip);
//            this.Controls.Add(statusStrip);
//        }

//        private void AddToolButton(string text, EventHandler handler)
//        {
//            var button = new ToolStripButton(text);
//            button.Click += handler;
//            toolStrip.Items.Add(button);
//        }

//        private void AddToolSeparator()
//        {
//            toolStrip.Items.Add(new ToolStripSeparator());
//        }

//        private void SetToolMode(ToolMode mode)
//        {
//            currentTool = mode;
//            currentEditMode = EditMode.None;
//            currentStartPoint = null;
//            angleVertex = null;

//            string statusText = "";
//            switch (mode)
//            {
//                case ToolMode.Line: statusText = "Line Tool: Click to place start and end points"; break;
//                case ToolMode.Point: statusText = "Point Tool: Click to place a point"; break;
//                case ToolMode.Angle: statusText = "Angle Tool: Click to place vertex, then end points"; break;
//                case ToolMode.Distance: statusText = "Distance Tool: Click to measure distance"; break;
//                case ToolMode.Reference: statusText = "Reference Tool: Draw a line of known length"; break;
//            }

//            UpdateStatus(statusText);
//            pictureBox.Cursor = Cursors.Cross;
//            DeselectAllMeasurements();
//        }

//        private void SetEditMode(EditMode mode)
//        {
//            currentEditMode = mode;
//            currentTool = ToolMode.None;
//            currentStartPoint = null;
//            angleVertex = null;

//            string statusText = mode == EditMode.Delete ?
//                "Delete Mode: Click on measurement to delete" :
//                "Move Mode: Click and drag to move measurement";

//            UpdateStatus(statusText);
//            pictureBox.Cursor = mode == EditMode.Delete ? Cursors.No : Cursors.Hand;
//            DeselectAllMeasurements();
//        }

//        private void UpdateStatus(string message)
//        {
//            if (statusStrip.Items.Count == 0)
//                statusStrip.Items.Add(new ToolStripStatusLabel());

//            statusStrip.Items[0].Text = message;
//        }

//        private void BtnImport_Click(object sender, EventArgs e)
//        {
//            using (OpenFileDialog openFileDialog = new OpenFileDialog())
//            {
//                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*";
//                if (openFileDialog.ShowDialog() == DialogResult.OK)
//                {
//                    try
//                    {
//                        originalImage = Image.FromFile(openFileDialog.FileName);
//                        pictureBox.Image = (Image)originalImage.Clone();

//                        // Initialize grid at center
//                        gridOrigin = new Point(pictureBox.Width / 2, pictureBox.Height / 2);

//                        measurements.Clear();
//                        measurementsList.Items.Clear();
//                        measurementCounter = 1;
//                        isReferenceSet = false;
//                        pixelToRealRatio = 1.0f;
//                        isSettingReference = false;

//                        UpdateStatus("Image loaded. Select a measurement tool.");
//                        pictureBox.Invalidate();
//                    }
//                    catch (Exception ex)
//                    {
//                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
//                            MessageBoxButtons.OK, MessageBoxIcon.Error);
//                    }
//                }
//            }
//        }

//        private void BtnSetReference_Click(object sender, EventArgs e)
//        {
//            if (measurements.Count == 0)
//            {
//                MessageBox.Show("Please create a distance measurement first to use as reference.");
//                return;
//            }

//            // Find the last distance measurement
//            var lastDistance = measurements.LastOrDefault(m => m.Type == MeasurementType.Distance);
//            if (lastDistance.Type != MeasurementType.Distance)
//            {
//                MessageBox.Show("Please create a distance measurement to use as reference.");
//                return;
//            }

//            using (var inputDialog = new ReferenceInputDialog())
//            {
//                if (inputDialog.ShowDialog() == DialogResult.OK)
//                {
//                    float referenceLength = inputDialog.ReferenceLength;
//                    SetScaleFromReference(lastDistance, referenceLength);
//                    UpdateStatus($"Reference set: 1 unit = {pixelToRealRatio:F2} pixels");
//                    UpdateMeasurementsList();
//                    pictureBox.Invalidate();
//                }
//            }
//        }

//        private void SetScaleFromReference(Measurement reference, float referenceLength)
//        {
//            double pixelLength = CalculateDistance(reference.Start, reference.End);
//            if (referenceLength > 0 && pixelLength > 0)
//            {
//                pixelToRealRatio = (float)(pixelLength / referenceLength);
//                isReferenceSet = true;

//                // Change reference measurement type
//                for (int i = 0; i < measurements.Count; i++)
//                {
//                    if (measurements[i].Name == reference.Name)
//                    {
//                        Measurement m = measurements[i];
//                        m.Type = MeasurementType.ReferenceLine;
//                        measurements[i] = m;
//                        break;
//                    }
//                }
//            }
//        }

//        private void BtnClear_Click(object sender, EventArgs e)
//        {
//            measurements.Clear();
//            measurementsList.Items.Clear();
//            measurementCounter = 1;
//            currentStartPoint = null;
//            angleVertex = null;
//            isReferenceSet = false;
//            pixelToRealRatio = 1.0f;
//            isSettingReference = false;
//            UpdateStatus("All measurements cleared.");
//            pictureBox.Invalidate();
//        }

//        private void BtnToggleGrid_Click(object sender, EventArgs e)
//        {
//            showGrid = !showGrid;
//            pictureBox.Invalidate();
//        }

//        private void MeasurementsList_SelectedIndexChanged(object sender, EventArgs e)
//        {
//            DeselectAllMeasurements();

//            if (measurementsList.SelectedIndex >= 0 && measurementsList.SelectedIndex < measurements.Count)
//            {
//                Measurement m = measurements[measurementsList.SelectedIndex];
//                m.IsSelected = true;
//                measurements[measurementsList.SelectedIndex] = m;
//                selectedMeasurementIndex = measurementsList.SelectedIndex;
//                selectedMeasurement = m;
//            }

//            pictureBox.Invalidate();
//        }

//        private void PictureBox_MouseClick(object sender, MouseEventArgs e)
//        {
//            if (pictureBox.Image == null) return;

//            // Handle grid dragging
//            if (e.Button == MouseButtons.Left && IsNearPoint(e.Location, gridOrigin, gridGrabRadius))
//            {
//                gridOrigin = e.Location;
//                pictureBox.Invalidate();
//                return;
//            }

//            // Handle measurement creation
//            if (currentTool != ToolMode.None && e.Button == MouseButtons.Left)
//            {
//                HandleMeasurementCreation(e.Location);
//            }

//            // Handle selection for moving or deleting
//            if (currentEditMode != EditMode.None && e.Button == MouseButtons.Left)
//            {
//                HandleSelection(e.Location);
//            }
//        }

//        private void HandleMeasurementCreation(Point location)
//        {
//            switch (currentTool)
//            {
//                case ToolMode.Line:
//                    if (currentStartPoint == null)
//                    {
//                        currentStartPoint = location;
//                        UpdateStatus("Click endpoint for line");
//                    }
//                    else
//                    {
//                        measurements.Add(new Measurement(
//                            currentStartPoint.Value,
//                            location,
//                            $"L{measurementCounter++}",
//                            MeasurementType.Line));
//                        currentStartPoint = null;
//                        UpdateMeasurementsList();
//                        pictureBox.Invalidate();
//                    }
//                    break;

//                case ToolMode.Point:
//                    measurements.Add(new Measurement(
//                        location,
//                        location,
//                        $"P{measurementCounter++}",
//                        MeasurementType.Point));
//                    UpdateMeasurementsList();
//                    pictureBox.Invalidate();
//                    break;

//                case ToolMode.Angle:
//                    if (angleVertex == null)
//                    {
//                        angleVertex = location;
//                        UpdateStatus("Click first angle point");
//                    }
//                    else if (currentStartPoint == null)
//                    {
//                        currentStartPoint = location;
//                        UpdateStatus("Click second angle point");
//                    }
//                    else
//                    {
//                        // Create angle measurement
//                        measurements.Add(new Measurement(
//                            angleVertex.Value,
//                            location,
//                            $"A{measurementCounter++}",
//                            MeasurementType.Angle));

//                        // Ask for axis reference
//                        var axisDialog = new AxisSelectionDialog();
//                        if (axisDialog.ShowDialog() == DialogResult.OK)
//                        {
//                            // Update measurement with axis info
//                            Measurement m = measurements[measurements.Count - 1];
//                            m.Axis = axisDialog.SelectedAxis;
//                            measurements[measurements.Count - 1] = m;
//                        }

//                        angleVertex = null;
//                        currentStartPoint = null;
//                        UpdateMeasurementsList();
//                        pictureBox.Invalidate();
//                    }
//                    break;

//                case ToolMode.Distance:
//                    if (currentStartPoint == null)
//                    {
//                        currentStartPoint = location;
//                        UpdateStatus("Click endpoint for distance measurement");
//                    }
//                    else
//                    {
//                        measurements.Add(new Measurement(
//                            currentStartPoint.Value,
//                            location,
//                            $"D{measurementCounter++}",
//                            MeasurementType.Distance));
//                        currentStartPoint = null;
//                        UpdateMeasurementsList();
//                        pictureBox.Invalidate();
//                    }
//                    break;

//                case ToolMode.Reference:
//                    if (currentStartPoint == null)
//                    {
//                        currentStartPoint = location;
//                        UpdateStatus("Click endpoint for reference line");
//                    }
//                    else
//                    {
//                        measurements.Add(new Measurement(
//                            currentStartPoint.Value,
//                            location,
//                            $"R{measurementCounter++}",
//                            MeasurementType.Distance));
//                        currentStartPoint = null;
//                        isSettingReference = true;
//                        UpdateMeasurementsList();
//                        pictureBox.Invalidate();

//                        // Prompt for reference value
//                        using (var inputDialog = new ReferenceInputDialog())
//                        {
//                            if (inputDialog.ShowDialog() == DialogResult.OK)
//                            {
//                                float referenceLength = inputDialog.ReferenceLength;
//                                SetScaleFromReference(measurements[measurements.Count - 1], referenceLength);
//                                UpdateStatus($"Reference set: 1 unit = {pixelToRealRatio:F2} pixels");
//                                UpdateMeasurementsList();
//                            }
//                        }

//                        isSettingReference = false;
//                    }
//                    break;
//            }
//        }

//        private void HandleSelection(Point location)
//        {
//            int index = FindMeasurementAtPoint(location);

//            if (index >= 0)
//            {
//                if (currentEditMode == EditMode.Delete)
//                {
//                    measurements.RemoveAt(index);
//                    UpdateMeasurementsList();
//                    pictureBox.Invalidate();
//                    UpdateStatus("Measurement deleted");
//                }
//                else if (currentEditMode == EditMode.Move)
//                {
//                    selectedMeasurementIndex = index;
//                    selectedMeasurement = measurements[index];

//                    // Calculate offset for smooth dragging
//                    if (selectedMeasurement.Value.Type == MeasurementType.Point)
//                    {
//                        dragOffset = new Point(
//                            location.X - selectedMeasurement.Value.Start.X,
//                            location.Y - selectedMeasurement.Value.Start.Y);
//                    }
//                    else
//                    {
//                        // For lines, calculate offset from midpoint
//                        Point midPoint = new Point(
//                            (selectedMeasurement.Value.Start.X + selectedMeasurement.Value.End.X) / 2,
//                            (selectedMeasurement.Value.Start.Y + selectedMeasurement.Value.End.Y) / 2);
//                        dragOffset = new Point(
//                            location.X - midPoint.X,
//                            location.Y - midPoint.Y);
//                    }

//                    isDraggingMeasurement = true;
//                    pictureBox.Invalidate();
//                }
//            }
//        }

//        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
//        {
//            if (e.Button == MouseButtons.Left && IsNearPoint(e.Location, gridOrigin, gridGrabRadius))
//            {
//                isDraggingGrid = true;
//            }
//        }

//        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
//        {
//            if (isDraggingGrid)
//            {
//                gridOrigin = e.Location;
//                pictureBox.Invalidate();
//            }

//            if (isDraggingMeasurement && selectedMeasurement.HasValue && selectedMeasurementIndex >= 0)
//            {
//                MoveMeasurement(selectedMeasurementIndex, e.Location);
//                pictureBox.Invalidate();
//            }
//            else if (currentTool != ToolMode.None && currentStartPoint.HasValue)
//            {
//                // Show preview of current measurement
//                pictureBox.Invalidate();
//            }
//        }

//        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
//        {
//            isDraggingGrid = false;
//            isDraggingMeasurement = false;
//        }

//        private void MoveMeasurement(int index, Point mouseLocation)
//        {
//            Measurement m = measurements[index];

//            if (m.Type == MeasurementType.Point)
//            {
//                // Move point to new location (adjusting for offset)
//                Point newLocation = new Point(
//                    mouseLocation.X - dragOffset.X,
//                    mouseLocation.Y - dragOffset.Y);

//                m.Start = newLocation;
//                m.End = newLocation;
//            }
//            else
//            {
//                // Calculate movement delta
//                Point midPoint = new Point(
//                    (m.Start.X + m.End.X) / 2,
//                    (m.Start.Y + m.End.Y) / 2);

//                int deltaX = mouseLocation.X - midPoint.X - dragOffset.X;
//                int deltaY = mouseLocation.Y - midPoint.Y - dragOffset.Y;

//                // Move both endpoints
//                m.Start = new Point(m.Start.X + deltaX, m.Start.Y + deltaY);
//                m.End = new Point(m.End.X + deltaX, m.End.Y + deltaY);
//            }

//            measurements[index] = m;
//            UpdateMeasurementsList();
//        }

//        private int FindMeasurementAtPoint(Point point)
//        {
//            for (int i = 0; i < measurements.Count; i++)
//            {
//                if (IsMeasurementAtPoint(measurements[i], point))
//                    return i;
//            }
//            return -1;
//        }

//        private bool IsMeasurementAtPoint(Measurement m, Point point)
//        {
//            const int tolerance = 5;

//            switch (m.Type)
//            {
//                case MeasurementType.Point:
//                    return IsNearPoint(point, m.Start, tolerance);

//                case MeasurementType.Line:
//                case MeasurementType.Distance:
//                case MeasurementType.ReferenceLine:
//                case MeasurementType.Angle:
//                    return IsPointNearLine(point, m.Start, m.End, tolerance);

//                default:
//                    return false;
//            }
//        }

//        private bool IsNearPoint(Point p1, Point p2, int tolerance)
//        {
//            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)) <= tolerance;
//        }

//        private bool IsPointNearLine(Point point, Point lineStart, Point lineEnd, int tolerance)
//        {
//            // Calculate distance from point to line segment
//            double lineLength = CalculateDistance(lineStart, lineEnd);
//            if (lineLength == 0) return IsNearPoint(point, lineStart, tolerance);

//            // Calculate projection point
//            double t = Math.Max(0, Math.Min(1,
//                ((point.X - lineStart.X) * (lineEnd.X - lineStart.X) +
//                 (point.Y - lineStart.Y) * (lineEnd.Y - lineStart.Y)) /
//                (lineLength * lineLength)));

//            Point projection = new Point(
//                (int)(lineStart.X + t * (lineEnd.X - lineStart.X)),
//                (int)(lineStart.Y + t * (lineEnd.Y - lineStart.Y)));

//            return IsNearPoint(point, projection, tolerance);
//        }

//        private void DeselectAllMeasurements()
//        {
//            for (int i = 0; i < measurements.Count; i++)
//            {
//                Measurement m = measurements[i];
//                m.IsSelected = false;
//                measurements[i] = m;
//            }
//            selectedMeasurement = null;
//            selectedMeasurementIndex = -1;
//            measurementsList.ClearSelected();
//        }

//        private void UpdateMeasurementsList()
//        {
//            measurementsList.Items.Clear();

//            foreach (var m in measurements)
//            {
//                string itemText = $"{m.Name}: ";

//                switch (m.Type)
//                {
//                    case MeasurementType.Line:
//                        double lineLength = CalculateDistance(m.Start, m.End);
//                        itemText += $"{lineLength:F1} px";
//                        break;

//                    case MeasurementType.Distance:
//                        double pixels = CalculateDistance(m.Start, m.End);
//                        itemText += $"{pixels:F1} px";

//                        if (isReferenceSet)
//                        {
//                            double realUnits = pixels / pixelToRealRatio;
//                            itemText += $" ({realUnits:F2} units)";
//                        }
//                        break;

//                    case MeasurementType.ReferenceLine:
//                        double refPixels = CalculateDistance(m.Start, m.End);
//                        double refUnits = refPixels / pixelToRealRatio;
//                        itemText += $"{refPixels:F1} px ({refUnits:F2} units) [Reference]";
//                        break;

//                    case MeasurementType.Angle:
//                        double angle = CalculateAngle(m);
//                        itemText += $"{angle:F1}°";
//                        if (m.Axis.HasValue)
//                            itemText += $" relative to {m.Axis.Value}-axis";
//                        break;

//                    case MeasurementType.Point:
//                        itemText += $"Point at ({m.Start.X}, {m.Start.Y})";
//                        break;
//                }

//                if (m.IsSelected) itemText += " [Selected]";
//                measurementsList.Items.Add(itemText);
//            }
//        }

//        private double CalculateDistance(Point p1, Point p2)
//        {
//            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
//        }

//        private double CalculateAngle(Measurement m)
//        {
//            if (m.Type != MeasurementType.Angle) return 0;

//            if (m.Axis.HasValue)
//            {
//                // Calculate angle relative to specified axis
//                double dx = m.End.X - m.Start.X;
//                double dy = m.End.Y - m.Start.Y;

//                if (m.Axis == AxisType.X)
//                    return Math.Abs(Math.Atan2(dy, dx) * (180 / Math.PI));
//                else
//                    return Math.Abs(Math.Atan2(dx, dy) * (180 / Math.PI));
//            }
//            else
//            {
//                // Calculate angle between two segments (if we had vertex and two points)
//                // Simplified implementation
//                double dx = m.End.X - m.Start.X;
//                double dy = m.End.Y - m.Start.Y;
//                return Math.Atan2(dy, dx) * (180 / Math.PI);
//            }
//        }

//        private void PictureBox_Paint(object sender, PaintEventArgs e)
//        {
//            if (pictureBox.Image == null) return;

//            Graphics g = e.Graphics;
//            g.SmoothingMode = SmoothingMode.AntiAlias;

//            // Draw grid if enabled
//            if (showGrid)
//            {
//                DrawGrid(g);
//            }

//            // Draw measurements
//            foreach (var m in measurements)
//            {
//                DrawMeasurement(g, m);
//            }

//            // Draw current measurement in progress
//            if (currentStartPoint.HasValue && currentTool != ToolMode.None)
//            {
//                Point currentPos = pictureBox.PointToClient(Cursor.Position);

//                using (Pen tempPen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash })
//                {
//                    if (currentTool == ToolMode.Angle && angleVertex.HasValue)
//                    {
//                        // Draw both segments for angle
//                        g.DrawLine(tempPen, angleVertex.Value, currentStartPoint.Value);
//                        g.DrawLine(tempPen, angleVertex.Value, currentPos);
//                    }
//                    else
//                    {
//                        g.DrawLine(tempPen, currentStartPoint.Value, currentPos);
//                    }
//                }

//                // Draw helper for 90° angles
//                if (currentTool == ToolMode.Line || currentTool == ToolMode.Distance)
//                {
//                    DrawAngleHelpers(g, currentStartPoint.Value, currentPos);
//                }
//            }
//        }

//        private void DrawGrid(Graphics g)
//        {
//            using (Pen gridPen = new Pen(Color.FromArgb(100, Color.LightBlue)))
//            using (Pen axisPen = new Pen(Color.Red, 1.5f))
//            {
//                gridPen.DashStyle = DashStyle.Dot;

//                // Draw vertical grid lines
//                for (int x = gridOrigin.X % 50; x < pictureBox.Width; x += 50)
//                {
//                    g.DrawLine(gridPen, x, 0, x, pictureBox.Height);
//                }

//                // Draw horizontal grid lines
//                for (int y = gridOrigin.Y % 50; y < pictureBox.Height; y += 50)
//                {
//                    g.DrawLine(gridPen, 0, y, pictureBox.Width, y);
//                }

//                // Draw axes
//                g.DrawLine(axisPen, gridOrigin.X, 0, gridOrigin.X, pictureBox.Height); // Y-axis
//                g.DrawLine(axisPen, 0, gridOrigin.Y, pictureBox.Width, gridOrigin.Y);   // X-axis

//                // Draw grid origin point
//                g.FillEllipse(Brushes.Red, gridOrigin.X - 5, gridOrigin.Y - 5, 10, 10);
//            }
//        }

//        private void DrawAngleHelpers(Graphics g, Point start, Point end)
//        {
//            // Calculate potential perpendicular endpoints for 90° assistance
//            int dx = end.X - start.X;
//            int dy = end.Y - start.Y;

//            // Horizontal helper
//            Point horizontalEnd = new Point(end.X, start.Y);
//            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Green)) { DashStyle = DashStyle.Dot })
//            {
//                g.DrawLine(helperPen, start, horizontalEnd);
//            }

//            // Vertical helper
//            Point verticalEnd = new Point(start.X, end.Y);
//            using (Pen helperPen = new Pen(Color.FromArgb(100, Color.Blue)) { DashStyle = DashStyle.Dot })
//            {
//                g.DrawLine(helperPen, start, verticalEnd);
//            }

//            // Show angle information
//            double angle = Math.Atan2(dy, dx) * (180 / Math.PI);
//            using (Font font = new Font("Arial", 9))
//            using (Brush brush = new SolidBrush(Color.White))
//            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
//            {
//                string angleText = $"{angle:F1}°";
//                SizeF textSize = g.MeasureString(angleText, font);
//                Point midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

//                RectangleF textRect = new RectangleF(
//                    midPoint.X - textSize.Width / 2,
//                    midPoint.Y - textSize.Height - 5,
//                    textSize.Width + 4,
//                    textSize.Height);

//                g.FillRectangle(bgBrush, textRect);
//                g.DrawString(angleText, font, brush, midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 3);
//            }
//        }

//        private void DrawMeasurement(Graphics g, Measurement m)
//        {
//            Color color = m.IsSelected ? Color.Yellow : GetMeasurementColor(m.Type);

//            using (Pen pen = new Pen(color, 2))
//            using (Brush brush = new SolidBrush(color))
//            using (Font font = new Font("Arial", 9))
//            using (Brush textBrush = new SolidBrush(Color.White))
//            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
//            {
//                switch (m.Type)
//                {
//                    case MeasurementType.Point:
//                        g.FillEllipse(brush, m.Start.X - 4, m.Start.Y - 4, 8, 8);

//                        // Draw label
//                        string label = $"{m.Name} ({m.Start.X}, {m.Start.Y})";
//                        SizeF textSize = g.MeasureString(label, font);
//                        RectangleF textRect = new RectangleF(
//                            m.Start.X + 10, m.Start.Y - textSize.Height / 2,
//                            textSize.Width + 4, textSize.Height);
//                        g.FillRectangle(bgBrush, textRect);
//                        g.DrawString(label, font, textBrush, m.Start.X + 12, m.Start.Y - textSize.Height / 2);
//                        break;

//                    case MeasurementType.Line:
//                        g.DrawLine(pen, m.Start, m.End);
//                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
//                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);
//                        break;

//                    case MeasurementType.Distance:
//                    case MeasurementType.ReferenceLine:
//                        g.DrawLine(pen, m.Start, m.End);
//                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
//                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);

//                        // Draw measurement value
//                        double distance = CalculateDistance(m.Start, m.End);
//                        string distText = m.Type == MeasurementType.ReferenceLine ?
//                            $"{distance / pixelToRealRatio:F1} units" :
//                            isReferenceSet ?
//                                $"{distance / pixelToRealRatio:F1} units" :
//                                $"{distance:F1} px";

//                        Point midPoint = new Point(
//                            (m.Start.X + m.End.X) / 2,
//                            (m.Start.Y + m.End.Y) / 2);

//                        textSize = g.MeasureString(distText, font);
//                        textRect = new RectangleF(
//                            midPoint.X - textSize.Width / 2, midPoint.Y - textSize.Height - 10,
//                            textSize.Width + 4, textSize.Height);
//                        g.FillRectangle(bgBrush, textRect);
//                        g.DrawString(distText, font, textBrush,
//                            midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 8);
//                        break;

//                    case MeasurementType.Angle:
//                        g.DrawLine(pen, m.Start, m.End);
//                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
//                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);

//                        // Draw angle value
//                        double angle = CalculateAngle(m);
//                        string angleText = $"{angle:F1}°";
//                        if (m.Axis.HasValue)
//                            angleText += $" to {m.Axis.Value}";

//                        textSize = g.MeasureString(angleText, font);
//                        g.FillRectangle(bgBrush, m.End.X, m.End.Y, textSize.Width + 4, textSize.Height);
//                        g.DrawString(angleText, font, textBrush, m.End.X + 2, m.End.Y);

//                        // Draw angle arc
//                        DrawAngleArc(g, m);
//                        break;
//                }
//            }
//        }

//        private void DrawAngleArc(Graphics g, Measurement m)
//        {
//            if (m.Type != MeasurementType.Angle || !m.Axis.HasValue) return;

//            double angle = CalculateAngle(m);
//            float startAngle = 0;
//            float sweepAngle = (float)angle;

//            if (m.Axis == AxisType.X)
//            {
//                startAngle = 0;
//            }
//            else
//            {
//                startAngle = 90;
//            }

//            using (Pen arcPen = new Pen(Color.FromArgb(100, Color.Orange), 2))
//            {
//                arcPen.DashStyle = DashStyle.Dash;
//                g.DrawArc(arcPen, m.Start.X - 30, m.Start.Y - 30, 60, 60, startAngle, sweepAngle);
//            }
//        }

//        private Color GetMeasurementColor(MeasurementType type)
//        {
//            switch (type)
//            {
//                case MeasurementType.Line: return Color.LimeGreen;
//                case MeasurementType.Point: return Color.Magenta;
//                case MeasurementType.Angle: return Color.Cyan;
//                case MeasurementType.Distance: return Color.Orange;
//                case MeasurementType.ReferenceLine: return Color.Red;
//                default: return Color.White;
//            }
//        }

//        // Dialog for axis selection
//        private class AxisSelectionDialog : Form
//        {
//            public AxisType SelectedAxis { get; private set; }

//            public AxisSelectionDialog()
//            {
//                InitializeComponent();
//            }

//            private void InitializeComponent()
//            {
//                this.Text = "Select Reference Axis";
//                this.Size = new Size(250, 120);
//                this.FormBorderStyle = FormBorderStyle.FixedDialog;
//                this.StartPosition = FormStartPosition.CenterParent;

//                Label label = new Label();
//                label.Text = "Select reference axis for angle measurement:";
//                label.Location = new Point(10, 10);
//                label.Size = new Size(220, 30);

//                Button xAxisBtn = new Button();
//                xAxisBtn.Text = "X-Axis";
//                xAxisBtn.Location = new Point(20, 50);
//                xAxisBtn.Size = new Size(80, 25);
//                xAxisBtn.Click += (s, e) => { SelectedAxis = AxisType.X; this.DialogResult = DialogResult.OK; };

//                Button yAxisBtn = new Button();
//                yAxisBtn.Text = "Y-Axis";
//                yAxisBtn.Location = new Point(120, 50);
//                yAxisBtn.Size = new Size(80, 25);
//                yAxisBtn.Click += (s, e) => { SelectedAxis = AxisType.Y; this.DialogResult = DialogResult.OK; };

//                this.Controls.Add(label);
//                this.Controls.Add(xAxisBtn);
//                this.Controls.Add(yAxisBtn);
//            }
//        }

//        // Dialog for reference input
//        private class ReferenceInputDialog : Form
//        {
//            private TextBox textBox;

//            public float ReferenceLength { get; private set; }

//            public ReferenceInputDialog()
//            {
//                InitializeComponent();
//            }

//            private void InitializeComponent()
//            {
//                this.Text = "Set Reference Length";
//                this.Size = new Size(300, 150);
//                this.FormBorderStyle = FormBorderStyle.FixedDialog;
//                this.StartPosition = FormStartPosition.CenterParent;
//                this.MaximizeBox = false;
//                this.MinimizeBox = false;

//                Label label = new Label();
//                label.Text = "Enter known length in real units:";
//                label.Location = new Point(20, 20);
//                label.Size = new Size(250, 20);

//                textBox = new TextBox();
//                textBox.Location = new Point(20, 50);
//                textBox.Size = new Size(250, 20);

//                Button okButton = new Button();
//                okButton.Text = "OK";
//                okButton.DialogResult = DialogResult.OK;
//                okButton.Location = new Point(60, 80);
//                okButton.Size = new Size(75, 25);
//                okButton.Click += OkButton_Click;

//                Button cancelButton = new Button();
//                cancelButton.Text = "Cancel";
//                cancelButton.DialogResult = DialogResult.Cancel;
//                cancelButton.Location = new Point(150, 80);
//                cancelButton.Size = new Size(75, 25);

//                this.Controls.Add(label);
//                this.Controls.Add(textBox);
//                this.Controls.Add(okButton);
//                this.Controls.Add(cancelButton);
//                this.AcceptButton = okButton;
//                this.CancelButton = cancelButton;
//            }

//            private void OkButton_Click(object sender, EventArgs e)
//            {
//                if (float.TryParse(textBox.Text, out float result) && result > 0)
//                {
//                    ReferenceLength = result;
//                }
//                else
//                {
//                    MessageBox.Show("Please enter a valid positive number.");
//                    this.DialogResult = DialogResult.None;
//                }
//            }
//        }
//    }

//    // Program entry point
//    //internal static class Program
//    //{
//    //    [STAThread]
//    //    static void Main()
//    //    {
//    //        Application.EnableVisualStyles();
//    //        Application.SetCompatibleTextRenderingDefault(false);
//    //        Application.Run(new MainForm());
//    //    }
//    //}
//}


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
                            $"A{measurementCounter}-1",
                            MeasurementType.Angle);
                        firstSegment.Vertex = angleVertex.Value;
                        measurements.Add(firstSegment);

                        Measurement secondSegment = new Measurement(
                            angleVertex.Value,
                            location,
                            $"A{measurementCounter}-2",
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
                else if (currentEditMode == EditMode.Move)
                {
                    selectedMeasurementIndex = index;
                    selectedMeasurement = measurements[index];

                    // Calculate offset for smooth dragging
                    if (selectedMeasurement.Value.Type == MeasurementType.Point)
                    {
                        dragOffset = new Point(
                            location.X - selectedMeasurement.Value.Start.X,
                            location.Y - selectedMeasurement.Value.Start.Y);
                    }
                    else
                    {
                        // For lines, calculate offset from midpoint
                        Point midPoint = new Point(
                            (selectedMeasurement.Value.Start.X + selectedMeasurement.Value.End.X) / 2,
                            (selectedMeasurement.Value.Start.Y + selectedMeasurement.Value.End.Y) / 2);
                        dragOffset = new Point(
                            location.X - midPoint.X,
                            location.Y - midPoint.Y);
                    }

                    isDraggingMeasurement = true;
                    pictureBox.Cursor = Cursors.SizeAll;
                    pictureBox.Invalidate();
                }
            }
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && IsNearPoint(e.Location, gridOrigin, gridGrabRadius))
            {
                isDraggingGrid = true;
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
            else if (currentTool != ToolMode.None && currentStartPoint.HasValue)
            {
                // Show preview of current measurement
                pictureBox.Invalidate();
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            isDraggingGrid = false;

            if (isDraggingMeasurement)
            {
                isDraggingMeasurement = false;
                pictureBox.Cursor = Cursors.Hand;
                UpdateMeasurementsList();
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
            else
            {
                // Calculate movement delta
                Point midPoint = new Point(
                    (m.Start.X + m.End.X) / 2,
                    (m.Start.Y + m.End.Y) / 2);

                int deltaX = mouseLocation.X - midPoint.X - dragOffset.X;
                int deltaY = mouseLocation.Y - midPoint.Y - dragOffset.Y;

                // Move both endpoints
                m.Start = new Point(m.Start.X + deltaX, m.Start.Y + deltaY);
                m.End = new Point(m.End.X + deltaX, m.End.Y + deltaY);
            }

            measurements[index] = m;
        }

        private int FindMeasurementAtPoint(Point point)
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                if (IsMeasurementAtPoint(measurements[i], point))
                    return i;
            }
            return -1;
        }

        private bool IsMeasurementAtPoint(Measurement m, Point point)
        {
            const int tolerance = 5;

            switch (m.Type)
            {
                case MeasurementType.Point:
                    return IsNearPoint(point, m.Start, tolerance);

                case MeasurementType.Line:
                case MeasurementType.Distance:
                case MeasurementType.ReferenceLine:
                case MeasurementType.Angle:
                case MeasurementType.AngleWithAxis:
                    return IsPointNearLine(point, m.Start, m.End, tolerance);

                default:
                    return false;
            }
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
                        if (!m.Name.EndsWith("-2")) // Only show for the first segment of each angle
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

        private double CalculateAngle(Measurement m)
        {
            if (m.Type != MeasurementType.Angle || !m.Vertex.HasValue) return 0;

            // Find the other segment that shares the same vertex
            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                meas.Type == MeasurementType.Angle &&
                meas.Vertex.HasValue &&
                meas.Vertex.Value == m.Vertex.Value &&
                meas.Name != m.Name);

            if (otherSegment.Type == MeasurementType.Angle)
            {
                // Calculate vectors from vertex to endpoints
                Point v1 = new Point(m.End.X - m.Vertex.Value.X, m.End.Y - m.Vertex.Value.Y);
                Point v2 = new Point(otherSegment.End.X - m.Vertex.Value.X, otherSegment.End.Y - m.Vertex.Value.Y);

                double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
                double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
                double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

                if (mag1 == 0 || mag2 == 0) return 0;

                double cosTheta = Math.Max(-1, Math.Min(1, dotProduct / (mag1 * mag2)));
                return Math.Acos(cosTheta) * (180 / Math.PI);
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
            if (currentStartPoint.HasValue && currentTool != ToolMode.None)
            {
                Point currentPos = pictureBox.PointToClient(Cursor.Position);

                using (Pen tempPen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash })
                {
                    if (currentTool == ToolMode.Angle)
                    {
                        if (angleVertex.HasValue && angleFirstPoint.HasValue)
                        {
                            // Draw both segments for angle
                            g.DrawLine(tempPen, angleVertex.Value, angleFirstPoint.Value);
                            g.DrawLine(tempPen, angleVertex.Value, currentPos);
                        }
                        else if (angleVertex.HasValue)
                        {
                            // Draw first segment
                            g.DrawLine(tempPen, angleVertex.Value, currentPos);
                        }
                    }
                    else
                    {
                        g.DrawLine(tempPen, currentStartPoint.Value, currentPos);
                    }
                }

                // Draw helper for 90° angles
                if (currentTool == ToolMode.Line || currentTool == ToolMode.Distance)
                {
                    DrawAngleHelpers(g, currentStartPoint.Value, currentPos);
                }
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

            using (Pen pen = new Pen(color, 2))
            using (Brush brush = new SolidBrush(color))
            using (Font font = new Font("Arial", 9))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - 4, m.Start.Y - 4, 8, 8);

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
                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);

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
                            g.FillEllipse(brush, m.Vertex.Value.X - 3, m.Vertex.Value.Y - 3, 6, 6);
                            g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);

                            // Find the other segment to calculate and draw angle
                            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                meas.Type == MeasurementType.Angle &&
                                meas.Vertex.HasValue &&
                                meas.Vertex.Value == m.Vertex.Value &&
                                meas.Name != m.Name);

                            if (otherSegment.Type == MeasurementType.Angle)
                            {
                                // Draw angle value at vertex
                                double angle = CalculateAngle(m);
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
                        g.FillEllipse(brush, m.Start.X - 3, m.Start.Y - 3, 6, 6);
                        g.FillEllipse(brush, m.End.X - 3, m.End.Y - 3, 6, 6);

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

        private void DrawAngleArc(Graphics g, Measurement m1, Measurement m2)
        {
            if (m1.Type != MeasurementType.Angle || !m1.Vertex.HasValue ||
                m2.Type != MeasurementType.Angle || !m2.Vertex.HasValue) return;

            // Calculate angles of both vectors
            Point v1 = new Point(m1.End.X - m1.Vertex.Value.X, m1.End.Y - m1.Vertex.Value.Y);
            Point v2 = new Point(m2.End.X - m2.Vertex.Value.X, m2.End.Y - m2.Vertex.Value.Y);

            double angle1 = Math.Atan2(v1.Y, v1.X) * (180 / Math.PI);
            double angle2 = Math.Atan2(v2.Y, v2.X) * (180 / Math.PI);

            // Ensure angles are positive
            if (angle1 < 0) angle1 += 360;
            if (angle2 < 0) angle2 += 360;

            float startAngle = (float)Math.Min(angle1, angle2);
            float sweepAngle = (float)Math.Abs(angle1 - angle2);

            // Ensure we draw the smaller angle
            if (sweepAngle > 180) sweepAngle = 360 - sweepAngle;

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