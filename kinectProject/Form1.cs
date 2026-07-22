using Microsoft.Kinect;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace kinectProject
{
    public partial class Form1 : Form
    {
        #region Services

        private KinectService kinectService;
        private DepthProcessingService depthService;
        private ColorProcessingService colorService;
        private SpineCurveService spineService;
        private PdfReportService pdfReportService;

        #endregion

        #region Frame Rate Control

        private DateTime lastFrameTime = DateTime.MinValue;
        private const int TargetFrameRate = 30;

        #endregion

        #region UI Controls

        private PictureBox depthPictureBox;
        private PictureBox normalPictureBox;
        private PictureBox sideBox;
        private PictureBox infoBox;
        private PictureBox angleSpineBox;
        private PictureBox realAngleCobb;

        private ImageCaptureService imageCaptureService;
        #endregion

        #region State Variables

        private Point clickPoint1 = Point.Empty;
        private Point clickPoint2 = Point.Empty;
        private CameraSpacePoint? selectedPoint1 = null;
        private CameraSpacePoint? selectedPoint2 = null;
        private double spineAngle;
        private bool isDraggingRefLine = false;

        #endregion

        #region Constructor

        public Form1()
        {
            InitializeComponent();
        }

        #endregion

        #region Form Load

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                kinectService = new KinectService();
                kinectService.ConnectionStatusChanged += KinectService_ConnectionStatusChanged;

                if (!kinectService.Initialize())
                {
                    // ✅ Just update status, no popup
                    UpdateStatusBar("Kinect non détecté - Vérifiez la connexion", Color.Red);

                    // ✅ Instead of Application.Exit(), let the user see the status
                    // The watchdog will update if sensor becomes available
                    return;
                }

                depthService = new DepthProcessingService(kinectService.CoordinateMapper);
                colorService = new ColorProcessingService(kinectService.CoordinateMapper);
                spineService = new SpineCurveService(kinectService.CoordinateMapper);
                pdfReportService = new PdfReportService();

                imageCaptureService = new ImageCaptureService(depthService, colorService);

                kinectService.FrameArrived += KinectService_FrameArrived;

                SetupPictureBoxes();
                SetupSidePanel();
                SetupTopPanel();
                SetupStatusStrip();
                SetupContextMenu();

                this.BackColor = Color.FromArgb(45, 45, 60);
                this.Text = "Kinect Body Analysis Pro - Posture Assessment System";
                this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                this.ForeColor = Color.White;
                this.AutoScaleMode = AutoScaleMode.Dpi;
                this.DoubleBuffered = true;
            }
            catch (Exception ex)
            {
                // ✅ Only show popup for unexpected errors
                UpdateStatusBar("Erreur: " + ex.Message, Color.Red);
            }
        }
        private void KinectService_ConnectionStatusChanged(object sender, bool isAvailable)
        {
            this.BeginInvoke((Action)(() =>
            {
                if (isAvailable)
                {
                    UpdateStatusBar("Kinect connecté - En direct", Color.LightGreen);
                }
                else
                {
                    UpdateStatusBar("Kinect déconnecté - Vérifiez la connexion", Color.OrangeRed);
                }
            }));
        }

        private void UpdateStatusBar(string message, Color color)
        {
            foreach (Control control in this.Controls)
            {
                if (control is StatusStrip statusStrip)
                {
                    foreach (ToolStripItem item in statusStrip.Items)
                    {
                        if (item is ToolStripStatusLabel label && label.Name == "kinectStatusLabel")
                        {
                            label.Text = message;
                            label.ForeColor = color;
                            return;
                        }
                    }
                }
            }
        }
        #endregion

        #region UI Setup Methods

        private void SetupPictureBoxes()
        {
            // Main depth view
            depthPictureBox = new PictureBox
            {
                Width = 450,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(25, 25, 40),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Padding = new Padding(5)
            };
            depthPictureBox.MouseClick += DepthPictureBox_MouseClick;
            this.Controls.Add(depthPictureBox);

            // Color view
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
        }

        private void SetupSidePanel()
        {
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BackColor = Color.FromArgb(15, 15, 25)
            };
            this.Controls.Add(rightPanel);

            Panel sideContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 25)
            };
            rightPanel.Controls.Add(sideContainer);

            // Side view (spine curve)
            sideBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            sideBox.MouseDown += SideBox_MouseDown;
            sideBox.MouseUp += SideBox_MouseUp;
            sideBox.MouseMove += SideBox_MouseMove;
            sideContainer.Controls.Add(sideBox);

            // Info panel
            infoBox = new PictureBox
            {
                Height = 120,
                Dock = DockStyle.Bottom,
                BackColor = Color.Transparent,
                Visible = true
            };
            sideContainer.Controls.Add(infoBox);

            // Spine angle display
            angleSpineBox = new PictureBox
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                BackColor = Color.Transparent,
                Visible = true
            };
            infoBox.Controls.Add(angleSpineBox);

            // Cobb angle display
            realAngleCobb = new PictureBox
            {
                Height = 40,
                Dock = DockStyle.Bottom,
                BackColor = Color.Transparent,
                Visible = true
            };
            infoBox.Controls.Add(realAngleCobb);
        }
        private void SetupTopPanel()
        {
            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.FromArgb(32, 32, 42),
                Padding = new Padding(6, 8, 6, 8)
            };
            this.Controls.Add(topPanel);

            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            topPanel.Controls.Add(toolbar);

            // Colors
            Color primaryColor = Color.FromArgb(0, 122, 204);
            Color secondaryColor = Color.FromArgb(40, 167, 69);
            Color accentColor = Color.FromArgb(255, 140, 0);
            Color exportColor = Color.FromArgb(138, 43, 226);
            Color pdfColor = Color.FromArgb(220, 20, 60);
            Color analyzerColor = Color.FromArgb(30, 144, 255);
            Color captureColor = Color.FromArgb(0, 150, 136);

            // Separator
            Label CreateSeparator() => new Label
            {
                Text = "│",
                ForeColor = Color.FromArgb(70, 70, 85),
                AutoSize = true,
                Margin = new Padding(6, 2, 6, 2),
                Font = new Font("Segoe UI", 11, FontStyle.Regular)
            };

            // All buttons in one row - auto-sized
            Button btnOpenBodyAnalyzer = CreateStyledButton("📷 Analyser Image", analyzerColor, BtnOpenBodyAnalyzer_Click);
            Button btnSaveDepthImage = CreateStyledButton("💾 Depth", primaryColor, BtnSaveDepthImage_Click);
            Button btnSaveImage = CreateStyledButton("💾 Color", primaryColor, BtnSaveImage_Click);
            Button btnNormalImage = CreateStyledButton("🖼️ Normal", exportColor, BtnNormalImage_Click);
            Button btnCaptureAll = CreateStyledButton("📸 Capture All", captureColor, BtnCaptureAll_Click);
            Button sagittalBtn = CreateStyledButton("📊 Courbe", secondaryColor, SagittalBtn_Click);
            Button exportBtn = CreateStyledButton("🖼️ Export PNG", accentColor, ExportCurveBtn_Click);
            Button btnExportData = CreateStyledButton("📁 Export", exportColor, BtnExportData_Click);
            Button btnImportData = CreateStyledButton("📂 Import", exportColor, BtnImportData_Click);
            Button generatePdfButton = CreateStyledButton("📄 PDF", pdfColor, GeneratePdfButton_Click);
            Button toggleInfoBtn = CreateStyledButton("Data 👁️", Color.FromArgb(100, 100, 110), (s, args) =>
            {
                infoBox.Visible = !infoBox.Visible;
                infoBox.Parent.PerformLayout();
                sideBox.Refresh();
            });

            toolbar.Controls.Add(btnOpenBodyAnalyzer);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(btnSaveDepthImage);
            toolbar.Controls.Add(btnSaveImage);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(btnNormalImage);
            toolbar.Controls.Add(btnCaptureAll);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(sagittalBtn);
            toolbar.Controls.Add(exportBtn);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(btnExportData);
            toolbar.Controls.Add(btnImportData);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(generatePdfButton);
            toolbar.Controls.Add(CreateSeparator());
            toolbar.Controls.Add(toggleInfoBtn);

            // Tooltips
            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(btnOpenBodyAnalyzer, "Ouvrir l'analyseur d'image corporelle");
            toolTip.SetToolTip(btnSaveDepthImage, "Sauvegarder l'image de profondeur");
            toolTip.SetToolTip(btnSaveImage, "Sauvegarder l'image couleur alignée");
            toolTip.SetToolTip(btnNormalImage, "Sauvegarder l'image couleur normale (1920x1080)");
            toolTip.SetToolTip(btnCaptureAll, "Capturer et sauvegarder toutes les images");
            toolTip.SetToolTip(sagittalBtn, "Capturer la courbe sagittale du dos");
            toolTip.SetToolTip(exportBtn, "Exporter la courbe en image PNG");
            toolTip.SetToolTip(btnExportData, "Exporter données (JSON/CSV)");
            toolTip.SetToolTip(btnImportData, "Importer données sauvegardées");
            toolTip.SetToolTip(generatePdfButton, "Générer un rapport PDF complet");
            toolTip.SetToolTip(toggleInfoBtn, "Afficher/Masquer le panneau info");
        }

        /// <summary>
        /// Creates a modern auto-sized button with rounded corners
        /// </summary>
        private Button CreateStyledButton(string text, Color backColor, EventHandler clickHandler)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(55, 32),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(2),
                Padding = new Padding(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.2f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.15f);

            // Rounded corners
            button.Paint += (s, e) =>
            {
                Rectangle rect = button.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int r = 6;
                    path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                    path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                    path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                    path.CloseAllFigures();
                    button.Region = new Region(path);
                }
            };

            button.Click += clickHandler;
            return button;
        }
        /// <summary>
        /// Creates a grouped panel with a title label
        /// </summary>
        private Panel CreateToolbarGroup(string title, Color backColor)
        {
            Panel group = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = backColor,
                Margin = new Padding(2, 4, 2, 4),
                Padding = new Padding(4, 4, 4, 4),
                MinimumSize = new Size(0, 70)
            };

            // Rounded corners for the group
            group.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, group.ClientRectangle,
                    Color.FromArgb(80, 80, 95), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 80, 95), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 80, 95), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 80, 95), 1, ButtonBorderStyle.Solid);
            };

            // Title label
            Label lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(180, 180, 195),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(6, 1)
            };
            group.Controls.Add(lblTitle);

            // Flow layout for buttons inside the group
            FlowLayoutPanel buttonFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(2, 16),
                BackColor = Color.Transparent
            };
            group.Controls.Add(buttonFlow);

            // Store reference to buttonFlow in Tag for later use
            group.Tag = buttonFlow;

            // Override Controls.Add to redirect buttons to the flow panel
            group.ControlAdded += (s, e) =>
            {
                if (e.Control is Button && e.Control != null && group.Tag is FlowLayoutPanel flow)
                {
                    if (!flow.Controls.Contains(e.Control))
                    {
                        group.Controls.Remove(e.Control);
                        flow.Controls.Add(e.Control);
                    }
                }
            };

            return group;
        }

        /// <summary>
        /// Creates a modern toolbar button with icon and text
        /// </summary>
        private Button CreateToolbarButton(string text, string icon, string tooltip, bool isPrimary = false, bool analyzerColor = false)
        {
            Color btnColor;
            if (analyzerColor)
                btnColor = Color.FromArgb(30, 144, 255);
            else if (isPrimary)
                btnColor = Color.FromArgb(0, 150, 136);
            else
                btnColor = Color.FromArgb(55, 55, 68);

            Button button = new Button
            {
                Text = $" {icon} {text}",
                Size = new Size(90, 42),
                BackColor = btnColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Margin = new Padding(2),
                Padding = new Padding(4, 2, 4, 2),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(btnColor, 0.15f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(btnColor, 0.1f);

            // Rounded corners
            button.Paint += (s, e) =>
            {
                Rectangle rect = button.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int r = 6;
                    path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                    path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                    path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                    path.CloseAllFigures();
                    button.Region = new Region(path);
                }
            };

            // Tooltip
            if (!string.IsNullOrEmpty(tooltip))
            {
                ToolTip tip = new ToolTip();
                tip.SetToolTip(button, tooltip);
            }

            return button;
        }

        #endregion

        private void SetupStatusStrip()
        {
            StatusStrip statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.White,
                RenderMode = ToolStripRenderMode.Professional
            };

            ToolStripStatusLabel statusLabel = new ToolStripStatusLabel
            {
                Text = "Veuillez vous placer à ~2 mètres du capteur pour une détection optimale.",
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9f)
            };

            ToolStripStatusLabel kinectStatus = new ToolStripStatusLabel
            {
                Text = "Kinect: En attente...", // ✅ Initial text
                ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Alignment = ToolStripItemAlignment.Right,
                Name = "kinectStatusLabel"
            };

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(kinectStatus);
            this.Controls.Add(statusStrip);
        }
        private void SetupContextMenu()
        {
            ContextMenuStrip curveMenu = new ContextMenuStrip();
            curveMenu.Items.Add("Ouvrir le visualisateur multi-courbes", null, (s, args) =>
            {
                var points = spineService.LastSmoothedSpinePoints;
                if (points != null && points.Count > 0)
                {
                    var currentCurve = new SpineCurveData
                    {
                        CaptureTime = DateTime.Now,
                        Points = points.Select(p => PointFData.FromPointF(p)).ToList(),
                        MaxZIndex = spineService.MaxZIndex,
                        ManualZRef = spineService.ManualZRef,
                        FixedDeepestXPixel = spineService.FixedDeepestXPixel,
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
        }

   

        #region Kinect Frame Processing

        // In Form1.cs, replace the KinectService_FrameArrived method:

        private void KinectService_FrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if ((DateTime.Now - lastFrameTime).TotalMilliseconds < 1000 / TargetFrameRate)
                return;

            lastFrameTime = DateTime.Now;

            var multiSourceFrame = e.FrameReference.AcquireFrame();
            if (multiSourceFrame == null) return;

            // Process depth + body
            using (var depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            using (var bodyFrame = multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                if (depthFrame != null && bodyFrame != null)
                {
                    Body trackedBody = depthService.GetTrackedBody(bodyFrame);
                    if (trackedBody != null)
                    {
                        depthService.ProcessDepthFrameWithBodyContext(depthFrame, trackedBody);
                        spineService.DrawSpineOnBitmap(trackedBody, depthService.DepthBitmap);
                        spineAngle = spineService.CalculateSpineAngle(trackedBody);

                        this.BeginInvoke((Action)(() =>
                        {
                            DrawSpineAngleInInfoBox(spineAngle);
                        }));
                    }
                }
            }

            // Process color frame
            using (var colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame())
            using (var depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame())
            {
                if (colorFrame != null && depthFrame != null)
                {
                    var aligned = colorService.GenerateAlignedColorImage(depthFrame, colorFrame);
                    if (aligned != null)
                    {
                        this.BeginInvoke((Action)(() =>
                        {
                            var oldImage = normalPictureBox.Image;
                            normalPictureBox.Image = aligned;
                            if (oldImage != null && oldImage != aligned)
                            {
                                oldImage.Dispose();
                            }
                        }));
                    }
                }
            }

            // Update depth picture box
            Bitmap safeDepth = depthService.GetSafeDepthBitmap();

            if (safeDepth != null)
            {
                this.BeginInvoke((Action)(() =>
                {
                    var oldImage = depthPictureBox.Image;
                    depthPictureBox.Image = safeDepth;
                    if (oldImage != null)
                    {
                        oldImage.Dispose();
                    }
                }));
            }

            // ✅ Just set to null - no Dispose needed
            // multiSourceFrame = null;
        }

        #endregion

        #region Button Click Handlers

        private void BtnOpenBodyAnalyzer_Click(object sender, EventArgs e)
        {
            BodyPictureAnalyzer bodyAnalyzerForm = new BodyPictureAnalyzer();
            bodyAnalyzerForm.ShowDialog();
        }

        private void GeneratePdfButton_Click(object sender, EventArgs e)
        {
            using (PdfInputForm inputForm = new PdfInputForm())
            {
                if (inputForm.ShowDialog() == DialogResult.OK)
                {
                    var depthImage = depthPictureBox?.Image;
                    var colorImage = normalPictureBox?.Image;
                    var splineImage = spineService.GenerateSpineCurveImageForPdf(500, 600);

                    pdfReportService.GeneratePatientReport(
                        inputForm, depthImage, colorImage, splineImage,
                        spineService, depthPictureBox);
                }
            }
        }

        private void BtnSaveDepthImage_Click(object sender, EventArgs e)
        {
            SavePictureBoxImage(depthPictureBox, "Depth");
        }

        private void BtnSaveImage_Click(object sender, EventArgs e)
        {
            SavePictureBoxImage(normalPictureBox, "Color");
        }

        private void SagittalBtn_Click(object sender, EventArgs e)
        {
            var multiFrame = kinectService.AcquireLatestFrame();
            if (multiFrame == null) return;

            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            using (var bodyFrame = multiFrame.BodyFrameReference.AcquireFrame())
            {
                if (depthFrame == null || bodyFrame == null) return;

                int width = depthFrame.FrameDescription.Width;
                int height = depthFrame.FrameDescription.Height;

                ushort[] depthData = depthService.GetDepthData(depthFrame);
                ushort[] smooth = depthService.SmoothDepthData(depthData, width, height);

                Body trackedBody = depthService.GetTrackedBody(bodyFrame);
                if (trackedBody == null) return;

                spineService.DrawDepthSpineCurve(smooth, trackedBody, sideBox);
            }
        }

        private void ExportCurveBtn_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image|*.png";
                sfd.Title = "Enregistrer Courbe Sagittale";
                sfd.FileName = $"SpineCurve_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    spineService.ExportSpineCurveHighRes(sfd.FileName, 1920, 1080);
                    MessageBox.Show($"Courbe enregistrée : {sfd.FileName}",
                        "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnExportData_Click(object sender, EventArgs e)
        {
            if (spineService.LastSmoothedSpinePoints == null || spineService.LastSmoothedSpinePoints.Count == 0)
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
                        if (sfd.FilterIndex == 1)
                            ExportCurveDataAsJson(sfd.FileName);
                        else
                            ExportCurveDataAsCsv(sfd.FileName);

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

        private void BtnImportData_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON Files|*.json|CSV Files|*.csv";
                ofd.Title = "Importer les données de courbe";
                ofd.CheckFileExists = true;
                ofd.Multiselect = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<SpineCurveData> loadedCurves = new List<SpineCurveData>();

                        foreach (string filePath in ofd.FileNames)
                        {
                            SpineCurveData curveData = null;

                            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                curveData = ImportCurveDataFromJson(filePath);
                            else if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                curveData = ImportCurveDataFromCsv(filePath);

                            if (curveData != null)
                            {
                                curveData.FilePath = filePath;
                                loadedCurves.Add(curveData);
                            }
                        }

                        if (loadedCurves.Count > 0)
                            OpenMultiCurveViewer(loadedCurves);
                        else
                            MessageBox.Show("Aucune donnée valide trouvée.", "Information",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'import: {ex.Message}",
                            "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnNormalImage_Click(object sender, EventArgs e)
        {
            try
            {
                // Get the full color bitmap (1920x1080)
                var fullColorImage = colorService.FullColorBitmap;

                if (fullColorImage == null)
                {
                    MessageBox.Show("No color image available. Make sure the Kinect is connected.",
                        "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var previewForm = new PreviewForm())
                {
                    previewForm.PreviewImage = fullColorImage;

                    if (previewForm.ShowDialog() == DialogResult.OK)
                    {
                        using (SaveFileDialog sfd = new SaveFileDialog())
                        {
                            sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                            sfd.FileName = $"Kinect_Color_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                            sfd.Title = "Save Color Image";

                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                fullColorImage.Save(sfd.FileName);
                                MessageBox.Show($"Image saved: {sfd.FileName}", "Success",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }

                fullColorImage?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        private void BtnCaptureAll_Click(object sender, EventArgs e)
        {
            try
            {
                // Get current images from services
                var (depthImage, colorAligned, normalImage) = imageCaptureService.CaptureAllImages();

                if (depthImage == null && normalImage == null)
                {
                    MessageBox.Show("No images available. Make sure the Kinect is connected.",
                        "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Get the aligned color image from the PictureBox
                Image alignedImage = null;
                if (normalPictureBox.Image != null)
                {
                    alignedImage = new Bitmap(normalPictureBox.Image);
                }

                // Show preview and save
                imageCaptureService.ShowPreviewAndSave(depthImage, alignedImage, normalImage);

                // Cleanup
                depthImage?.Dispose();
                alignedImage?.Dispose();
                normalImage?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error capturing images: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region SideBox Mouse Events

        private void SideBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (spineService.FixedDeepestXPixel > 0 &&
                Math.Abs(e.X - spineService.FixedDeepestXPixel) < 10)
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

        private void SideBox_MouseMove(object sender, MouseEventArgs e)
        {
            var points = spineService.LastSmoothedSpinePoints;
            if (points == null || points.Count == 0) return;

            if (isDraggingRefLine)
            {
                spineService.FixedDeepestXPixel = e.X;
                spineService.ManualZRef = (spineService.FixedDeepestXPixel - 50) / 0.1f;
                sideBox.Invalidate();
            }

            Bitmap sideView = new Bitmap(sideBox.Width, sideBox.Height);
            using (Graphics g = Graphics.FromImage(sideView))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Draw spine curve
                using (Pen pen = new Pen(Color.Cyan, 3))
                {
                    for (int i = 1; i < points.Count; i++)
                    {
                        float x1 = 50 + points[i - 1].X * 0.1f;
                        float y1 = points[i - 1].Y;
                        float x2 = 50 + points[i].X * 0.1f;
                        float y2 = points[i].Y;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }

                // Draw reference line
                float zRef = (spineService.ManualZRef > 0) ? spineService.ManualZRef :
                    (spineService.MaxZIndex >= 0 && spineService.MaxZIndex < points.Count ?
                    points[spineService.MaxZIndex].X : 0);

                float fixedX = (spineService.ManualZRef > 0) ?
                    spineService.FixedDeepestXPixel : 50 + zRef * 0.1f;
                spineService.FixedDeepestXPixel = fixedX;

                using (Pen redPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(redPen, fixedX, 0, fixedX, sideView.Height);
                }
                g.DrawString($"Ref Z: {zRef:F0} mm", new Font("Arial", 9), Brushes.White, fixedX + 5, 10);

                // Find nearest point
                float minDistance = 10f;
                System.Drawing.PointF? closestPoint = null;

                foreach (var pt in points)
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

                if (closestPoint.HasValue)
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

        #endregion

        #region Depth PictureBox Click

        private void DepthPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (depthService.DepthBitmap == null || kinectService.CoordinateMapper == null) return;

            int x = e.X * 512 / depthPictureBox.Width;
            int y = e.Y * 424 / depthPictureBox.Height;

            var multiFrame = kinectService.AcquireLatestFrame();
            if (multiFrame == null) return;

            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            {
                if (depthFrame == null) return;

                ushort[] depthData = depthService.GetDepthData(depthFrame);
                int index = y * 512 + x;
                ushort depth = depthData[index];

                if (depth == 0) return;

                CameraSpacePoint cameraPoint = kinectService.MapDepthToCameraSpace(x, y, depth);

                if (selectedPoint1 == null)
                {
                    selectedPoint1 = cameraPoint;
                    MessageBox.Show("First point selected.");
                }
                else if (selectedPoint2 == null)
                {
                    selectedPoint2 = cameraPoint;
                    float depthDifference = Math.Abs(selectedPoint1.Value.Z - selectedPoint2.Value.Z) * 1000;
                    MessageBox.Show($"Depth Difference: {depthDifference:F2} mm");
                    selectedPoint1 = null;
                    selectedPoint2 = null;
                }
            }
        }

        #endregion

        #region Spine Angle Display

        private void DrawSpineAngleInInfoBox(double angle)
        {
            if (angleSpineBox == null) return;

            Bitmap infoBitmap = new Bitmap(angleSpineBox.Width, angleSpineBox.Height);
            using (Graphics g = Graphics.FromImage(infoBitmap))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                string angleText = $"Angle sagittal du tronc: {angle:F2}°";
                using (Font font = new Font("Arial", 8, FontStyle.Regular))
                {
                    g.DrawString(angleText, font, Brushes.LightGreen, new System.Drawing.PointF(10, 10));
                }
            }
            angleSpineBox.Image?.Dispose();
            angleSpineBox.Image = infoBitmap;
            angleSpineBox.Invalidate();
        }

        #endregion

        #region Data Export/Import

        private void ExportCurveDataAsJson(string filePath)
        {
            var curveData = new SpineCurveData
            {
                CaptureTime = DateTime.Now,
                Points = spineService.LastSmoothedSpinePoints.Select(p => PointFData.FromPointF(p)).ToList(),
                MaxZIndex = spineService.MaxZIndex,
                ManualZRef = spineService.ManualZRef,
                FixedDeepestXPixel = spineService.FixedDeepestXPixel,
                SpineAngle = spineAngle,
                PatientIdentifier = "Unknown",
                OriginalOffsetX = 50f,
                OriginalScaleX = 0.1f
            };

            string json = JsonConvert.SerializeObject(curveData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private void ExportCurveDataAsCsv(string filePath)
        {
            var points = spineService.LastSmoothedSpinePoints;
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Index,X (Z-depth mm),Y (position),IsMaxPoint");

                for (int i = 0; i < points.Count; i++)
                {
                    string isMaxPoint = (i == spineService.MaxZIndex) ? "Yes" : "No";
                    writer.WriteLine($"{i},{points[i].X:F2},{points[i].Y:F2},{isMaxPoint}");
                }

                writer.WriteLine();
                writer.WriteLine($"# Metadata");
                writer.WriteLine($"CaptureTime,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"MaxZIndex,{spineService.MaxZIndex}");
                writer.WriteLine($"ManualZRef,{spineService.ManualZRef:F2}");
                writer.WriteLine($"FixedDeepestXPixel,{spineService.FixedDeepestXPixel:F2}");
                writer.WriteLine($"SpineAngle,{spineAngle:F2}");
            }
        }

        private SpineCurveData ImportCurveDataFromJson(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<SpineCurveData>(json);
        }

        private SpineCurveData ImportCurveDataFromCsv(string filePath)
        {
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
                    if (parts.Length >= 4 && parts[3].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase))
                        curveData.MaxZIndex = curveData.Points.Count - 1;
                }
                else if (parts.Length >= 2)
                {
                    switch (parts[0].ToLower())
                    {
                        case "capturetime":
                            if (DateTime.TryParse(parts[1], out DateTime dt)) curveData.CaptureTime = dt;
                            break;
                        case "maxzindex":
                            if (int.TryParse(parts[1], out int maxIdx)) curveData.MaxZIndex = maxIdx;
                            break;
                        case "manualzref":
                            if (float.TryParse(parts[1], out float mRef)) curveData.ManualZRef = mRef;
                            break;
                        case "fixeddeepestxpixel":
                            if (float.TryParse(parts[1], out float fx)) curveData.FixedDeepestXPixel = fx;
                            break;
                        case "spineangle":
                            if (double.TryParse(parts[1], out double ang)) curveData.SpineAngle = ang;
                            break;
                    }
                }
            }

            return curveData.Points.Count > 0 ? curveData : null;
        }

        #endregion

        #region Multi-Curve Viewer

        private void OpenMultiCurveViewer(List<SpineCurveData> curves)
        {
            MultiCurveViewer multiViewer = new MultiCurveViewer();
            multiViewer.LoadCurves(curves);
            multiViewer.StartPosition = FormStartPosition.Manual;
            multiViewer.Location = new Point(this.Right + 10, this.Top);

            Screen currentScreen = Screen.FromControl(this);
            if (multiViewer.Right > currentScreen.WorkingArea.Right)
                multiViewer.Left = currentScreen.WorkingArea.Right - multiViewer.Width - 10;

            multiViewer.Show(this);
            multiViewer.Activate();
        }

        #endregion

        #region Helpers

        private void SavePictureBoxImage(PictureBox pictureBox, string imageType)
        {
            try
            {
                if (pictureBox.Image == null)
                {
                    MessageBox.Show($"No {imageType} image available to save.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg";
                    sfd.FileName = $"Kinect_{imageType}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        pictureBox.Image.Save(sfd.FileName);
                        MessageBox.Show($"{imageType} image saved successfully!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving {imageType} image: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Form Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            kinectService?.Shutdown();
            base.OnFormClosing(e);
        }

        #endregion
    }
}