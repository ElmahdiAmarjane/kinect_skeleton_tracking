using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace kinectProject
{
    public partial class BodyPictureAnalyzer : Form
    {
        private enum MeasurementMode { Distance, Angle }
        private MeasurementMode currentMode = MeasurementMode.Distance;

        private List<Measurement> measurements = new List<Measurement>();
        private Image loadedImage;
        private Point? currentStartPoint = null;
        private int measurementCounter = 1;
        ////
        private float pixelToMmRatio = 1.0f; // Ratio px/mm (initialisé à 1 par défaut)
        private bool isReferenceSet = false; // Indique si l'échelle est calibrée
        /// <summary>
        /// //
        /// </summary>
        private bool showPlan = true;
        private Point planCenter;
        private bool isDraggingPlan = false;
        private const int planGrabRadius = 10; // sensitivity for grabbing the center

        /// <summary>
        /// /
        /// </summary>
         
        private Point? lastAngleVertex = null;
        private AxisType lastAngleAxis;
        private double lastLineAngle = 0;

        private List<(Point vertex, double refAngle, double lineAngle, AxisType axis)> highlightedAngles
    = new List<(Point vertex, double refAngle, double lineAngle, AxisType axis)>();
        //

        private bool deleteMode = false;
        private const int lineClickThreshold = 5; // Sensitivity in pixels for selecting a line


        public BodyPictureAnalyzer()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            UpdateModeDisplay();
            this.DoubleBuffered = true;
          //  pictureBox1.MouseDown += PictureBox1_MouseDown; // Nouvel événement
           
        }
      

        private void btnSetReferenceScale_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null)
            {
                MessageBox.Show("Please load an image first.");
                return;
            }

            if (measurements.Count == 0 || measurements[measurements.Count - 1].Type != MeasurementType.Distance)
            {
                MessageBox.Show("The last measurement must be a distance line to set the reference scale.");
                return;
            }

            using (var input = new ForReferenceInputDialog())
            {
                if (input.ShowDialog(this) == DialogResult.OK)
                {
                    SetScaleFromReference(input.ReferenceLength);
                    pictureBox1.Invalidate();
                }
            }
        }

        public void SetScaleFromReference(float referenceLengthMm)
        {
            if (measurements.Count == 0 || measurements[measurements.Count - 1].Type != MeasurementType.Distance)
            {
                MessageBox.Show("La dernière mesure doit être une ligne de référence en mode Distance.");
                return;
            }

            var referenceLine = measurements[measurements.Count - 1];
            float pixelLength = (float)CalculatePixelDistance(referenceLine.Start, referenceLine.End);

            if (referenceLengthMm <= 0 || pixelLength <= 0)
            {
                MessageBox.Show("Mesure invalide. Vérifiez les points sélectionnés et la valeur entrée.");
                return;
            }

            pixelToMmRatio = pixelLength / referenceLengthMm;
            isReferenceSet = true;

            toolStripStatusLabel1.Text = $"Échelle calibrée : 1 mm = {pixelToMmRatio:F2} px";
            CalculateMeasurements(); // Recalculer toutes les mesures
        }


        private double CalculatePixelDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private double CalculateRealDistance(Point p1, Point p2)
        {
            double pixels = CalculatePixelDistance(p1, p2);
            return pixels / pixelToMmRatio; // Conversion en mm
        }

        private void UpdateModeDisplay()
        {
            lblMode.Text = $"Mode: {currentMode}";
            btnSwitchMode.Text = $"Switch to {(currentMode == MeasurementMode.Distance ? "Angle" : "Distance")}";
            toolStripStatusLabel1.Text = currentMode == MeasurementMode.Distance
                ? "Click to place start and end points for distance measurement"
                : "Click to place connected segments for angle measurement";
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.png;*.bmp";
                openFileDialog.Title = "Select Body Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        loadedImage = Image.FromFile(openFileDialog.FileName);
                        pictureBox1.Image = (Image)loadedImage.Clone();
                        measurements.Clear();
                        lstMeasurements.Items.Clear();
                        measurementCounter = 1;
                        currentStartPoint = null;
                        isReferenceSet = false; // Réinitialiser l'échelle
                        pixelToMmRatio = 1.0f; // Réinitialiser le ratio
                        UpdateModeDisplay();

                        // ✅ Initialize plan center at image center
                        planCenter = new Point(pictureBox1.Width / 2, pictureBox1.Height / 2);

                        UpdateModeDisplay();
                        pictureBox1.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || pictureBox1.Image == null) return;

           

            // ✅ Delete mode
            if (deleteMode)
            {
                int indexToRemove = FindMeasurementAtPoint(e.Location);
                if (indexToRemove >= 0)
                {
                    string targetName = measurements[indexToRemove].Name;
                    measurements.RemoveAt(indexToRemove);

                    // 🧠 Find and remove from ListBox by name
                    for (int i = 0; i < lstMeasurements.Items.Count; i++)
                    {
                        if (lstMeasurements.Items[i].ToString().StartsWith(targetName))
                        {
                            lstMeasurements.Items.RemoveAt(i);
                            break;
                        }
                    }

                    // 🧠 Also remove from highlight if needed
                    int highlightIndex = FindHighlightedAngleAtPoint(e.Location);
                    if (highlightIndex >= 0)
                    {
                        highlightedAngles.RemoveAt(highlightIndex);
                    }

                    pictureBox1.Invalidate();
                }

                return;
            }

            // ✅ 1) If clicking on the plan center, ignore normal measurement click
            double dist = Math.Sqrt(Math.Pow(e.X - planCenter.X, 2) + Math.Pow(e.Y - planCenter.Y, 2));
            if (dist <= planGrabRadius)
            {
                // Plan drag click → do NOT create a point
                return;
            }

            // ✅ 2) Normal behavior for placing points
            if (currentStartPoint == null)
            {
                currentStartPoint = e.Location;
                toolStripStatusLabel1.Text = currentMode == MeasurementMode.Distance
                    ? "Click to place end point for distance measurement"
                    : "Click to place second point of first segment";
            }
            else
            {
                if (angleWithPlanMode)
                {
                    if (currentStartPoint == null)
                    {
                        currentStartPoint = e.Location;
                    }
                    else
                    {
                        // Complete the line
                        Point start = currentStartPoint.Value;
                        Point end = e.Location;

                        // Ask which axis to compare
                        var result = MessageBox.Show("Use X-axis? (No = Y-axis)",
                            "Choose Axis", MessageBoxButtons.YesNo);
                        bool useXAxis = (result == DialogResult.Yes);

                        double angle = CalculateAngleWithPlan(start, end, useXAxis);

                        //////////////////

                        // Store for drawing later
                        //lastAngleVertex = start;
                        //lastAngleAxis = useXAxis ? AxisType.X : AxisType.Y;
                        //lastLineAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                        double refAngle = (useXAxis ? 0 : Math.PI / 2);
                        double lineAngle = Math.Atan2(end.Y - start.Y, end.X - start.X);

                        // ✅ Store all highlights
                        highlightedAngles.Add((start, refAngle, lineAngle, useXAxis ? AxisType.X : AxisType.Y));


                        /////////////////

                        // ✅ Store as AngleWithPlan directly
                        measurements.Add(new Measurement(start, end, $"P{measurementCounter++}", MeasurementType.AngleWithPlan));

                        lstMeasurements.Items.Add($"P{measurementCounter++} Angle vs {(useXAxis ? "X-axis" : "Y-axis")}: {angle:F1}°");

                        currentStartPoint = null;
                      //  angleWithPlanMode = false;
                        pictureBox1.Invalidate();
                    }
                    return; // ✅ Stop here to avoid adding a Distance line
                }


                if (currentMode == MeasurementMode.Distance)
                {
                    measurements.Add(new Measurement(
                        currentStartPoint.Value,
                        e.Location,
                        $"D{measurementCounter++}",
                        MeasurementType.Distance));

                    currentStartPoint = null;
                    CalculateMeasurements();
                }
                else
                {
                    if (measurements.Count > 0 && measurements[measurements.Count - 1].Type == MeasurementType.AngleSegment1)
                    {
                        measurements.Add(new Measurement(
                            measurements[measurements.Count - 1].End,
                            e.Location,
                            $"A{measurementCounter++}",
                            MeasurementType.AngleSegment2));

                        CalculateMeasurements();
                        currentStartPoint = null;
                    }
                    else
                    {
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            e.Location,
                            $"A{measurementCounter++}",
                            MeasurementType.AngleSegment1));

                        currentStartPoint = e.Location;
                        toolStripStatusLabel1.Text = "Click to place end point of second segment";
                    }
                }
            }

            pictureBox1.Invalidate();
        }

        private void CalculateMeasurements()
        {
            lstMeasurements.Items.Clear();

            for (int i = 0; i < measurements.Count; i++)
            {
                var m = measurements[i];

                if (m.Type == MeasurementType.Distance)
                {
                    double pixels = CalculatePixelDistance(m.Start, m.End);
                    string itemText = $"{m.Name}: {pixels:F0} px";

                    if (isReferenceSet)
                    {
                        double mm = CalculateRealDistance(m.Start, m.End);
                        itemText += $" ({mm:F1} mm)";
                    }

                    lstMeasurements.Items.Add(itemText);
                }
                else if (m.Type == MeasurementType.AngleSegment2)
                {
                    // Look for a previous AngleSegment1
                    if (i > 0 && measurements[i - 1].Type == MeasurementType.AngleSegment1)
                    {
                        var seg1 = measurements[i - 1];
                        var seg2 = m;

                        double angle = CalculateAngle(seg1.End, seg1.Start, seg2.End);
                        lstMeasurements.Items.Add($"{m.Name}: {angle:F1}°");
                    }
                    else
                    {
                        lstMeasurements.Items.Add($"{m.Name}: Invalid angle (missing segment 1)");
                    }
                }
            }
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            if (measurements.Count == 0)
            {
                toolStripStatusLabel1.Text = "No measurements to calculate";
                return;
            }

            CalculateMeasurements();
            pictureBox1.Invalidate();
            toolStripStatusLabel1.Text = "Measurements calculated";
        }

        private double CalculateDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
        }

        private double CalculateAngle(Point vertex, Point p1, Point p2)
        {
            Vector2 v1 = new Vector2(p1.X - vertex.X, p1.Y - vertex.Y);
            Vector2 v2 = new Vector2(p2.X - vertex.X, p2.Y - vertex.Y);

            double dotProduct = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0)
                return 0;

            double cosTheta = Math.Max(-1, Math.Min(1, dotProduct / (mag1 * mag2)));
            return Math.Acos(cosTheta) * (180.0 / Math.PI);
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            // ✅ Draw all highlighted angles
            foreach (var angleData in highlightedAngles)
            {
                DrawAngleHighlight(e.Graphics, angleData.vertex, angleData.refAngle, angleData.lineAngle);
            }



            if (pictureBox1.Image == null)
            {
                using (var font = new Font("Segoe UI", 10))
                {
                    e.Graphics.DrawString("Please import an image first",
                        font, Brushes.White, new System.Drawing.PointF(10, 10));
                }
                return;
            }

          
            // === ✅ Draw XY Plan ===
            if (showPlan)
            {
                using (Pen axisPen = new Pen(Color.Red, 1.5f))
                {
                    axisPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

                    // Horizontal line (X-axis)
                    e.Graphics.DrawLine(axisPen, 0, planCenter.Y, pictureBox1.Width, planCenter.Y);

                    // Vertical line (Y-axis)
                    e.Graphics.DrawLine(axisPen, planCenter.X, 0, planCenter.X, pictureBox1.Height);
                }

                // Draw draggable center point
                using (Brush grabBrush = new SolidBrush(Color.Yellow))
                {
                    e.Graphics.FillEllipse(grabBrush, planCenter.X - 5, planCenter.Y - 5, 10, 10);
                }
            }


            // Draw all measurements
            foreach (var m in measurements)
            {
                Color lineColor = m.Type == MeasurementType.Distance ? Color.LimeGreen :
                                 m.Type == MeasurementType.AngleSegment1 ? Color.Cyan : Color.Black;

                using (var pen = new Pen(lineColor, 2f))
                {
                    e.Graphics.DrawLine(pen, m.Start, m.End);
                }

                // Draw points
                e.Graphics.FillEllipse(Brushes.Red, m.Start.X - 3, m.Start.Y - 3, 6, 6);
                e.Graphics.FillEllipse(Brushes.Red, m.End.X - 3, m.End.Y - 3, 6, 6);

                // Calculate midpoint for label
                Point midPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);

                // Prepare measurement text
                string measurementText = m.Name;
                if (m.Type == MeasurementType.Distance)
                {
                    double pixels = CalculatePixelDistance(m.Start, m.End);
                   // measurementText += $": {pixels:F0} px";

                    if (isReferenceSet)
                    {
                        double mm = CalculateRealDistance(m.Start, m.End);
                        measurementText += $" {mm:F1} mm"; // Nouvelle ligne pour les mm
                    }
                }

                // Draw label slightly offset from the line (not overlapping or under)
                using (var font = new Font("Segoe UI", 9, FontStyle.Regular))
                using (var bgBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    if (string.IsNullOrWhiteSpace(measurementText))
                        return;

                    SizeF textSize = e.Graphics.MeasureString(measurementText, font);

                    // Midpoint of the line
                    float midX = (m.Start.X + m.End.X) / 2f;
                    float midY = (m.Start.Y + m.End.Y) / 2f;

                    // Offset direction: perpendicular to the line
                    double angle = Math.Atan2(m.End.Y - m.Start.Y, m.End.X - m.Start.X);
                    float offsetX = (float)(-Math.Sin(angle) * 15); // 15 px away perpendicular
                    float offsetY = (float)(Math.Cos(angle) * 15);

                    // Final position: a bit away from the line
                    float labelX = midX + offsetX - textSize.Width / 2;
                    float labelY = midY + offsetY - textSize.Height / 2;

                    //RectangleF textBg = new RectangleF(
                    //    labelX - 3,
                    //    labelY - 2,
                    //    textSize.Width + 6,
                    //    textSize.Height + 4);

                    //e.Graphics.FillRectangle(bgBrush, textBg);
                    e.Graphics.DrawString(measurementText, font, textBrush, labelX, labelY);
                }

            }
            // Draw current measurement in progress
            if (currentStartPoint != null)
            {
                Point currentPos = pictureBox1.PointToClient(Cursor.Position);
                using (var tempPen = new Pen(Color.Yellow, 1.5f))
                {
                    e.Graphics.DrawLine(tempPen, currentStartPoint.Value, currentPos);
                }
                e.Graphics.FillEllipse(Brushes.Red, currentStartPoint.Value.X - 3, currentStartPoint.Value.Y - 3, 6, 6);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (showPlan && e.Button == MouseButtons.Left)
            {
                double dist = Math.Sqrt(Math.Pow(e.X - planCenter.X, 2) + Math.Pow(e.Y - planCenter.Y, 2));
                if (dist <= planGrabRadius)
                {
                    isDraggingPlan = true;
                    Cursor = Cursors.Hand;
                }
            }
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingPlan)
            {
                planCenter = e.Location;
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingPlan)
            {
                isDraggingPlan = false;
                Cursor = Cursors.Default;
            }
        }


        private void btnClear_Click(object sender, EventArgs e)
        {
            measurements.Clear();
            lstMeasurements.Items.Clear();
            currentStartPoint = null;
            measurementCounter = 1;
            //
            // ✅ Reset highlighted angles
            highlightedAngles.Clear();


            //
            toolStripStatusLabel1.Text = "Ready for new measurements";
            pictureBox1.Invalidate();
        }

        private void btnSwitchMode_Click(object sender, EventArgs e)
        {
            currentMode = currentMode == MeasurementMode.Distance
                ? MeasurementMode.Angle
                : MeasurementMode.Distance;

            currentStartPoint = null;
            UpdateModeDisplay();
        }

        // Helper classes
        private enum MeasurementType { Distance, AngleSegment1, AngleSegment2, AngleWithPlan }

        private struct Measurement
        {
            public Point Start { get; }
            public Point End { get; }
            public string Name { get; }
            public MeasurementType Type { get; }

            public Measurement(Point start, Point end, string name, MeasurementType type)
            {
                Start = start;
                End = end;
                Name = name;
                Type = type;
            }
        }

        private struct Vector2
        {
            public double X { get; }
            public double Y { get; }

            public Vector2(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        private void BodyPictureAnalyzer_Load(object sender, EventArgs e)
        {
            
        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {

        }



        private void btnTogglePlan_Click(object sender, EventArgs e)
        {
            showPlan = !showPlan;
            pictureBox1.Invalidate(); // Refresh image
        }


        private bool angleWithPlanMode = false;

        private void btnAngleWithPlan_Click(object sender, EventArgs e)
        {
            currentMode = MeasurementMode.Distance; // we draw a single line
            angleWithPlanMode = !angleWithPlanMode;
            toolStripStatusLabel1.Text = "Draw a line, then select axis (X or Y)";
        }

        private void DrawAngleHighlight(Graphics g, Point vertex, double refAngle, double lineAngle)
        {
            // Compute start and sweep angles (degrees)
            float startAngle = (float)(Math.Min(refAngle, lineAngle) * 180 / Math.PI);
            float sweepAngle = (float)(Math.Abs(lineAngle - refAngle) * 180 / Math.PI);

            // Draw semi-transparent arc
            using (Brush semiBrush = new SolidBrush(Color.FromArgb(80, Color.LightBlue)))
            {
                float radius = 50; // size of the arc highlight
                g.FillPie(semiBrush,
                    vertex.X - radius,
                    vertex.Y - radius,
                    radius * 2,
                    radius * 2,
                    startAngle,
                    sweepAngle);
            }
        }

        private enum AxisType
        {
            X,
            Y
        }

        private void btnDeleteMode_Click(object sender, EventArgs e)
        {
            deleteMode = !deleteMode;
            btnDeleteMode.Text = deleteMode ? "Delete: ON" : "Delete: OFF";
            Cursor = deleteMode ? Cursors.Cross : Cursors.Default;
        }

        private int FindMeasurementAtPoint(Point clickPoint)
        {
            for (int i = 0; i < measurements.Count; i++)
            {
                if (IsPointNearLine(clickPoint, measurements[i].Start, measurements[i].End))
                    return i;
            }
            return -1;
        }

        private int FindHighlightedAngleAtPoint(Point clickPoint)
        {
            for (int i = 0; i < highlightedAngles.Count; i++)
            {
                Point vertex = highlightedAngles[i].vertex;
                if (Math.Sqrt(Math.Pow(clickPoint.X - vertex.X, 2) + Math.Pow(clickPoint.Y - vertex.Y, 2)) <= 8)
                    return i;
            }
            return -1;
        }

        private bool IsPointNearLine(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            if (dx == 0 && dy == 0)
                return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2)) < lineClickThreshold;

            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            double projX = a.X + t * dx;
            double projY = a.Y + t * dy;

            double distance = Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
            return distance <= lineClickThreshold;
        }


        private void DetectCobbAngleFromStickers(Bitmap bitmap)
    {
        if (bitmap == null)
        {
            MessageBox.Show("Aucune image chargée.");
            return;
        }

        // 1. Liste pour stocker les points rouges détectés
        List<Point> redPoints = new List<Point>();

        // 2. Scanner l’image pixel par pixel
        for (int y = 0; y < bitmap.Height; y += 2) // +2 pour aller plus vite
        {
            for (int x = 0; x < bitmap.Width; x += 2)
            {
                Color pixel = bitmap.GetPixel(x, y);

                // 3. Détection de rouge : intensité R élevée, G et B faibles
                //if (pixel.R > 180 && pixel.G < 80 && pixel.B < 80)
                //{
                //    redPoints.Add(new Point(x, y));
                //}
                    if (pixel.R > 220 && pixel.G > 220 && pixel.B > 220)
                    {
                        redPoints.Add(new Point(x, y));
                    }

                }
            }

        if (redPoints.Count < 50)
        {
            MessageBox.Show("Pas assez de points rouges détectés.");
            return;
        }

        // 4. Grouper les points en 3 clusters (k-means simple)
        var clusteredPoints = redPoints
            .OrderBy(p => p.Y) // du haut vers le bas (axe Y)
            .GroupBy(p => redPoints.IndexOf(p) * 3 / redPoints.Count) // 3 groupes approximés
            .Select(g => new Point(
                (int)g.Average(p => p.X),
                (int)g.Average(p => p.Y)
            ))
            .ToList();

        if (clusteredPoints.Count < 3)
        {
            MessageBox.Show("Impossible de trouver 3 clusters rouges.");
            return;
        }

        // 5. Trier les points du haut vers le bas (vertèbres sup -> inf)
        clusteredPoints = clusteredPoints.OrderBy(p => p.Y).ToList();

        Point upper = clusteredPoints[0];
        Point middle = clusteredPoints[1];
        Point lower = clusteredPoints[2];

        // 6. Calcul de l’angle de Cobb (angle entre 2 lignes)
        double angle = CalculateAngle(middle, upper, lower); // vertex = milieu

        MessageBox.Show($"Angle de Cobb estimé : {angle:F1}°", "Résultat");

        // 7. Affichage visuel (optionnel mais utile)
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            using (Pen pen = new Pen(Color.Yellow, 3))
            {
                g.DrawLine(pen, upper, middle);
                g.DrawLine(pen, middle, lower);
            }

            foreach (var pt in clusteredPoints)
            {
                g.FillEllipse(Brushes.Red, pt.X - 5, pt.Y - 5, 10, 10);
            }
        }

        pictureBox1.Image = (Bitmap)bitmap.Clone(); // rafraîchir l’image avec lignes
    }

        private double CalculateAngleWithPlan(Point p1, Point p2, bool useXAxis)
        {
            // Convert line into vector
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;

            // Axis vector based on draggable plan
            double ax, ay;
            if (useXAxis)
            {
                ax = 1;
                ay = 0;
            }
            else
            {
                ax = 0;
                ay = 1;
            }

            // Dot product and magnitudes
            double dot = dx * ax + dy * ay;
            double mag1 = Math.Sqrt(dx * dx + dy * dy);
            double mag2 = Math.Sqrt(ax * ax + ay * ay);

            if (mag1 == 0 || mag2 == 0)
                return 0;

            double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
            return Math.Acos(cosTheta) * (180.0 / Math.PI);
        }


    }
}