
using kinectProject;
using Microsoft.Kinect;
using Newtonsoft.Json; 
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
using System.Drawing.Drawing2D;

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

        private PictureBox depthPictureBox; // 
        private PictureBox normalPictureBox; // 
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

        private Bitmap lastDepthImage;

        /////////////////////////////
        ///

        private bool isDraggingRefLine = false;
        private float manualZRef = -1; // to store manually set Z reference



        //
        // Horizontal and vertical detection area (in pixels, Kinect depth frame = 512x424)
        private const int ROI_X = 150;       // Start X (left boundary)
        private const int ROI_WIDTH = 250;   // Width of the ROI
        private const int ROI_Y = 5;        // Start Y (top boundary)
        private const int ROI_HEIGHT = 400;  // Height of the ROI


        //
        private Bitmap colorBitmap;
        private byte[] colorPixels;
       

        // Ajoutez en haut avec les autres constantes
        private const int EROSION_KERNEL_SIZE = 2;
        private const int DILATION_KERNEL_SIZE = 3;
        private const int MIN_BLOB_SIZE = 500;
        private Bitmap _displayBuffer;
        private readonly object _bufferLock = new object();
       //



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

                coordinateMapper = kinectSensor.CoordinateMapper;

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);
                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameReader_MultiSourceFrameArrived;

                depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
                depthPixels = new byte[512 * 424 * 4];

                colorBitmap = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
                colorPixels = new byte[1920 * 1080 * 4];

                // === Main depth view PictureBox ===
                depthPictureBox = new PictureBox
                {
                    Width = 450,
                    Dock = DockStyle.Left,
                    BackColor = Color.FromArgb(25, 25, 40),
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Padding = new Padding(5)
                };
                this.Controls.Add(depthPictureBox);
                depthPictureBox.MouseClick += DepthPictureBox_MouseClick;

                // === Color view PictureBox ===
                normalPictureBox = new PictureBox
                {
                    Width = 450,
                    Dock = DockStyle.Right,
                    BackColor = Color.FromArgb(25, 25, 40),
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Padding = new Padding(5)
                };
                this.Controls.Add(normalPictureBox);

                // === Right Panel (sideBox + infoBox) ===
                Panel rightPanel = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = 300,
                    BackColor = Color.FromArgb(15, 15, 25)
                };
                this.Controls.Add(rightPanel);

                // Container panel to manage layout
                Panel sideContainer = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(15, 15, 25)
                };
                rightPanel.Controls.Add(sideContainer);

                sideBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(20, 20, 30),
                    BorderStyle = BorderStyle.FixedSingle
                };
                sideContainer.Controls.Add(sideBox);
                sideBox.MouseMove += SideBox_MouseMove;

                infoBox = new PictureBox
                {
                    Height = 120,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Transparent,
                    Visible = true
                };
                sideContainer.Controls.Add(infoBox);

                angleSpineBox = new PictureBox
                {
                    Height = 40,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Transparent,
                    Visible = true
                };
                infoBox.Controls.Add(angleSpineBox);

                realAngleCobb = new PictureBox
                {
                    Height = 40,
                    Dock = DockStyle.Bottom,
                    BackColor = Color.Transparent,
                    Visible = true
                };
                infoBox.Controls.Add(realAngleCobb);
                // === Top panel with controls ===
                Panel topPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 80, // Slightly taller to accommodate better button layout
                    BackColor = Color.FromArgb(30, 30, 45),
                    Padding = new Padding(8, 12, 8, 8)
                };
                this.Controls.Add(topPanel);

                // Create a more organized flow layout for buttons
                FlowLayoutPanel buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Padding = new Padding(0, 5, 0, 0)
                };
                topPanel.Controls.Add(buttonPanel);

                // Professional color scheme
                Color primaryColor = Color.FromArgb(65, 105, 225);    // Royal Blue
                Color secondaryColor = Color.FromArgb(50, 205, 50);   // Lime Green
                Color accentColor = Color.FromArgb(255, 140, 0);      // Dark Orange
                Color exportColor = Color.FromArgb(138, 43, 226);     // Blue Violet
                Color pdfColor = Color.FromArgb(220, 20, 60);         // Crimson
                Color analyzerColor = Color.FromArgb(30, 144, 255);   // Dodger Blue

                // Helper method to create styled buttons with proper width
                Button CreateStyledButton(string text, Color backColor, EventHandler clickHandler, int minWidth = 90)
                {
                    Button button = new Button
                    {
                        Text = text,
                        BackColor = backColor,
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        FlatAppearance = { BorderSize = 0 },
                        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        Height = 32,
                        MinimumSize = new Size(minWidth, 32),
                        Margin = new Padding(4, 0, 4, 0),
                        Padding = new Padding(4, 0, 4, 0),
                        Cursor = Cursors.Hand,
                        TextAlign = ContentAlignment.MiddleCenter,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink
                    };
                    ApplyRoundedStyle(button, 8);
                    button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.2f);
                    button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.2f);
                    button.Click += clickHandler;

                    return button;
                }

                // Create and style buttons with clearer text and proper widths
                Button btnOpenBodyAnalyzer = CreateStyledButton("📷 Analyser Image", analyzerColor, BtnOpenBodyAnalyzer_Click);
                Button generatePdfButton = CreateStyledButton("📄 Générer PDF", pdfColor, GeneratePdfButton_Click);
                Button btnSaveDepthImage = CreateStyledButton("💾 Depth Image", primaryColor, BtnSaveDepthImage_Click);
                Button btnSaveImage = CreateStyledButton("💾 Color Image", primaryColor, BtnSaveImage_Click);
                Button sagittalBtn = CreateStyledButton("📊 Capturer Courbe", secondaryColor, SagittalBtn_Click);
                Button exportBtn = CreateStyledButton("🖼️ Export PNG", accentColor, ExportCurveBtn_Click);
                Button btnExportData = CreateStyledButton("📁 Export Data", exportColor, BtnExportData_Click);
                Button btnImportData = CreateStyledButton("📂 Import Data", exportColor, BtnImportData_Click);
                Button btnNormalImage = CreateStyledButton("🖼️ Normal Image", exportColor, BtnNormalImage_Click);
                Button toggleInfoBtn = CreateStyledButton("👁️ Afficher Info", Color.Gray, (s, args) =>
                {
                    infoBox.Visible = !infoBox.Visible;
                    infoBox.Parent.PerformLayout();
                    sideBox.Refresh();
                });

                // Create visual separators
                Label CreateSeparator() => new Label
                {
                    Text = "|",
                    ForeColor = Color.FromArgb(100, 100, 120),
                    AutoSize = true,
                    Margin = new Padding(5, 5, 5, 5),
                    Font = new Font("Segoe UI", 11, FontStyle.Bold)
                };

                // Add buttons in logical groups with better spacing
                buttonPanel.Controls.Add(btnOpenBodyAnalyzer);
               
                buttonPanel.Controls.Add(CreateSeparator());
                buttonPanel.Controls.Add(btnSaveDepthImage);
                buttonPanel.Controls.Add(btnSaveImage);
                buttonPanel.Controls.Add(CreateSeparator());
                buttonPanel.Controls.Add(sagittalBtn);
                buttonPanel.Controls.Add(exportBtn);
                buttonPanel.Controls.Add(btnExportData);
                buttonPanel.Controls.Add(btnImportData);
                buttonPanel.Controls.Add(CreateSeparator());
                buttonPanel.Controls.Add(toggleInfoBtn);
                buttonPanel.Controls.Add(generatePdfButton);
                buttonPanel.Controls.Add(btnNormalImage);
                // Add tooltips for better usability
                ToolTip toolTip = new ToolTip();
                toolTip.SetToolTip(btnSaveDepthImage, "Sauvegarder l'image de profondeur");
                toolTip.SetToolTip(btnSaveImage, "Sauvegarder l'image couleur");
                toolTip.SetToolTip(btnExportData, "Exporter les données de courbe (JSON/CSV)");
                toolTip.SetToolTip(exportBtn, "Exporter la courbe en image PNG");
                toolTip.SetToolTip(sagittalBtn, "Capturer la courbe sagittale du dos");
                toolTip.SetToolTip(btnOpenBodyAnalyzer, "Ouvrir l'analyseur d'image corporelle");
                toolTip.SetToolTip(generatePdfButton, "Générer un rapport PDF complet");
                toolTip.SetToolTip(toggleInfoBtn, "Afficher/Masquer les informations");
                toolTip.SetToolTip(btnImportData, "Importer des données de courbe sauvegardées");
                toolTip.SetToolTip(btnNormalImage, "Sauvegarder l'image couleur normal");

                // Event handlers
                sideBox.MouseDown += SideBox_MouseDown;
                sideBox.MouseUp += SideBox_MouseUp;
                sideBox.MouseMove += SideBox_MouseMove;

                // Status strip
                StatusStrip statusStrip = new StatusStrip
                {
                    Dock = DockStyle.Bottom,
                    BackColor = Color.FromArgb(40, 40, 60),
                    ForeColor = Color.White,
                    RenderMode = ToolStripRenderMode.Professional
                };

                ToolStripStatusLabel statusLabel = new ToolStripStatusLabel
                {
                    Text = "Veuillez vous placer à 1-2 mètres du capteur pour une détection optimale.",
                    ForeColor = Color.LightGray,
                    Font = new Font("Segoe UI", 9f)
                };

                ToolStripStatusLabel kinectStatus = new ToolStripStatusLabel
                {
                    Text = "Kinect: Connecté",
                    ForeColor = Color.LightGreen,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Alignment = ToolStripItemAlignment.Right
                };

                statusStrip.Items.Add(statusLabel);
                statusStrip.Items.Add(kinectStatus);
                this.Controls.Add(statusStrip);

                // Main form styling
                this.BackColor = Color.FromArgb(45, 45, 60);
                this.Text = "Kinect Body Analysis Pro - Posture Assessment System";
                this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                this.ForeColor = Color.White;
                this.AutoScaleMode = AutoScaleMode.Dpi;
                this.DoubleBuffered = true;

                // Add context menu for curve management
                ContextMenuStrip curveMenu = new ContextMenuStrip();
                curveMenu.Items.Add("Ouvrir le visualisateur multi-courbes", null, (s, args) =>
                {
                    if (lastSmoothedSpinePoints != null && lastSmoothedSpinePoints.Count > 0)
                    {
                        var currentCurve = new SpineCurveData
                        {
                            CaptureTime = DateTime.Now,
                            Points = lastSmoothedSpinePoints.Select(p => PointFData.FromPointF(p)).ToList(),
                            MaxZIndex = maxZIndex,
                            ManualZRef = manualZRef,
                            FixedDeepestXPixel = fixedDeepestXPixel,
                            SpineAngle = spineAngle
                        };

                        OpenMultiCurveViewer(new List<SpineCurveData> { currentCurve });
                    }
                    else
                    {
                        MessageBox.Show("Aucune courbe active à afficher.", "Information",
                                       MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });

                this.ContextMenuStrip = curveMenu;


                // Add form icon if available
                try
                {
                    // this.Icon = YourIconHere;
                }
                catch { }
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


// Helper method to apply rounded corners
         Button ApplyRoundedStyle(Button btn, int radius = 8)
    {

        // Custom paint to make rounded corners
        btn.Paint += (s, e) =>
        {
            Rectangle rect = btn.ClientRectangle;
            using (GraphicsPath path = new GraphicsPath())
            {
                int r = radius;
                path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                path.CloseAllFigures();
                btn.Region = new Region(path);
            }
        };

        return btn;
    }




        private void MultiSourceFrameReader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if ((DateTime.Now - lastFrameTime).TotalMilliseconds < 1000 / TargetFrameRate)
                return;

            lastFrameTime = DateTime.Now;

            var multiSourceFrame = e.FrameReference.AcquireFrame();
            if (multiSourceFrame == null) return;

            // === FRAME DEPTH + BODY ===
            using (var depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            using (var bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {

                if (depthFrame != null && bodyFrame != null)
                {

                    ProcessDepthFrameWithBodyContext(depthFrame, bodyFrame);
                }
            }

            //// === FRAME COLOR ===
            //using (var colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            //{
            //    if (colorFrame != null)
            //    {
            //        FrameDescription desc = colorFrame.FrameDescription;
            //        if (colorBitmap == null)
            //            colorBitmap = new Bitmap(desc.Width, desc.Height, PixelFormat.Format32bppArgb);

            //        if (colorPixels == null || colorPixels.Length != desc.Width * desc.Height * 4)
            //            colorPixels = new byte[desc.Width * desc.Height * 4];

            //        // Copier pixels
            //        colorFrame.CopyConvertedFrameDataToArray(colorPixels, ColorImageFormat.Bgra);

            //        // Copier dans le bitmap
            //        BitmapData bmpData = colorBitmap.LockBits(
            //            new Rectangle(0, 0, desc.Width, desc.Height),
            //            ImageLockMode.WriteOnly,
            //            PixelFormat.Format32bppArgb);
            //        Marshal.Copy(colorPixels, 0, bmpData.Scan0, colorPixels.Length);
            //        colorBitmap.UnlockBits(bmpData);
            //        Bitmap safeBitmap = (Bitmap)colorBitmap.Clone();



            //        // ✅ Mise à jour UI (sans blocage)
            //        this.BeginInvoke((Action)(() =>
            //        {
            //            Bitmap cropped = CropCenter(safeBitmap, 800, 800); // crop center zone
            //            normalPictureBox.Image?.Dispose();
            //            normalPictureBox.Image = DrawROI(cropped, normalPictureBox);
            //        }));


            //    }
            //}

            // === FRAME COLOR ===
            using (var colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            using (var depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            {
                if (colorFrame != null && depthFrame != null)
                {
                    var aligned = GenerateAlignedColorImage(depthFrame, colorFrame);
                    if (aligned != null)
                    {
                        this.BeginInvoke((Action)(() =>
                        {
                            normalPictureBox.Image?.Dispose();
                            normalPictureBox.Image = aligned;
                        }));
                    }
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

                  
                    if (depth >= minDepth && depth <= maxDepth && trackedBody != null)
                    {
                        // Map depth to hue directly for high sensitivity
                        double normalizedDepth = (depth - minDepth) / (double)(maxDepth - minDepth);
                        normalizedDepth = Math.Max(0.0, Math.Min(1.0, normalizedDepth)); // clamp 0-1

                        // Full hue spectrum mapping
                        Color color = HsvToRgb(normalizedDepth * 360.0, 1.0, 1.0);
                        SetPixelColor(i, color.R, color.G, color.B);
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

        private Color HsvToRgb(double h, double s, double v)
        {
            // Normalize hue to [0, 360)
            h = (h % 360 + 360) % 360;

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r1, g1, b1;

            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);

            return Color.FromArgb(r, g, b);
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
        private void DrawDepthSpineCurve(ushort[] depthData, Body trackedBody)
        {
            int width = 512;
            int height = 424;
            int centerX = width / 2;
            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);

            List<System.Drawing.PointF> rawPoints = new List<System.Drawing.PointF>();
            float maxZ = float.MinValue;
            maxZIndex = -1;

            // --- Get reference joints ---
            CameraSpacePoint neckPos = trackedBody.Joints[JointType.Neck].Position;
            CameraSpacePoint basePos = trackedBody.Joints[JointType.SpineBase].Position;

            DepthSpacePoint neckDepth = coordinateMapper.MapCameraPointToDepthSpace(neckPos);
            DepthSpacePoint baseDepth = coordinateMapper.MapCameraPointToDepthSpace(basePos);

            int startY = (int)Math.Max(0, neckDepth.Y);
            int endY = (int)Math.Min(height - 1, baseDepth.Y);

            if (endY <= startY)
            {
                sideBox.Image = sideView;
                return;
            }

            // --- Sample center columns with median filtering ---
            for (int y = startY; y <= endY; y++)
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

                    zSamples.Add(cp.Z * 1000f); // convert to mm
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

            // --- Smooth & interpolate curve ---
            var filtered = FilterDepthPoints(rawPoints);
            var gaussianed = GaussianSmooth(filtered, 5, 2.0);
            List<System.Drawing.PointF> smoothedPoints = InterpolateSpinePoints(gaussianed);

            // --- Draw curve ---
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

                // --- Find deepest point ---
                float deepestZ = float.MinValue;
                float deepestX = 0;

                for (int i = 0; i < smoothedPoints.Count; i++)
                {
                    if (smoothedPoints[i].X > deepestZ)
                    {
                        deepestZ = smoothedPoints[i].X;
                        deepestX = smoothedPoints[i].X;
                        maxZIndex = i;
                    }
                }

                float refX = 50 + deepestX * 0.1f;
                fixedDeepestXPixel = refX;


                using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(redPen, refX, 0, refX, sideView.Height);
                }

                g.DrawString($"Deepest Z: {deepestZ:F0} mm", new System.Drawing.Font("Arial", 9), Brushes.White, refX + 5, 10);

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
            using (var bodyFrame = multiFrame.BodyFrameReference.AcquireFrame())
            {
                if (depthFrame == null || bodyFrame == null) return;

                int width = depthFrame.FrameDescription.Width;
                int height = depthFrame.FrameDescription.Height;

                ushort[] depthData = new ushort[width * height];
                depthFrame.CopyFrameDataToArray(depthData);

                // Smooth depth data
                ushort[] smooth = SmoothDepthData(depthData, width, height);

                // Get tracked body
                Body[] bodies = new Body[bodyFrame.BodyCount];
                bodyFrame.GetAndRefreshBodyData(bodies);

                Body trackedBody = bodies.FirstOrDefault(b => b.IsTracked);
                if (trackedBody == null) return;

                // ✅ Call updated method
                DrawDepthSpineCurve(smooth, trackedBody);
            }
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



        private void SideBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (lastSmoothedSpinePoints == null || lastSmoothedSpinePoints.Count == 0)
                return;

            // ✅ If dragging, move the reference line
            if (isDraggingRefLine)
            {
                fixedDeepestXPixel = e.X;

                // Convert dragged pixel position back to Z value
                manualZRef = (fixedDeepestXPixel - 50) / 0.1f;
                sideBox.Invalidate();
            }

            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);
            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 1️⃣ Draw spine curve
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

                // 2️⃣ Draw red reference line (auto or manual)
                float zRef = (manualZRef > 0) ? manualZRef : lastSmoothedSpinePoints[maxZIndex].X;
                fixedDeepestXPixel = (manualZRef > 0) ? fixedDeepestXPixel : 50 + zRef * 0.1f;

                using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(redPen, fixedDeepestXPixel, 0, fixedDeepestXPixel, sideView.Height);
                }
                g.DrawString($"Ref Z: {zRef:F0} mm", new Font("Arial", 9), Brushes.White, fixedDeepestXPixel + 5, 10);

                // 3️⃣ Distance from nearest point to reference
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

        private void SideBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (fixedDeepestXPixel > 0 && Math.Abs(e.X - fixedDeepestXPixel) < 10)
            {
                isDraggingRefLine = true;
                this.Cursor = Cursors.SizeWE;
            }
        }

        private void SideBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingRefLine)
            {
                isDraggingRefLine = false;
                this.Cursor = Cursors.Default;
            }
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

                //gfx.DrawString($"Angle de Cobb V2 : {cobbAngleV2:F1}°", valueFont, XBrushes.Black, margin, yPoint);
                //yPoint += 20;

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
            //   bodyAnalyzerForm.Show();

            // Or if you want it modal (block main window until closed), use:
             bodyAnalyzerForm.ShowDialog();

          

        }




        private Bitmap DrawROI(Bitmap image, PictureBox box)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
                return null;

            Bitmap output = new Bitmap(box.Width, box.Height);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, box.Width, box.Height);

                float scaleX = (float)box.Width / 512f;  // Depth base width
                float scaleY = (float)box.Height / 424f; // Depth base height

                using (Pen pen = new Pen(Color.LimeGreen, 2))
                {
                    g.DrawRectangle(pen,
                        ROI_X * scaleX,
                        ROI_Y * scaleY,
                        ROI_WIDTH * scaleX,
                        ROI_HEIGHT * scaleY);
                }
            }
            return output;
        }
        private Bitmap CropCenter(Bitmap source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;

            int x = Math.Max(0, (source.Width - targetWidth) / 2);
            int y = Math.Max(0, (source.Height - targetHeight) / 2);

            targetWidth = Math.Min(targetWidth, source.Width);
            targetHeight = Math.Min(targetHeight, source.Height);

            Rectangle cropArea = new Rectangle(x, y, targetWidth, targetHeight);
            return source.Clone(cropArea, source.PixelFormat);
        }

        private void BtnSaveImage_Click(object sender, EventArgs e)
        {
            try
            {
                if (normalPictureBox.Image == null)
                {
                    MessageBox.Show("No image available to save.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                    sfd.FileName = $"Kinect_Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        normalPictureBox.Image.Save(sfd.FileName);
                        MessageBox.Show("Image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void BtnSaveDepthImage_Click(object sender, EventArgs e)
        {
            try
            {
                if (depthPictureBox.Image == null)
                {
                    MessageBox.Show("No depth image available to save.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                    sfd.FileName = $"Kinect_Depth_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        depthPictureBox.Image.Save(sfd.FileName);
                        MessageBox.Show("Depth image saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving depth image: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }





        private Bitmap GenerateAlignedColorImage(DepthFrame depthFrame, ColorFrame colorFrame)
        {
            if (depthFrame == null || colorFrame == null) return null;

            int depthWidth = depthFrame.FrameDescription.Width;
            int depthHeight = depthFrame.FrameDescription.Height;
            int colorWidth = colorFrame.FrameDescription.Width;
            int colorHeight = colorFrame.FrameDescription.Height;

            // 1. Acquisition des données
            ushort[] depthData = new ushort[depthWidth * depthHeight];
            depthFrame.CopyFrameDataToArray(depthData);

            byte[] colorData = new byte[colorWidth * colorHeight * 4];
            colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Bgra);

            // 2. Création de l'image finale (résolution depth pour correspondance parfaite)
            Bitmap alignedBitmap = new Bitmap(depthWidth, depthHeight, PixelFormat.Format32bppArgb);
            BitmapData bmpData = alignedBitmap.LockBits(
                new Rectangle(0, 0, depthWidth, depthHeight),
                ImageLockMode.WriteOnly,
                alignedBitmap.PixelFormat);

            byte[] alignedPixels = new byte[depthWidth * depthHeight * 4];

            // 3. Mappage des coordonnées depth vers couleur
            ColorSpacePoint[] colorPoints = new ColorSpacePoint[depthWidth * depthHeight];
            coordinateMapper.MapDepthFrameToColorSpace(depthData, colorPoints);

            // 4. Paramètres de détection du corps
            ushort minDepth = BODY_DETECTION_MIN_DEPTH;
            ushort maxDepth = BODY_DETECTION_MAX_DEPTH;

            // 5. Traitement parallèle optimisé
            Parallel.For(0, depthHeight, depthY =>
            {
                for (int depthX = 0; depthX < depthWidth; depthX++)
                {
                    int depthIndex = depthY * depthWidth + depthX;
                    ushort depthValue = depthData[depthIndex];
                    ColorSpacePoint colorPoint = colorPoints[depthIndex];

                    int outputIndex = depthIndex * 4;

                    // Fond gris uniforme par défaut
                    byte b = 128, g = 128, r = 128, a = 255;

                    // Vérification si le pixel appartient au corps
                    bool isValidBodyPixel = depthValue > 0 &&
                                          depthValue >= minDepth &&
                                          depthValue <= maxDepth;

                    if (isValidBodyPixel)
                    {
                        int colorX = (int)Math.Floor(colorPoint.X + 0.5);
                        int colorY = (int)Math.Floor(colorPoint.Y + 0.5);

                        // Vérification des limites de l'image couleur
                        if (colorX >= 0 && colorX < colorWidth &&
                            colorY >= 0 && colorY < colorHeight)
                        {
                            int colorIndex = (colorY * colorWidth + colorX) * 4;
                            b = colorData[colorIndex];
                            g = colorData[colorIndex + 1];
                            r = colorData[colorIndex + 2];
                            a = 255;
                        }
                    }

                    alignedPixels[outputIndex] = b;
                    alignedPixels[outputIndex + 1] = g;
                    alignedPixels[outputIndex + 2] = r;
                    alignedPixels[outputIndex + 3] = a;
                }
            });

            Marshal.Copy(alignedPixels, 0, bmpData.Scan0, alignedPixels.Length);
            alignedBitmap.UnlockBits(bmpData);

            return alignedBitmap;
        }



        private void BtnExportData_Click(object sender, EventArgs e)
        {
            if (lastSmoothedSpinePoints == null || lastSmoothedSpinePoints.Count == 0)
            {
                MessageBox.Show("Aucune donnée de courbe disponible pour l'export.", "Information",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON Files|*.json|CSV Files|*.csv";
                sfd.Title = "Exporter les données de courbe";
                sfd.FileName = $"SpineCurveData_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (sfd.FilterIndex == 1) // JSON
                        {
                            ExportCurveDataAsJson(sfd.FileName);
                        }
                        else // CSV
                        {
                            ExportCurveDataAsCsv(sfd.FileName);
                        }

                        MessageBox.Show($"Données exportées avec succès: {sfd.FileName}",
                                      "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'export: {ex.Message}",
                                      "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportCurveDataAsJson(string filePath)
        {
            // Create data object
            SpineCurveData curveData = new SpineCurveData
            {
                CaptureTime = DateTime.Now,
                Points = lastSmoothedSpinePoints.Select(p => PointFData.FromPointF(p)).ToList(),
                MaxZIndex = maxZIndex,
                ManualZRef = manualZRef,
                FixedDeepestXPixel = fixedDeepestXPixel,
                SpineAngle = spineAngle,
                PatientIdentifier = "Unknown",
                // ADD THE ORIGINAL SCALING FACTORS
                OriginalOffsetX = 50f,    // Same as your main form
                OriginalScaleX = 0.1f     // Same as your main form
            };

            // Serialize to JSON
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(curveData,
                Newtonsoft.Json.Formatting.Indented);

            File.WriteAllText(filePath, json);
        }

        private void ExportCurveDataAsCsv(string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // Write header
                writer.WriteLine("Index,X (Z-depth mm),Y (position),IsMaxPoint");

                // Write data points
                for (int i = 0; i < lastSmoothedSpinePoints.Count; i++)
                {
                    var point = lastSmoothedSpinePoints[i];
                    string isMaxPoint = (i == maxZIndex) ? "Yes" : "No";
                    writer.WriteLine($"{i},{point.X:F2},{point.Y:F2},{isMaxPoint}");
                }

                // Write metadata
                writer.WriteLine();
                writer.WriteLine($"# Metadata");
                writer.WriteLine($"CaptureTime,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"MaxZIndex,{maxZIndex}");
                writer.WriteLine($"ManualZRef,{manualZRef:F2}");
                writer.WriteLine($"FixedDeepestXPixel,{fixedDeepestXPixel:F2}");
                writer.WriteLine($"SpineAngle,{spineAngle:F2}");
            }

        }



        private void BtnImportData_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files|*.json|CSV Files|*.csv";
                ofd.Title = "Importer les données de courbe";
                ofd.CheckFileExists = true;
                ofd.Multiselect = true; // ← ENABLE MULTI-SELECT

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<SpineCurveData> loadedCurves = new List<SpineCurveData>();

                        foreach (string filePath in ofd.FileNames) // ← PROCESS ALL FILES
                        {
                            SpineCurveData curveData = null;

                            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                curveData = ImportCurveDataFromJson(filePath);
                            }
                            else if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                            {
                                curveData = ImportCurveDataFromCsv(filePath);
                            }

                            if (curveData != null)
                            {
                                curveData.FilePath = filePath; // Store file path for reference
                                loadedCurves.Add(curveData);
                            }
                        }

                        if (loadedCurves.Count > 0)
                        {
                            if (loadedCurves.Count == 1)
                            {
                                // Single file - open in individual viewer
                                OpenCurveDataViewer(loadedCurves[0]);
                            }
                            else
                            {
                                // Multiple files - open in multi-curve viewer
                                OpenMultiCurveViewer(loadedCurves);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Aucune donnée valide trouvée dans les fichiers sélectionnés.",
                                          "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'import: {ex.Message}",
                                      "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }


        private SpineCurveData ImportCurveDataFromJson(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<SpineCurveData>(json);
        }

        private SpineCurveData ImportCurveDataFromCsv(string filePath)
        {
            // Simple CSV import - you might want to enhance this based on your CSV format
            var lines = File.ReadAllLines(filePath);
            var curveData = new SpineCurveData
            {
                CaptureTime = DateTime.Now,
                Points = new List<PointFData>(),
                MaxZIndex = -1
            };

            foreach (var line in lines)
            {
                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length >= 3 && float.TryParse(parts[1], out float x) && float.TryParse(parts[2], out float y))
                {
                    curveData.Points.Add(new PointFData(x, y));

                    // Check if this is the max point
                    if (parts.Length >= 4 && parts[3].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        curveData.MaxZIndex = curveData.Points.Count - 1;
                    }
                }
                // Parse metadata
                else if (parts.Length >= 2)
                {
                    switch (parts[0].ToLower())
                    {
                        case "capturetime":
                            if (DateTime.TryParse(parts[1], out DateTime captureTime))
                                curveData.CaptureTime = captureTime;
                            break;
                        case "maxzindex":
                            if (int.TryParse(parts[1], out int maxIndex))
                                curveData.MaxZIndex = maxIndex;
                            break;
                        case "manualzref":
                            if (float.TryParse(parts[1], out float manualRef))
                                curveData.ManualZRef = manualRef;
                            break;
                        case "fixeddeepestxpixel":
                            if (float.TryParse(parts[1], out float fixedX))
                                curveData.FixedDeepestXPixel = fixedX;
                            break;
                        case "spineangle":
                            if (double.TryParse(parts[1], out double angle))
                                curveData.SpineAngle = angle;
                            break;
                    }
                }
            }

            return curveData.Points.Count > 0 ? curveData : null;
        }

        private void OpenCurveDataViewer(SpineCurveData curveData)
        {
            CurveDataViewer viewer = new CurveDataViewer();
            viewer.LoadCurveData(curveData);

            // Set owner and show
            viewer.Show(this); // This is the key fix

            // Position relative to main form
            viewer.StartPosition = FormStartPosition.Manual;

            // Calculate position to the right of main form
            int newX = this.Right + 10;
            int newY = this.Top;

            // Ensure it doesn't go off-screen
            Screen currentScreen = Screen.FromControl(this);
            if (newX + viewer.Width > currentScreen.WorkingArea.Right)
            {
                newX = currentScreen.WorkingArea.Right - viewer.Width - 10;
            }

            viewer.Location = new Point(newX, newY);

            // Ensure it gets focus
            viewer.Activate();
        }

        private void OpenMultiCurveViewer(List<SpineCurveData> curves)
        {
            MultiCurveViewer multiViewer = new MultiCurveViewer();
            multiViewer.LoadCurves(curves);

            // Position near main form
            multiViewer.StartPosition = FormStartPosition.Manual;
            multiViewer.Location = new Point(this.Right + 10, this.Top);

            // Ensure it fits on screen
            Screen currentScreen = Screen.FromControl(this);
            if (multiViewer.Right > currentScreen.WorkingArea.Right)
            {
                multiViewer.Left = currentScreen.WorkingArea.Right - multiViewer.Width - 10;
            }

            multiViewer.Show(this);
            multiViewer.Activate();
        }



        private void BtnNormalImage_Click(object sender, EventArgs e)
        {
            try
            {
                if (colorBitmap == null)
                {
                    MessageBox.Show("No color image available to capture.", "Info",
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Create a new bitmap from the current color frame
                var multiFrame = multiSourceFrameReader.AcquireLatestFrame();
                {
                    if (multiFrame == null) return;

                    using (var colorFrame = multiFrame.ColorFrameReference.AcquireFrame())
                    {
                        if (colorFrame == null) return;

                        // Copy the color frame data to the bitmap
                        colorFrame.CopyConvertedFrameDataToArray(colorPixels, ColorImageFormat.Bgra);
                        BitmapData bmpData = colorBitmap.LockBits(
                            new Rectangle(0, 0, colorBitmap.Width, colorBitmap.Height),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppArgb);
                        Marshal.Copy(colorPixels, 0, bmpData.Scan0, colorPixels.Length);
                        colorBitmap.UnlockBits(bmpData);
                    }
                }

                // Show preview dialog
                using (var previewForm = new PreviewForm())
                {
                    previewForm.PreviewImage = colorBitmap;
                    if (previewForm.ShowDialog() == DialogResult.OK)
                    {
                        using (SaveFileDialog sfd = new SaveFileDialog())
                        {
                            sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                            sfd.FileName = $"Kinect_Color_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                            sfd.Title = "Save Color Image";

                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                colorBitmap.Save(sfd.FileName);
                                MessageBox.Show($"Image saved successfully: {sfd.FileName}",
                                               "Success",
                                               MessageBoxButtons.OK,
                                               MessageBoxIcon.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving image: {ex.Message}",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

    }

    }




