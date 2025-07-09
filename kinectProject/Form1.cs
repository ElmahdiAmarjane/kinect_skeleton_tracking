using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Kinect;
using Microsoft.VisualBasic;
using System.IO;
using System.Linq;
using System.Collections.Generic;



namespace KinectProject
{
    public partial class Form1 : Form
    {
        private KinectSensor kinectSensor;
        private MultiSourceFrameReader multiSourceFrameReader;
        private Bitmap depthBitmap;
        private byte[] depthPixels;
        private DateTime lastFrameTime = DateTime.MinValue;
        private const int TargetFrameRate = 30;

        // More precise depth range for human body
        private const ushort BODY_DETECTION_MIN_DEPTH = 500;  // 0.5m
        private const ushort BODY_DETECTION_MAX_DEPTH = 2000; // 2m
        private const int DEPTH_WINDOW = 200; // Adjustable depth window in millimeters

        // VARIABLES FOR SELECT TWO POINTS
        private DepthFrameReader depthReader;
        private CoordinateMapper coordinateMapper;

        private Point clickPoint1 = Point.Empty;
        private Point clickPoint2 = Point.Empty;

        private CameraSpacePoint? selectedPoint1 = null;
        private CameraSpacePoint? selectedPoint2 = null;

        private PictureBox depthPictureBox; // Make this global if it's not already
        private PictureBox sideBox;

        private List<System.Drawing.PointF> lastSmoothedPoints = new List<System.Drawing.PointF>();

        private List<System.Drawing.PointF> lastSmoothedSpinePoints = new List<System.Drawing.PointF>();

        // En haut de la classe Form1 :
        private int maxZIndex = -1;

        private float fixedDeepestXPixel = -1;  // ← position en pixels sur le sideBox (avec échelle)


        ///////////////
        /// <summary>
        /// 
        /// </summary>
        ComboBox jointSelector1 = new ComboBox();
        ComboBox jointSelector2 = new ComboBox();
        Label depthDiffLabel = new Label();

        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                kinectSensor = KinectSensor.GetDefault();
                if (kinectSensor == null)
                {
                    MessageBox.Show("No Kinect sensor detected.");
                    return;
                }

                kinectSensor.Open();

                coordinateMapper = kinectSensor.CoordinateMapper;
                depthReader = kinectSensor.DepthFrameSource.OpenReader();

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);
                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameReader_MultiSourceFrameArrived;

                depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
                depthPixels = new byte[512 * 424 * 4];
// === ÉTAPE 2 : Créer le PictureBox et l'ajouter après ===
                depthPictureBox = new PictureBox
                {
                    Width = 500,
                    Height = 424,
                    Dock = DockStyle.Left,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Black
                };
                this.Controls.Add(depthPictureBox); // Ajouté après le panel
                depthPictureBox.MouseClick += DepthPictureBox_MouseClick;
                ///////////////////////:
                ///
                // === PictureBox secondaire pour la courbe sagittale ===
                 sideBox = new PictureBox
                {
                    Width = 350,
                    Height = 424,
                    Dock = DockStyle.Right,
                    BackColor = Color.Black
                };
                this.Controls.Add(sideBox);

                // 👉 Ajouter le suivi de la souris ici
                sideBox.MouseMove += SideBox_MouseMove;


                ////////////////////////
                // === ÉTAPE 1 : Créer le Panel pour les boutons et UI ===
                Panel topPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 60,
                    BackColor = Color.DarkGray
                };
                this.Controls.Add(topPanel); // Ajouté en premier !

                

                // === ÉTAPE 3 : Bouton Capture Vue 3D ===
                Button captureBtn = new Button();
                captureBtn.Text = "Capturer Vue 3D";
                captureBtn.Location = new Point(10, 15);
                captureBtn.Size = new Size(140, 30);
                captureBtn.BackColor = Color.LightGreen;

                captureBtn.Click += (s, args) =>
                {
                    string label = Microsoft.VisualBasic.Interaction.InputBox("Nom de la vue (ex: face, gauche...)", "Nom vue", "face");
                    if (string.IsNullOrWhiteSpace(label)) return;

                    string fileName = $"capture_{label}_{DateTime.Now:HHmmss}.ply";
                    CapturePointCloud(fileName);
                };
                topPanel.Controls.Add(captureBtn);
                ///////////////////////

                Button sagittalBtn = new Button();
                sagittalBtn.Text = "Capturer Courbe Sagittale";
                sagittalBtn.Location = new Point(620, 15);
                sagittalBtn.Size = new Size(180, 30);
                sagittalBtn.BackColor = Color.LightBlue;
                sagittalBtn.Click += SagittalBtn_Click;
                topPanel.Controls.Add(sagittalBtn);


                //////////////////////
                // === ÉTAPE 4 : ComboBox pour les joints ===
                jointSelector1.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector2.Items.AddRange(Enum.GetNames(typeof(JointType)));

                jointSelector1.SelectedIndex = 0;
                jointSelector2.SelectedIndex = 1;

                jointSelector1.Location = new Point(160, 15);
                jointSelector2.Location = new Point(310, 15);

                topPanel.Controls.Add(jointSelector1);
                topPanel.Controls.Add(jointSelector2);

                // === ÉTAPE 5 : Label pour la différence de profondeur ===
                depthDiffLabel.Location = new Point(470, 20);
                depthDiffLabel.Text = "Depth Difference: - mm";
                depthDiffLabel.AutoSize = true;
                topPanel.Controls.Add(depthDiffLabel);

                // === Message d'information ===
                MessageBox.Show("Veuillez vous placer à 1-2 mètres du capteur pour une détection optimale.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void MultiSourceFrameReader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if ((DateTime.Now - lastFrameTime).TotalMilliseconds < 1000 / TargetFrameRate)
                return;

            lastFrameTime = DateTime.Now;

            var multiSourceFrame = e.FrameReference.AcquireFrame();
            if (multiSourceFrame == null) return;

            using (var depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            using (var bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                if (depthFrame != null && bodyFrame != null)
                {
                    ProcessDepthFrameWithBodyContext(depthFrame, bodyFrame);
                }
            }
        }

        private void ProcessDepthFrameWithBodyContext(DepthFrame depthFrame, BodyFrame bodyFrame)
        {
            try
            {
                int width = depthFrame.FrameDescription.Width;
                int height = depthFrame.FrameDescription.Height;
                ushort[] depthData = new ushort[width * height];
                depthFrame.CopyFrameDataToArray(depthData);

               

                Body[] bodies = new Body[bodyFrame.BodyCount];
                bodyFrame.GetAndRefreshBodyData(bodies);

                // Find the primary tracked body
                Body trackedBody = null;
                foreach (var body in bodies)
                {
                    if (body.IsTracked)
                    {
                        trackedBody = body;
                        break;
                    }
                }

                if (trackedBody == null) return;
                DrawSpineOnBitmap(trackedBody);


                // ***************************************
                // Get selected joints from ComboBox
                string jointName1 = jointSelector1.SelectedItem.ToString();
                string jointName2 = jointSelector2.SelectedItem.ToString();

                // Convert selected joint names to JointType enum
                JointType jointType1 = (JointType)Enum.Parse(typeof(JointType), jointName1);
                JointType jointType2 = (JointType)Enum.Parse(typeof(JointType), jointName2);

                // Get depth values of the selected joints
                CameraSpacePoint position1 = trackedBody.Joints[jointType1].Position;
                CameraSpacePoint position2 = trackedBody.Joints[jointType2].Position;

                // Convert to millimeters
                ushort depth1 = (ushort)(position1.Z * 1000);
                ushort depth2 = (ushort)(position2.Z * 1000);

                // Calculate depth difference
                int depthDifference = Math.Abs(depth1 - depth2);

                // Update the label
                depthDiffLabel.Text = $"{jointName1}-{jointName2} Depth Diff: {depthDifference} mm";

                // ***************************************


                // Get spine base position for reference depth
                CameraSpacePoint spineBase = trackedBody.Joints[JointType.SpineMid].Position;
                ushort referenceDepth = (ushort)(spineBase.Z * 1000); // Convert to millimeters

                // Calculate adaptive depth window
                ushort minDepth = (ushort)Math.Max(referenceDepth - DEPTH_WINDOW, BODY_DETECTION_MIN_DEPTH);
                ushort maxDepth = (ushort)Math.Min(referenceDepth + DEPTH_WINDOW, BODY_DETECTION_MAX_DEPTH);

                // Update depth range display
                if (Controls.Count > 1 && Controls[1] is Label depthLabel)
                {
                    depthLabel.Text = $"Body Depth Range: {minDepth}mm - {maxDepth}mm";
                }

                Parallel.For(0, depthData.Length, i =>
                {
                    ushort depth = depthData[i];

                    // Check if depth is within our region of interest
                    if (depth == 0 || depth < minDepth || depth > maxDepth)
                    {
                        SetPixelColor(i, 0, 0, 0); // Black for out of range
                        return;
                    }

                    // Enhanced sensitivity mapping
                    if (depth >= minDepth && depth <= maxDepth && trackedBody != null)
                    {
                        // Normalize depth within the body-specific window
                        double normalizedDepth = (depth - minDepth) / (double)(maxDepth - minDepth);

                        // Use red for closest parts (chest) to blue for furthest (stomach)
                        if (normalizedDepth < 0.33)
                        {
                            // Red to Yellow
                            byte r = 255;
                            byte g = (byte)(normalizedDepth * 3 * 255);
                            SetPixelColor(i, r, g, 0);
                        }
                        else if (normalizedDepth < 0.66)
                        {
                            // Yellow to Green
                            byte r = (byte)((0.66 - normalizedDepth) * 3 * 255);
                            byte g = 255;
                            SetPixelColor(i, r, g, 0);
                        }
                        else
                        {
                            // Green to Blue
                            byte g = (byte)((1 - normalizedDepth) * 3 * 255);
                            byte b = (byte)(normalizedDepth * 255);
                            SetPixelColor(i, 0, g, b);
                        }
                    }
                    else
                    {
                        // Grey for detected but out of focus range
                        SetPixelColor(i, 128, 128, 128);
                    }
                });

                UpdateBitmap(width, height);
                DrawSpineOnBitmap(trackedBody);
                depthPictureBox.Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void SetPixelColor(int index, byte r, byte g, byte b)
        {
            depthPixels[index * 4] = b;
            depthPixels[index * 4 + 1] = g;
            depthPixels[index * 4 + 2] = r;
            depthPixels[index * 4 + 3] = 255;
        }

        private void UpdateBitmap(int width, int height)
        {
            BitmapData bitmapData = depthBitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(depthPixels, 0, bitmapData.Scan0, depthPixels.Length);
            depthBitmap.UnlockBits(bitmapData);

            var pictureBox = Controls[0] as PictureBox;
            if (pictureBox != null)
            {
                pictureBox.Image = depthBitmap;
            }
        }

        private void DepthPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (depthBitmap == null || coordinateMapper == null || depthReader == null)
            {
                MessageBox.Show("Initialization error: Missing depthBitmap or coordinateMapper.");
                return;
            }

            int x = e.X * 512 / depthPictureBox.Width;   // Scale from PictureBox to depth image size
            int y = e.Y * 424 / depthPictureBox.Height;

            using (var frame = depthReader.AcquireLatestFrame())
            {
                if (frame == null)
                {
                    MessageBox.Show("No depth frame available.");
                    return;
                }

                ushort[] depthData = new ushort[512 * 424];
                frame.CopyFrameDataToArray(depthData);

                int index = y * 512 + x;
                ushort depth = depthData[index];

                if (depth == 0) return;  // Skip if no valid depth

                // Map the depth point to camera space
                DepthSpacePoint depthPoint = new DepthSpacePoint { X = x, Y = y };
                CameraSpacePoint cameraPoint = coordinateMapper.MapDepthPointToCameraSpace(depthPoint, depth);

                // Select the points and calculate depth difference
                if (selectedPoint1 == null)
                {
                    selectedPoint1 = cameraPoint;
                    MessageBox.Show("First point selected.");
                }
                else if (selectedPoint2 == null)
                {
                    selectedPoint2 = cameraPoint;

                    // Calculate depth difference (Z-axis difference)
                    float depthDifference = Math.Abs(selectedPoint1.Value.Z - selectedPoint2.Value.Z) * 1000; // in mm

                    // Display the depth difference
                    MessageBox.Show($"Depth Difference: {depthDifference:F2} mm");

                    // Reset selected points for the next measurement
                    selectedPoint1 = null;
                    selectedPoint2 = null;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (multiSourceFrameReader != null) multiSourceFrameReader.Dispose();
            if (kinectSensor != null) kinectSensor.Close();
            base.OnFormClosing(e);
        }
        private void CapturePointCloud(string fileName)
        {
            var multiFrame = multiSourceFrameReader.AcquireLatestFrame();
            if (multiFrame == null)
            {
                MessageBox.Show("MultiSourceFrame indisponible.");
                return;
            }

            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            using (var colorFrame = multiFrame.ColorFrameReference.AcquireFrame())
            {
                if (depthFrame == null || colorFrame == null)
                {
                    MessageBox.Show("DepthFrame ou ColorFrame indisponible.");
                    return;
                }

                int width = depthFrame.FrameDescription.Width;
                int height = depthFrame.FrameDescription.Height;

                ushort[] depthData = new ushort[width * height];
                depthFrame.CopyFrameDataToArray(depthData);

                CameraSpacePoint[] cameraPoints = new CameraSpacePoint[width * height];
                coordinateMapper.MapDepthFrameToCameraSpace(depthData, cameraPoints);

                // Ta plage habituelle pour le corps (en mm converti en m)
                const ushort BODY_DETECTION_MIN_DEPTH = 500;
                const ushort BODY_DETECTION_MAX_DEPTH = 2000;
                const int DEPTH_WINDOW = 200;

                // Pour chaque point on calcule la couleur selon la profondeur autour de la base de la colonne vertébrale (SpineMid)
                // Cherchons la profondeur de référence (si tu veux, sinon on peut prendre la moyenne des profondeurs valides)
                // Ici, on prend la profondeur médiane des points valides pour plus de stabilité
                var validDepths = depthData.Where(d => d >= BODY_DETECTION_MIN_DEPTH && d <= BODY_DETECTION_MAX_DEPTH).ToArray();
                if (validDepths.Length == 0)
                {
                    MessageBox.Show("Aucune profondeur valide dans la plage.");
                    return;
                }
                ushort referenceDepth = validDepths[validDepths.Length / 2];

                // Fenêtre adaptative autour de cette profondeur
                ushort minDepth = (ushort)Math.Max(referenceDepth - DEPTH_WINDOW, BODY_DETECTION_MIN_DEPTH);
                ushort maxDepth = (ushort)Math.Min(referenceDepth + DEPTH_WINDOW, BODY_DETECTION_MAX_DEPTH);

                // Compter points valides
                int validPointsCount = 0;
                for (int i = 0; i < cameraPoints.Length; i++)
                {
                    var cp = cameraPoints[i];
                    if (float.IsInfinity(cp.X) || float.IsNaN(cp.X)) continue;

                    // Convertir profondeur en mm
                    int depthInMM = (int)(cp.Z * 1000);
                    if (depthInMM < minDepth || depthInMM > maxDepth) continue;

                    validPointsCount++;
                }

                using (var writer = new StreamWriter(fileName))
                {
                    // Écrire header PLY
                    writer.WriteLine("ply");
                    writer.WriteLine("format ascii 1.0");
                    writer.WriteLine($"element vertex {validPointsCount}");
                    writer.WriteLine("property float x");
                    writer.WriteLine("property float y");
                    writer.WriteLine("property float z");
                    writer.WriteLine("property uchar red");
                    writer.WriteLine("property uchar green");
                    writer.WriteLine("property uchar blue");
                    writer.WriteLine("end_header");

                    for (int i = 0; i < cameraPoints.Length; i++)
                    {
                        var cp = cameraPoints[i];
                        if (float.IsInfinity(cp.X) || float.IsNaN(cp.X)) continue;

                        int depthInMM = (int)(cp.Z * 1000);
                        if (depthInMM < minDepth || depthInMM > maxDepth) continue;

                        // Normaliser profondeur entre minDepth et maxDepth
                        double normalizedDepth = (depthInMM - minDepth) / (double)(maxDepth - minDepth);

                        byte r, g, b;

                        if (normalizedDepth < 0.33)
                        {
                            // Rouge à Jaune
                            r = 255;
                            g = (byte)(normalizedDepth * 3 * 255);
                            b = 0;
                        }
                        else if (normalizedDepth < 0.66)
                        {
                            // Jaune à Vert
                            r = (byte)((0.66 - normalizedDepth) * 3 * 255);
                            g = 255;
                            b = 0;
                        }
                        else
                        {
                            // Vert à Bleu
                            r = 0;
                            g = (byte)((1 - normalizedDepth) * 3 * 255);
                            b = (byte)(normalizedDepth * 255);
                        }

                        writer.WriteLine($"{cp.X} {cp.Y} {cp.Z} {r} {g} {b}");
                    }
                }

                MessageBox.Show($"Nuage de points 3D coloré selon profondeur enregistré :\n{fileName}");
            }
        }

        // 29/06
        private void DrawSpineOnBitmap(Body body)
        {
            if (body == null || coordinateMapper == null) return;

            var joints = new JointType[]
            {
        JointType.SpineBase,
        JointType.SpineMid,
        JointType.SpineShoulder,
        JointType.Neck,
        JointType.Head
            };

            List<System.Drawing.PointF> spinePoints2D = new List<System.Drawing.PointF>();

            foreach (var jointType in joints)
            {
                Joint joint = body.Joints[jointType];
                if (joint.TrackingState == TrackingState.NotTracked)
                    return;

                DepthSpacePoint dp = coordinateMapper.MapCameraPointToDepthSpace(joint.Position);

                if (float.IsNaN(dp.X) || float.IsNaN(dp.Y))
                    return;

                // Vérifier que le point est bien dans les limites de l'image (512x424)
                if (dp.X >= 0 && dp.X < 512 && dp.Y >= 0 && dp.Y < 424)
                {
                    spinePoints2D.Add(new System.Drawing.PointF(dp.X, dp.Y));
                }
            }

            // Tracer si au moins 2 points valides
            if (spinePoints2D.Count >= 2)
            {
                using (Graphics g = Graphics.FromImage(depthBitmap))
                using (Pen redPen = new Pen(Color.Red, 4))
                {
                    for (int i = 0; i < spinePoints2D.Count - 1; i++)
                    {
                        g.DrawLine(redPen, spinePoints2D[i], spinePoints2D[i + 1]);
                    }
                }
            }
        }

        //03/07

        // AJOUTER CETTE MÉTHODE DANS Form1
        private void DrawDepthSpineCurve(ushort[] depthData)
        {
            int width = 512;
            int height = 424;
            int centerX = width / 2;
            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);

            List<System.Drawing.PointF> rawPoints = new List<System.Drawing.PointF>();
            float maxZ = float.MinValue;
            maxZIndex = -1;

            // Sample multiple center columns and apply median filter
            for (int y = 0; y < height; y += 1)
            {
                List<float> zSamples = new List<float>();
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = centerX + dx;
                    if (x < 0 || x >= width) continue;

                    int index = y * width + x;
                    ushort depth = depthData[index];
                    if (depth == 0 || depth < BODY_DETECTION_MIN_DEPTH || depth > BODY_DETECTION_MAX_DEPTH)
                        continue;

                    CameraSpacePoint cp = coordinateMapper.MapDepthPointToCameraSpace(
                        new DepthSpacePoint { X = x, Y = y }, depth);

                    zSamples.Add(cp.Z * 1000f); // in mm
                }

                if (zSamples.Count >= 3)
                {
                    float medianZ = zSamples.OrderBy(z => z).ElementAt(zSamples.Count / 2);
                    rawPoints.Add(new System.Drawing.PointF(medianZ, y));

                    if (medianZ > maxZ)
                    {
                        maxZ = medianZ;
                        maxZIndex = rawPoints.Count - 1;
                    }
                }
            }

            if (rawPoints.Count < 5)
            {
                sideBox.Image = sideView;
                return;
            }

            var filtered = FilterDepthPoints(rawPoints);
            List<System.Drawing.PointF> smoothedPoints = InterpolateSpinePoints(filtered);

            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);

                using (Pen spinePen = new Pen(Color.Cyan, 3))
                {
                    for (int i = 1; i < smoothedPoints.Count; i++)
                    {
                        float x1 = 50 + smoothedPoints[i - 1].X * 0.1f;
                        float y1 = smoothedPoints[i - 1].Y;
                        float x2 = 50 + smoothedPoints[i].X * 0.1f;
                        float y2 = smoothedPoints[i].Y;

                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                // Trouver le point le plus profond après interpolation
                float deepestZ = float.MinValue;
                float deepestX = 0;

                for (int i = 0; i < smoothedPoints.Count; i++)
                {
                    if (smoothedPoints[i].X > deepestZ)
                    {
                        deepestZ = smoothedPoints[i].X;
                        deepestX = smoothedPoints[i].X;
                        maxZIndex = i; // mets à jour maxZIndex basé sur smoothedPoints
                    }
                }

                float refX = 50 + deepestX * 0.1f;
                fixedDeepestXPixel = refX; // 🟢 stocké pour le MouseMove

                using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(redPen, refX, 0, refX, sideView.Height);
                }

                g.DrawString($"Deepest Z: {deepestZ:F0} mm", new Font("Arial", 9), Brushes.White, refX + 5, 10);
            }

            lastSmoothedSpinePoints = smoothedPoints;
            sideBox.Image = sideView;
        }

        private List<System.Drawing.PointF> InterpolateSpinePoints(List<System.Drawing.PointF> points)
        {
            List<System.Drawing.PointF> interpolated = new List<System.Drawing.PointF>();

            for (int i = 0; i < points.Count - 3; i++)
            {
                System.Drawing.PointF p0 = points[i];
                System.Drawing.PointF p1 = points[i + 1];
                System.Drawing.PointF p2 = points[i + 2];
                System.Drawing.PointF p3 = points[i + 3];

                for (float t = 0; t <= 1; t += 0.05f)
                {
                    float t2 = t * t;
                    float t3 = t2 * t;

                    float x =
                        0.5f * ((2 * p1.X) +
                        (-p0.X + p2.X) * t +
                        (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                        (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

                    float y =
                        0.5f * ((2 * p1.Y) +
                        (-p0.Y + p2.Y) * t +
                        (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                        (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

                    interpolated.Add(new System.Drawing.PointF(x, y));
                }
            }

            return interpolated;
        }

        ushort[] SmoothDepthData(ushort[] depthData, int width, int height)
        {
            ushort[] smoothed = new ushort[depthData.Length];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    if (depthData[index] == 0) continue;

                    // Average nearby pixels
                    ushort sum = 0;
                    int count = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int neighborIndex = (y + dy) * width + (x + dx);
                            if (depthData[neighborIndex] > 0)
                            {
                                sum += depthData[neighborIndex];
                                count++;
                            }
                        }
                    }
                    smoothed[index] = (ushort)(sum / Math.Max(1, count));
                }
            }
            return smoothed;
        }

        private void SagittalBtn_Click(object sender, EventArgs e)
        {
            var multiFrame = multiSourceFrameReader.AcquireLatestFrame();
            if (multiFrame == null) return;

            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame == null) return;

                int width = depthFrame.FrameDescription.Width;
                int height = depthFrame.FrameDescription.Height;

                ushort[] depthData = new ushort[width * height];
                depthFrame.CopyFrameDataToArray(depthData);

                ushort[] smooth = SmoothDepthData(depthData, width, height);
                DrawDepthSpineCurve(smooth); // 🎯 appelle uniquement ici !
            }
        }


        private void SideBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (lastSmoothedSpinePoints == null || lastSmoothedSpinePoints.Count == 0 || maxZIndex < 0 || maxZIndex >= lastSmoothedSpinePoints.Count)
                return;

            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);
            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);

                // 🔁 1. Redessiner la courbe depuis lastSmoothedSpinePoints
                using (Pen pen = new Pen(Color.Cyan, 3))
                {
                    for (int i = 1; i < lastSmoothedSpinePoints.Count; i++)
                    {
                        float x1 = 50 + lastSmoothedSpinePoints[i - 1].X * 0.1f;
                        float y1 = lastSmoothedSpinePoints[i - 1].Y;
                        float x2 = 50 + lastSmoothedSpinePoints[i].X * 0.1f;
                        float y2 = lastSmoothedSpinePoints[i].Y;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }

                // 🔁 2. Ligne rouge fixe (Z le plus profond)
                if (fixedDeepestXPixel > 0)
                {
                    using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(redPen, fixedDeepestXPixel, 0, fixedDeepestXPixel, sideView.Height);
                    }

                    float zRef = lastSmoothedSpinePoints[maxZIndex].X;
                    g.DrawString($"Deepest Z: {zRef:F0} mm", new Font("Arial", 9), Brushes.White, fixedDeepestXPixel + 5, 10);
                }

                // 🔁 3. Affichage de la distance si curseur proche
                float minDistance = 10f;
                System.Drawing.PointF? closestPoint = null;

                foreach (var pt in lastSmoothedSpinePoints)
                {
                    float x = 50 + pt.X * 0.1f;
                    float y = pt.Y;

                    float dx = e.X - x;
                    float dy = e.Y - y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestPoint = pt;
                    }
                }

                if (closestPoint != null)
                {
                    float zPoint = closestPoint.Value.X;
                    float zRef = lastSmoothedSpinePoints[maxZIndex].X;
                    float lateralDistance = Math.Abs(zPoint - zRef);

                    float x = 50 + closestPoint.Value.X * 0.1f;
                    float y = closestPoint.Value.Y;

                    string label = $"Z: {zPoint:F1} mm\nDécalage: {lateralDistance:F1} mm";
                    g.DrawString(label, new Font("Arial", 9), Brushes.Yellow, x + 5, y - 25);
                    g.FillEllipse(Brushes.Yellow, x - 3, y - 3, 6, 6);
                }
            }

            sideBox.Image?.Dispose();
            sideBox.Image = sideView;
        }



        private List<System.Drawing.PointF> FilterDepthPoints(List<System.Drawing.PointF> points)
        {
            List<System.Drawing.PointF> filtered = new List<System.Drawing.PointF>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                float x = (points[i - 1].X + points[i].X + points[i + 1].X) / 3f;
                float y = points[i].Y; // garde Y intact
                filtered.Add(new System.Drawing.PointF(x, y));
            }
            return filtered;
        }


        private void DrawFixedDeepestLine(Graphics g, List<System.Drawing.PointF> spinePoints, int maxZIdx)
        {
            if (spinePoints == null || spinePoints.Count == 0 || maxZIdx < 0 || maxZIdx >= spinePoints.Count)
                return;

            float xZ = spinePoints[maxZIdx].X;
            float xPixel = 50 + xZ * 0.1f;

            using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                g.DrawLine(redPen, xPixel, 0, xPixel, sideBox.Height);
            }

            g.DrawString($"Deepest Z: {xZ:F0} mm", new Font("Arial", 9), Brushes.White, xPixel + 5, 10);
        }



    }



}
