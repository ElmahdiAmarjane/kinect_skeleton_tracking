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

        private MultiSourceFrameReader _reader;
        private CoordinateMapper _coordinateMapper;
        private Bitmap _latestBitmap;
        private bool _takingPhoto = false;
        private KinectSensor _kinectSensor;

        public BodyPictureAnalyzer()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            UpdateModeDisplay();
            this.DoubleBuffered = true;
          //  pictureBox1.MouseDown += PictureBox1_MouseDown; // Nouvel événement
           
        }
        //private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        //{
        //    if (e.Button == MouseButtons.Right)
        //    {
        //        if (pictureBox1.Image == null) return;

        //        if (measurements.Count > 1 && measurements[measurements.Count - 1].Type == MeasurementType.Distance)
        //        {
        //            using (var input = new ForReferenceInputDialog())
        //            {
        //                if (input.ShowDialog(this) == DialogResult.OK)
        //                {
        //                    SetScaleFromReference(input.ReferenceLength);
        //                    pictureBox1.Invalidate();
        //                }
        //            }
        //        }
        //        else
        //        {
        //            MessageBox.Show("Tracez d'abord une ligne de référence en mode Distance");
        //        }
        //    }
        //}

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

            if (pictureBox1.Image == null) return;

            if (currentStartPoint == null)
            {
                currentStartPoint = e.Location;
                toolStripStatusLabel1.Text = currentMode == MeasurementMode.Distance
                    ? "Click to place end point for distance measurement"
                    : "Click to place second point of first segment";
            }
            else
            {
                if (currentMode == MeasurementMode.Distance)
                {
                    // Complete distance measurement
                    measurements.Add(new Measurement(
                        currentStartPoint.Value,
                        e.Location,
                        $"D{measurementCounter++}",
                        MeasurementType.Distance));

                    currentStartPoint = null;
                    CalculateMeasurements();
                }
                else // Angle mode
                {
                    if (measurements.Count > 0 && measurements[measurements.Count - 1].Type == MeasurementType.AngleSegment1)
                    {
                        // Complete angle measurement (second segment)
                        measurements.Add(new Measurement(
                            measurements[measurements.Count - 1].End, // Connect to previous segment
                            e.Location,
                            $"A{measurementCounter++}",
                            MeasurementType.AngleSegment2));

                        CalculateMeasurements();
                        currentStartPoint = null;
                    }
                    else
                    {
                        // First segment of angle measurement
                        measurements.Add(new Measurement(
                            currentStartPoint.Value,
                            e.Location,
                            $"A{measurementCounter++}",
                            MeasurementType.AngleSegment1));

                        currentStartPoint = e.Location; // Next segment starts here
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
            if (pictureBox1.Image == null)
            {
                using (var font = new Font("Segoe UI", 10))
                {
                    e.Graphics.DrawString("Please import an image first",
                        font, Brushes.White, new System.Drawing.PointF(10, 10));
                }
                return;
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

        private void btnClear_Click(object sender, EventArgs e)
        {
            measurements.Clear();
            lstMeasurements.Items.Clear();
            currentStartPoint = null;
            measurementCounter = 1;
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
        private enum MeasurementType { Distance, AngleSegment1, AngleSegment2 }

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
            _kinectSensor = KinectSensor.GetDefault();
            _coordinateMapper = _kinectSensor.CoordinateMapper;
            _reader = _kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color);
            _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            _kinectSensor.Open();
        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {

        }
    
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if (!_takingPhoto) return;

            var frameReference = e.FrameReference.AcquireFrame();

            using (var colorFrame = frameReference.ColorFrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription desc = colorFrame.FrameDescription;
                    byte[] pixels = new byte[desc.Width * desc.Height * 4];
                    colorFrame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

                    Bitmap bitmap = new Bitmap(desc.Width, desc.Height, PixelFormat.Format32bppArgb);
                    BitmapData bmpData = bitmap.LockBits(
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                        ImageLockMode.WriteOnly,
                        bitmap.PixelFormat);
                    Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                    bitmap.UnlockBits(bmpData);

                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        int rectWidth = 500;
                        int rectHeight = 1000;
                        int centerX = bitmap.Width / 2 - rectWidth / 2;
                        int centerY = bitmap.Height / 2 - rectHeight / 2 ;
                        Color rectColor = Color.LightGreen; // Change to green later if needed

                        g.DrawRectangle(new Pen(rectColor, 5), centerX, centerY, rectWidth, rectHeight);
                    }

                    _latestBitmap = (Bitmap)bitmap.Clone();
                    pictureBox1.Image = _latestBitmap;
                    bitmap.Dispose();
                }
            }
        }

        private void btnTakePhoto_Click(object sender, EventArgs e)
        {
            _takingPhoto = true;
            btnTakePhoto.Enabled = false;
            btnDone.Visible = true;
            btnDone.Enabled = true;
            
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            _takingPhoto = false;
            btnTakePhoto.Enabled = true;
            btnDone.Visible = false;
            btnDone.Enabled = false;

            if (_latestBitmap != null)
            {
                string path = Path.Combine(Application.StartupPath, "CapturedImage_" + DateTime.Now.Ticks + ".png");
                _latestBitmap.Save(path, ImageFormat.Png);
                

                MessageBox.Show("Photo enregistrée : " + path);

             //   DetectCobbAngleFromStickers(_latestBitmap);
                // Tu peux l'importer ici dans ton PictureBox d’analyse
               // pictureBox1.Image = Image.FromFile(path); // remplace par ton PictureBox
            }
        }

        // Ajoute ce using en haut si manquant


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



}
}