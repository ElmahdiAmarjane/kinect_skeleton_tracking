﻿
using kinectProject;
using Microsoft.Kinect;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;  // for ImageFormat
using System.IO;
using System.Linq;
using System.Numerics; // pour Vector3
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;



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
        private PictureBox infoBox;
        private PictureBox angleSpineBox;
        private PictureBox realAngleCobb;
        private float cobbAngleV2; 

        private List<System.Drawing.PointF> lastSmoothedPoints = new List<System.Drawing.PointF>();

        private List<System.Drawing.PointF> lastSmoothedSpinePoints = new List<System.Drawing.PointF>();

        // En haut de la classe Form1 :
        private int maxZIndex = -1;

        private float fixedDeepestXPixel = -1;  // ← position en pixels sur le sideBox (avec échelle)
        //
        private double spineAngle;

        ////////////////:
        ///

        private float _lineX = -1; // X position of the line in pixels
        private bool _isDraggingLine = false;
        private const int _lineGrabMargin = 10; // sensitivity zone for grabbing the line


        ///////////////
        /// <summary>
        /// 
        /// </summary>
        ComboBox jointSelector1 = new ComboBox();
        ComboBox jointSelector2 = new ComboBox();
        Label depthDiffLabel = new Label();
        /////////////////////
        ///
        private TextBox patientNameTextBox;
        private TextBox patientAgeTextBox;
        private Button generatePdfBtn;

        public Form1()
        {
           
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // === Initialize Kinect ===
                kinectSensor = KinectSensor.GetDefault();
                if (kinectSensor == null)
                {
                    MessageBox.Show("Aucun capteur Kinect détecté.", "Erreur Kinect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Application.Exit();
                    return;
                }

                kinectSensor.Open();
               // kinectSensor.IsAvailableChanged -= KinectSensor_IsAvailableChanged;
                //kinectSensor.IsAvailableChanged += KinectSensor_IsAvailableChanged;
                coordinateMapper = kinectSensor.CoordinateMapper;

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body);
                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameReader_MultiSourceFrameArrived;

                depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
                depthPixels = new byte[512 * 424 * 4];

                // === Main depth view PictureBox ===
                depthPictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(30, 30, 30),
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom
                };
                this.Controls.Add(depthPictureBox);
                depthPictureBox.MouseClick += DepthPictureBox_MouseClick;

                // === Right Panel (sideBox + infoBox) ===
                Panel rightPanel = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = 370,
                    BackColor = Color.Black
                };
                this.Controls.Add(rightPanel);

                // Container panel to manage layout
                Panel sideContainer = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black
                };
                rightPanel.Controls.Add(sideContainer);

                sideBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(30, 30, 30),
                    BorderStyle = BorderStyle.FixedSingle
                };
                sideContainer.Controls.Add(sideBox);
                sideBox.MouseMove += SideBox_MouseMove;

                infoBox = new PictureBox
                {
                    Height = 150,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Transparent,
                    Visible = true
                };
                sideContainer.Controls.Add(infoBox);

                angleSpineBox = new PictureBox
                {
                    Height = 50,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Transparent,
                    Visible = true
                };
                infoBox.Controls.Add(angleSpineBox);
                /////////////////////


                realAngleCobb = new PictureBox
                {
                    Height = 50,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Red,
                    Visible = true
                };
                infoBox.Controls.Add(realAngleCobb);

                /////////////////////
                // === Top panel with controls ===
                Panel topPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 60,
                    BackColor = Color.FromArgb(64, 64, 64),
                    Padding = new Padding(10)
                };
                this.Controls.Add(topPanel);

                TableLayoutPanel controlLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 9,
                    RowCount = 1,
                    BackColor = Color.Transparent
                };
                controlLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
                controlLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
                topPanel.Controls.Add(controlLayout);

                //Button captureBtn = new Button
                //{
                //    Text = "Capturer Vue 3D",
                //    Dock = DockStyle.Fill,
                //    Height = 36,
                //    BackColor = Color.FromArgb(76, 175, 80),
                //    ForeColor = Color.White,
                //    FlatStyle = FlatStyle.Flat,
                //    Margin = new Padding(2)
                //};
                //captureBtn.FlatAppearance.BorderSize = 0;
                //captureBtn.Click += (s, args) =>
                //{
                //    string label = Microsoft.VisualBasic.Interaction.InputBox("Nom de la vue (ex: face, gauche...)", "Nom vue", "face");
                //    if (string.IsNullOrWhiteSpace(label)) return;
                //    string fileName = $"capture_{label}_{DateTime.Now:HHmmss}.ply";
                //    CapturePointCloud(fileName);
                //};
                //controlLayout.Controls.Add(captureBtn, 0, 0);

                jointSelector1.DropDownStyle = ComboBoxStyle.DropDownList;
                jointSelector1.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector1.SelectedIndex = 0;
                jointSelector1.Dock = DockStyle.Fill;
                jointSelector1.BackColor = Color.FromArgb(50, 50, 50);
                jointSelector1.ForeColor = Color.White;
                jointSelector1.FlatStyle = FlatStyle.Flat;
                controlLayout.Controls.Add(jointSelector1, 1, 0);

                jointSelector2.DropDownStyle = ComboBoxStyle.DropDownList;
                jointSelector2.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector2.SelectedIndex = 1;
                jointSelector2.Dock = DockStyle.Fill;
                jointSelector2.BackColor = Color.FromArgb(50, 50, 50);
                jointSelector2.ForeColor = Color.White;
                jointSelector2.FlatStyle = FlatStyle.Flat;
                controlLayout.Controls.Add(jointSelector2, 2, 0);

                depthDiffLabel.Text = "Depth Difference: - mm";
                depthDiffLabel.Dock = DockStyle.Fill;
                depthDiffLabel.ForeColor = Color.White;
                depthDiffLabel.TextAlign = ContentAlignment.MiddleLeft;
                controlLayout.Controls.Add(depthDiffLabel, 3, 0);

                Button sagittalBtn = new Button
                {
                    Text = "Capturer Courbe Sagittale",
                    Dock = DockStyle.Fill,
                    Height = 36,
                    BackColor = Color.FromArgb(33, 150, 243),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2)
                };
                sagittalBtn.FlatAppearance.BorderSize = 0;
                sagittalBtn.Click += SagittalBtn_Click;
                controlLayout.Controls.Add(sagittalBtn, 4, 0);

                Button exportBtn = new Button
                {
                    Text = "Exporter Courbe PNG",
                    Dock = DockStyle.Fill,
                    Height = 36,
                    BackColor = Color.FromArgb(255, 193, 7),
                    ForeColor = Color.Black,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2)
                };
                exportBtn.FlatAppearance.BorderSize = 0;
                exportBtn.Click += ExportCurveBtn_Click;
                controlLayout.Controls.Add(exportBtn, 5, 0);

                Button toggleInfoBtn = new Button
                {
                    Text = "Afficher Info",
                    Dock = DockStyle.Fill,
                    Height = 36,
                    BackColor = Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2)
                };
                toggleInfoBtn.Click += (s, args) =>
                {
                    infoBox.Visible = !infoBox.Visible;
                    infoBox.Parent.PerformLayout();
                    sideBox.Refresh();
                };
                controlLayout.Controls.Add(toggleInfoBtn, 6, 0);
                //////////////////

                Button generatePdfButton = new Button
                {
                    Text = "Générer PDF",
                    Dock = DockStyle.Fill,
                    Height = 36,
                    BackColor = Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2)
                };
                generatePdfButton.Click += GeneratePdfButton_Click;

                controlLayout.Controls.Add(generatePdfButton, 7, 0);
                /////////////////////////
                ///
                Button btnOpenBodyAnalyzer = new Button
                {
                    Text = "Analyser une Image",
                    Dock = DockStyle.Fill,
                    Height = 36,
                    BackColor = Color.Gray,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(2)
                };
                // Add the click event handler:
                btnOpenBodyAnalyzer.Click += BtnOpenBodyAnalyzer_Click;

                // Add button to your layout
                controlLayout.Controls.Add(btnOpenBodyAnalyzer, 0, 0);









                /////////////////
                StatusStrip statusStrip = new StatusStrip
                {
                    Dock = DockStyle.Bottom,
                    BackColor = Color.FromArgb(64, 64, 64),
                    ForeColor = Color.White
                };
                ToolStripStatusLabel statusLabel = new ToolStripStatusLabel
                {
                    Text = "Veuillez vous placer à 1-2 mètres du capteur pour une détection optimale.",
                    ForeColor = Color.White
                };
                statusStrip.Items.Add(statusLabel);
                this.Controls.Add(statusStrip);

                this.BackColor = Color.FromArgb(45, 45, 45);
                this.Text = "Kinect Body Analysis Pro";
                this.Font = new System.Drawing.Font("Segoe UI", 9f, FontStyle.Regular);
                this.AutoScaleMode = AutoScaleMode.Dpi;
                this.DoubleBuffered = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    

        private void KinectSensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)(() =>
            {
                if (!e.IsAvailable)
                {
                    MessageBox.Show("Connexion perdue avec le capteur Kinect.", "Alerte", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }));
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
                //
                spineAngle = CalculateSpineAngle(trackedBody);
                DrawSpineAngleInInfoBox(spineAngle);

                //
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
                new System.Drawing.Rectangle(0, 0, width, height),
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
            var gaussianed = GaussianSmooth(filtered, 5, 2.0);
            List<System.Drawing.PointF> smoothedPoints = InterpolateSpinePoints(gaussianed);

            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;


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

                g.DrawString($"Deepest Z: {deepestZ:F0} mm", new System.Drawing.Font("Arial", 9), Brushes.White, refX + 5, 10);

                float cobbAngle = CalculateCobbAngle(smoothedPoints);
                string interpretation = InterpretCobbAngle(cobbAngle);
                ShowCobbInfo(cobbAngle, interpretation);
                /////////////////
                cobbAngleV2 = CalculateCobbAngleV2(smoothedPoints);
                ShowCobbInfoV2(cobbAngleV2);


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

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

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
                    g.DrawString($"Deepest Z: {zRef:F0} mm", new System.Drawing.Font("Arial", 9), Brushes.White, fixedDeepestXPixel + 5, 10);
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
                    g.DrawString(label, new System.Drawing.Font("Arial", 9), Brushes.Yellow, x + 5, y - 25);
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

            g.DrawString($"Deepest Z: {xZ:F0} mm", new System.Drawing.Font("Arial", 9), Brushes.White, xPixel + 5, 10);
        }


        //13/07
        List<System.Drawing.PointF> GaussianSmooth(List<System.Drawing.PointF> raw, int radius = 3, double sigma = 1.0)
        {
            int len = raw.Count;
            var smoothed = new List<System.Drawing.PointF>(len);

            // Build Gaussian kernel
            var kernel = new double[2 * radius + 1];
            double sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-0.5 * (i * i) / (sigma * sigma));
                kernel[i + radius] = v;
                sum += v;
            }
            for (int i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            // Convolve
            for (int i = 0; i < len; i++)
            {
                double accum = 0;
                double weight = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int idx = i + k;
                    if (idx < 0 || idx >= len) continue;
                    accum += raw[idx].X * kernel[k + radius];
                    weight += kernel[k + radius];
                }
                // Keep original Y
                smoothed.Add(new System.Drawing.PointF((float)(accum / weight), raw[i].Y));
            }
            return smoothed;
        }


        /////////////::
        private void ExportCurveBtn_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image|*.png";
                sfd.Title = "Enregistrer Courbe Sagittale";
                sfd.FileName = $"SpineCurve_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    ExportSpineCurveHighRes(sfd.FileName, 1920, 1080);
                    MessageBox.Show($"Courbe enregistrée : {sfd.FileName}",
                                    "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        ////////////////////
        ///

        private void ExportSpineCurveHighRes(string filePath, int targetWidth, int targetHeight)
        {
            if (lastSmoothedSpinePoints == null || lastSmoothedSpinePoints.Count < 2)
                return;

            using (var bmp = new Bitmap(targetWidth, targetHeight))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                // Match UI values exactly
                float offsetX = 50f;
                float scaleX = 0.1f;
                float scaleY = targetHeight / 424f;

                // 1. Draw curve
                using (Pen spinePen = new Pen(Color.Cyan, 4))
                {
                    for (int i = 1; i < lastSmoothedSpinePoints.Count; i++)
                    {
                        var p1 = lastSmoothedSpinePoints[i - 1];
                        var p2 = lastSmoothedSpinePoints[i];

                        float x1 = offsetX + p1.X * scaleX;
                        float y1 = p1.Y * scaleY;
                        float x2 = offsetX + p2.X * scaleX;
                        float y2 = p2.Y * scaleY;

                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                // 2. Vertical red line aligned with deepest point
                if (maxZIndex >= 0 && maxZIndex < lastSmoothedSpinePoints.Count)
                {
                    float deepestZ = lastSmoothedSpinePoints[maxZIndex].X;
                    float xDeep = offsetX + deepestZ * scaleX;

                    using (Pen redDash = new Pen(Color.Red, 3) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(redDash, xDeep, 0, xDeep, targetHeight);
                    }

                    using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 20, FontStyle.Bold))
                    {
                        g.DrawString($"Profondeur max : {deepestZ:F0} mm", font, Brushes.White, xDeep + 10, 20);
                    }
                }

                bmp.Save(filePath, ImageFormat.Png);
            }
        }


        private float CalculateCobbAngle(List<System.Drawing.PointF> spinePoints)
        {
            if (spinePoints == null || spinePoints.Count < 10) return 0;

            // On prend le haut et le bas de la courbe (par ex. 10%)
            int offset = spinePoints.Count / 10;
            System.Drawing.PointF top = spinePoints[offset];
            System.Drawing.PointF bottom = spinePoints[spinePoints.Count - 1 - offset];

            // Vecteurs par rapport à l’axe vertical
            var vec1 = new System.Drawing.PointF(0, 1); // axe vertical
            var vec2 = new System.Drawing.PointF(bottom.X - top.X, bottom.Y - top.Y);

            // Produit scalaire et angle
            float dot = vec1.X * vec2.X + vec1.Y * vec2.Y;
            float mag1 = (float)Math.Sqrt(vec1.X * vec1.X + vec1.Y * vec1.Y);
            float mag2 = (float)Math.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y);

            float angleRad = (float)Math.Acos(dot / (mag1 * mag2));
            float angleDeg = angleRad * 180f / (float)Math.PI;

            return angleDeg;
        }




        private string InterpretCobbAngle(float angleDeg)
        {
            if (angleDeg < 10) return "Pas de scoliose détectée.";
            else if (angleDeg < 20) return "Scoliose légère.";
            else if (angleDeg < 40) return "Scoliose modérée.";
            else return "Scoliose sévère. Suivi médical recommandé.";
        }


        private void ShowCobbInfo(float angle, string interpretation)
        {
            if (infoBox == null) return;

            // ✅ Évite les erreurs dues à taille invalide
            int w = infoBox.Width;
            int h = infoBox.Height;
            if (w <= 0 || h <= 0) return;

            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(30, 30, 30));

                using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 8, FontStyle.Regular))
                using (System.Drawing.Font fontSmall = new System.Drawing.Font("Segoe UI", 8))
                {
                    g.DrawString($"Angle d’inclinaison vertébrale : {angle:F1}°", font, Brushes.LightGreen, 10, 10);
                    g.DrawString($"Analyse : {interpretation}", fontSmall, Brushes.White, 10, 30);
                }
            }

            infoBox.Image?.Dispose(); // ✅ Nettoyage ancien bitmap
            infoBox.Image = bmp;
        }

        private void ShowCobbInfoV2(float angle)
        {
            if (realAngleCobb == null) return;

            // ✅ Évite les erreurs dues à taille invalide
            int w = realAngleCobb.Width;
            int h = realAngleCobb.Height;
            if (w <= 0 || h <= 0) return;

            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(30, 30, 30));

                using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 8, FontStyle.Regular))
                using (System.Drawing.Font fontSmall = new System.Drawing.Font("Segoe UI", 8))
                {
                    g.DrawString($"Angle de Cobb V2 : {angle:F1}°", font, Brushes.LightGreen, 20, 10);
                  //  g.DrawString($"Analyse : {interpretation}", fontSmall, Brushes.White, 10, 30);
                }
            }

            realAngleCobb.Image?.Dispose(); // ✅ Nettoyage ancien bitmap
            realAngleCobb.Image = bmp;
        }

        private void DrawSpineAngleInInfoBox(double angle)
        {
            if (angleSpineBox == null) return;

            Bitmap infoBitmap = new Bitmap(angleSpineBox.Width, angleSpineBox.Height);
            using (Graphics g = Graphics.FromImage(infoBitmap))
            {
                g.Clear(Color.FromArgb(30, 30, 30)); // Fond sombre pour meilleure visibilité
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                string angleText = $"Angle sagittal du tronc: {angle:F2}°";
                using (System.Drawing.Font font = new System.Drawing.Font("Arial", 8, FontStyle.Regular))
                {
                    g.DrawString(angleText, font, Brushes.LightGreen, new System.Drawing.PointF(10, 10));
                }
            }
            angleSpineBox.Image?.Dispose(); // Libérer ancienne image
            angleSpineBox.Image = infoBitmap;
            angleSpineBox.Invalidate(); // Forcer rafraîchissement
        }

        private double CalculateSpineAngle(Body body)
        {
            if (body == null || !body.IsTracked) return double.NaN;

            Joint shoulder = body.Joints[JointType.ShoulderLeft];
            Joint spineMid = body.Joints[JointType.SpineMid];
            Joint spineBase = body.Joints[JointType.SpineBase];

            if (shoulder.TrackingState == TrackingState.NotTracked ||
                spineMid.TrackingState == TrackingState.NotTracked ||
                spineBase.TrackingState == TrackingState.NotTracked)
                return double.NaN;

            // Vecteurs
            Vector3 vector1 = new Vector3(
                spineMid.Position.X - shoulder.Position.X,
                spineMid.Position.Y - shoulder.Position.Y,
                spineMid.Position.Z - shoulder.Position.Z
            );

            Vector3 vector2 = new Vector3(
                spineBase.Position.X - spineMid.Position.X,
                spineBase.Position.Y - spineMid.Position.Y,
                spineBase.Position.Z - spineMid.Position.Z
            );

            // Produit scalaire + angle
            float dot = Vector3.Dot(vector1, vector2);
            float mag1 = vector1.Length();
            float mag2 = vector2.Length();
            double angleRadians = Math.Acos(dot / (mag1 * mag2));
            double angleDegrees = angleRadians * (180.0 / Math.PI);

            return Math.Round(angleDegrees, 1);
        }






        ////////////////////////////////////////////
        ///////////////////////////////////////////


        private void GeneratePdfButton_Click(object sender, EventArgs e)
        {
            using (PdfInputForm inputForm = new PdfInputForm())
            {
                if (inputForm.ShowDialog() == DialogResult.OK)
                {
                    System.Drawing.Image imageToInclude = depthPictureBox?.Image; // ou n'importe quelle autre image disponible
                    GeneratePatientReport(inputForm, imageToInclude);
                }
            }
        }

        private void GeneratePatientReport(PdfInputForm form, System.Drawing.Image imageToInclude)
        {
            try
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = "Rapport Médical Patient";

                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XFont titleFont = new XFont("Segoe UI", 18, XFontStyle.Bold);
                XFont labelFont = new XFont("Segoe UI", 12, XFontStyle.Bold);
                XFont valueFont = new XFont("Segoe UI", 12, XFontStyle.Regular);

                double margin = 40;
                double yPoint = margin;
                double pageHeight = page.Height;

                void CheckPageOverflow(double requiredHeight)
                {
                    if (yPoint + requiredHeight > pageHeight - margin)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPoint = margin;
                    }
                }

                // 🟦 Titre
                gfx.DrawString("Rapport d'analyse posturale", titleFont, XBrushes.DarkBlue,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 40), XStringFormats.TopCenter);
                yPoint += 50;

                // 🟦 Infos patient
                gfx.DrawString("Informations du patient", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 25;

                string[] patientInfo = new[]
                {
            $"Nom : {form.PatientName}",
            $"Âge : {form.PatientAge}",
            $"Sexe : {form.PatientSex}",
            $"Date de naissance : {form.PatientBirthDate.ToShortDateString()}",
            $"N° Dossier médical : {form.MedicalRecordNumber}"
        };

                foreach (var info in patientInfo)
                {
                    gfx.DrawString(info, valueFont, XBrushes.Black, margin, yPoint);
                    yPoint += 20;
                }

                // 🟦 Antécédents médicaux
                gfx.DrawString("Antécédents médicaux :", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 20;

              

                XTextFormatter tf = new XTextFormatter(gfx);
                XRect historyRect = new XRect(margin, yPoint, page.Width - 2 * margin, 80);
                tf.DrawString(form.MedicalHistory, valueFont, XBrushes.Black, historyRect, XStringFormats.TopLeft);
                yPoint += 100;

                gfx.DrawString("Resultats Analyse :", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 20;

                gfx.DrawString($"Angle de Cobb V2 : {cobbAngleV2:F1}°", valueFont, XBrushes.Black, margin, yPoint);
                yPoint += 20;

                // 🖼️ Première image
                if (imageToInclude != null)
                {
                    CheckPageOverflow(300); // estimate image height
                    yPoint = DrawImage(gfx, imageToInclude, page, margin, yPoint);
                }

                // 🖼️ Texte avant spline
                CheckPageOverflow(40);
                gfx.DrawString("Courbe spline (courbure du dos)", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 20;

                // 🖼️ Deuxième image spline
                System.Drawing.Image splineImg = GenerateSpineCurveImageForPdf(500, 600);
                if (splineImg != null)
                {
                    CheckPageOverflow(300);
                    yPoint = DrawImage(gfx, splineImg, page, margin, yPoint);
                }

                // 📝 Sauvegarde
                string filename = $"rapport_{form.PatientName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), filename);
                document.Save(fullPath);
                document.Close();

                MessageBox.Show($"PDF généré avec succès à l’emplacement :\n\n{fullPath}", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la génération du PDF :\n" + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private double DrawImage(XGraphics gfx, System.Drawing.Image image, PdfPage page, double margin, double yPoint)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                XImage xImg = XImage.FromStream(ms);

                double maxWidth = page.Width - 2 * margin;
                double maxHeight = page.Height - yPoint - margin;

                double imgRatio = (double)xImg.PixelWidth / xImg.PixelHeight;
                double targetWidth = maxWidth;
                double targetHeight = targetWidth / imgRatio;

                if (targetHeight > maxHeight)
                {
                    targetHeight = maxHeight;
                    targetWidth = targetHeight * imgRatio;
                }

                gfx.DrawImage(xImg, margin, yPoint, targetWidth, targetHeight);
                return yPoint + targetHeight + 20;
            }
        }


        private System.Drawing.Image GenerateSpineCurveImageForPdf(int width, int height)
        {
            if (lastSmoothedSpinePoints == null || lastSmoothedSpinePoints.Count < 2)
                return null;

            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                float offsetX = 50f;
                float scaleX = 0.1f;
                float scaleY = height / 424f;

                using (Pen spinePen = new Pen(Color.Cyan, 4))
                {
                    for (int i = 1; i < lastSmoothedSpinePoints.Count; i++)
                    {
                        var p1 = lastSmoothedSpinePoints[i - 1];
                        var p2 = lastSmoothedSpinePoints[i];

                        float x1 = offsetX + p1.X * scaleX;
                        float y1 = p1.Y * scaleY;
                        float x2 = offsetX + p2.X * scaleX;
                        float y2 = p2.Y * scaleY;

                        g.DrawLine(spinePen, x1, y1, x2, y2);
                    }
                }

                if (maxZIndex >= 0 && maxZIndex < lastSmoothedSpinePoints.Count)
                {
                    float deepestZ = lastSmoothedSpinePoints[maxZIndex].X;
                    float xDeep = offsetX + deepestZ * scaleX;

                    using (Pen redDash = new Pen(Color.Red, 3) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    {
                        g.DrawLine(redDash, xDeep, 0, xDeep, height);
                    }

                    using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 20, FontStyle.Bold))
                    {
                        g.DrawString($"Profondeur max : {deepestZ:F0} mm", font, Brushes.White, xDeep + 10, 20);
                    }
                }
            }

            return bmp;
        }



      
// Define the click event method somewhere in your form class:
private void BtnOpenBodyAnalyzer_Click(object sender, EventArgs e)
        {
            // Create an instance of the BodyPictureAnalyzer form
            BodyPictureAnalyzer bodyAnalyzerForm = new BodyPictureAnalyzer();

            // Show it as a new window (non-modal)
            bodyAnalyzerForm.Show();

            // Or if you want it modal (block main window until closed), use:
            // bodyAnalyzerForm.ShowDialog();
        }


        private float CalculateCobbAngleV2(List<System.Drawing.PointF> spinePoints)
        {
            if (spinePoints == null || spinePoints.Count < 10)
                return 0;

            int count = spinePoints.Count;
            int segmentSize = count / 4; // prend environ 25% du haut et du bas

            // ---- Partie haute (supérieure de la colonne) ----
            var topSegment = spinePoints.Take(segmentSize).ToList();
            var topLine = FitLine(topSegment); // Retourne un vecteur directeur

            // ---- Partie basse (inférieure de la colonne) ----
            var bottomSegment = spinePoints.Skip(count - segmentSize).ToList();
            var bottomLine = FitLine(bottomSegment); // Retourne un vecteur directeur

            // ---- Calcul de l’angle entre les deux vecteurs ----
            float dot = topLine.X * bottomLine.X + topLine.Y * bottomLine.Y;
            float magTop = (float)Math.Sqrt(topLine.X * topLine.X + topLine.Y * topLine.Y);
            float magBottom = (float)Math.Sqrt(bottomLine.X * bottomLine.X + bottomLine.Y * bottomLine.Y);

            float angleRad = (float)Math.Acos(dot / (magTop * magBottom));
            float angleDeg = angleRad * 180f / (float)Math.PI;

            return angleDeg;
        }

        private System.Drawing.PointF FitLine(List<System.Drawing.PointF> points)
        {
            if (points == null || points.Count < 2)
                return new System.Drawing.PointF(0, 1); // vertical par défaut

            float sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = points.Count;

            foreach (var p in points)
            {
                sumX += p.X;
                sumY += p.Y;
                sumXY += p.X * p.Y;
                sumX2 += p.X * p.X;
            }

            float meanX = sumX / n;
            float meanY = sumY / n;
            float denominator = sumX2 - sumX * meanX;

            // éviter division par zéro
            if (Math.Abs(denominator) < 1e-6)
                return new System.Drawing.PointF(0, 1); // vertical

            float slope = (sumXY - sumX * meanY) / denominator;

            // vecteur directeur basé sur la pente
            return new System.Drawing.PointF(1, slope);
        }

    }



}
