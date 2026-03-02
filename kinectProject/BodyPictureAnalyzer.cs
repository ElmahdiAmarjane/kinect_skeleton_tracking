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

namespace kinectProject
{
    public partial class BodyPictureAnalyzer : Form
    {
        // Enums

        ////////////////////////////////

        // Add to enums section
        public enum DetectionMode { None, SinglePoint, MultiplePoints, BodyContour, ManualPick, Automatic }
        public enum PointColor { Red, Green, Blue, Yellow, White, Custom }

        // Add after other structures
        private struct DetectedPoint
        {
            public Point Location;
            public PointColor Color;
            public double Confidence;
            public int Radius;
            public int ID;

            public DetectedPoint(Point location, PointColor color, double confidence, int radius, int id)
            {
                Location = location;
                Color = color;
                Confidence = confidence;
                Radius = radius;
                ID = id;
            }
        }

        private struct BodyLandmark
        {
            public string Name;
            public Point Location;
            public List<string> ConnectedTo;

            public BodyLandmark(string name, Point location)
            {
                Name = name;
                Location = location;
                ConnectedTo = new List<string>();
            }
        }



        ////////////////////////////////
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

            public double? AngleValue; // For storing the angle in degrees
            public List<int> RelatedLineIDs; // IDs of lines that form this angle

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
                AngleValue = null;
                RelatedLineIDs = new List<int>();
            }
            public static Measurement CreateIntersectionAngle(string name, int id, Point vertex,
                                                 double angleValue, int line1Id, int line2Id)
            {
                var measurement = new Measurement(vertex, vertex, name, MeasurementType.Angle, id);
                measurement.Vertex = vertex;
                measurement.AngleValue = angleValue;
                measurement.RelatedLineIDs.Add(line1Id);
                measurement.RelatedLineIDs.Add(line2Id);
                return measurement;
            }


        }

        private enum MeasurementType { Line, Point, Angle, AngleWithAxis, Distance, ReferenceLine, PerpendicularLine, None }

        // Ajoutez ces énumérations dans la section des enums
        private enum IntersectionType { Exact, Proximity, Terminal, None }

        // Ajoutez cette structure après la struct Measurement
        private struct IntersectionPoint
        {
            public Point Location;
            public List<int> LineIDs; // IDs des lignes qui se croisent
            public IntersectionType Type;
            public List<Tuple<int, int, double>> Angles; // (ID1, ID2, Angle)
            public int ID;

            public IntersectionPoint(Point location, int id)
            {
                Location = location;
                LineIDs = new List<int>();
                Type = IntersectionType.None;
                Angles = new List<Tuple<int, int, double>>();
                ID = id;
            }
        }

        // Ajoutez ces champs dans la section "Application state"
        private List<IntersectionPoint> intersectionPoints = new List<IntersectionPoint>();
        private int intersectionCounter = 1;
        private IntersectionPoint? hoveredIntersection = null;
        private IntersectionPoint? selectedIntersection = null;
        private const int intersectionTolerance = 10; // pixels

        // Application state


        /// /////////////////////////////////////////////

        // Add to Application state section
        private DetectionMode currentDetectionMode = DetectionMode.None;
        private List<DetectedPoint> detectedPoints = new List<DetectedPoint>();
        private List<BodyLandmark> bodyLandmarks = new List<BodyLandmark>();
        private int detectionTolerance = 30; // Color tolerance
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
        ////////////////////////////////
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
        //
        // Dans la section des champs privés
        private bool autoRenameEnabled = true; // Par défaut activé

        // AJOUTER ces variables pour gérer la création de lignes entre points :
        private Point? selectedPointForLine = null;
        private bool isCreatingLineBetweenPoints = false;

        // AJOUTER cette variable pour la surbrillance :
        private Point? highlightedPoint = null;

        /////

        // Add these with the other fields
        private bool isPickingReferenceColor = false;
        private Color? referenceColor = null;
        private Point? pickedPointLocation = null; // For visual feedback



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

            // Toolstrip setup - CRÉER LE TOOLSTRIP D'ABORD
            toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;
            toolStrip.BackColor = Color.FromArgb(62, 62, 64);
            toolStrip.ForeColor = Color.White;
            toolStrip.RenderMode = ToolStripRenderMode.Professional;
            toolStrip.Renderer = new CustomToolStripRenderer();

            // Toolstrip buttons - AJOUTER LES BOUTONS APRÈS AVOIR CRÉÉ LE TOOLSTRIP
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

            AddToolButton("🔴 Simple Test", (s, e) => SimpleDetectionTest());

            // Zoom controls
            AddToolSeparator();
            AddToolButton("🔍 Zoom In", BtnZoomIn_Click);
            AddToolButton("🔍 Zoom Out", BtnZoomOut_Click);
            AddToolButton("🔍 Zoom Fit", BtnZoomFit_Click);
            AddToolButton("🔍 Zoom 100%", BtnZoomReset_Click);
            AddToolButton("✋ Pan", BtnPan_Click);

            // Auto-rename button - AJOUTEZ-LE ICI, APRÈS LA CRÉATION DU TOOLSTRIP
            AddToolButton("🏷️ Auto-Rename", BtnToggleAutoRename_Click);

            // In SetupUI method, add these buttons after existing ones
            AddToolSeparator();
            AddToolButton("🎯 Detect Points", BtnDetectPoints_Click);

            AddToolButton("📏 Connect Points", BtnConnectPoints_Click);


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

            // Add controls to form - AJOUTEZ LES CONTRÔLES DANS LE BON ORDRE
            this.Controls.Add(drawingPanel);
            this.Controls.Add(measurementsList);
            this.Controls.Add(toolStrip); // toolStrip doit être ajouté avant statusStrip
            this.Controls.Add(statusStrip);

            // Initialize grid origin
            gridOrigin = new Point(drawingPanel.Width / 2, drawingPanel.Height / 2);
            UpdateTransformationMatrices();
        }
        private void BtnToggleAutoRename_Click(object sender, EventArgs e)
        {
            // Basculer l'état
            autoRenameEnabled =
                !autoRenameEnabled;


            // Mettre à jour le texte du bouton
            var button = sender as ToolStripButton;
            if (button != null)
            {
                button.Text = autoRenameEnabled ?
                             "🏷️ Auto-Rename: ON" : "🏷️ Auto-Rename: OFF";
            }

            UpdateStatus($"Auto-rename: {(autoRenameEnabled ? "Enabled" : "Disabled")}");
        }


        ////

        //private void SimpleDetectionTest()
        //{
        //    if (originalImage == null) return;

        //    detectedPoints.Clear();

        //    // Just detect bright red pixels
        //    Bitmap bmp = new Bitmap(originalImage);

        //    for (int x = 0; x < bmp.Width; x += 5) // Sample every 5 pixels
        //    {
        //        for (int y = 0; y < bmp.Height; y += 5)
        //        {
        //            Color pixel = bmp.GetPixel(x, y);

        //            // Simple red detection: R > 200, G < 100, B < 100
        //            if (pixel.R > 200 && pixel.G < 100 && pixel.B < 100)
        //            {
        //                detectedPoints.Add(new DetectedPoint(
        //                    new Point(x, y),
        //                    PointColor.Red,
        //                    1.0,
        //                    10,
        //                    detectedPoints.Count + 1));
        //            }
        //        }
        //    }

        //    bmp.Dispose();

        //    MessageBox.Show($"Simple detection found {detectedPoints.Count} red pixels");
        //    CreateMeasurementsFromDetectedPoints();
        //    drawingPanel.Invalidate();
        //}


        // // // // // // // // //


        private void DetectColoredPoints()
        {
            if (originalImage == null) return;

            detectedPoints.Clear();

            // REINITIALISER les paramètres pour les autocollants
            int toleranceToUse = 15; // Très strict : 15%
            int minSizeToUse = 50;   // Les autocollants sont plus grands
            int maxSizeToUse = 500;  // Maximum raisonnable

            using (Bitmap bmp = new Bitmap(originalImage))
            {
                int width = bmp.Width;
                int height = bmp.Height;

                // Créer une image de débogage
                Bitmap debug = new Bitmap(width, height);

                // ---- ÉTAPE 1: Détection STRICTE du rouge vif #e32e2c ----
                bool[,] strictMask = new bool[height, width];

                // Valeurs exactes du rouge autocollant
                int stickerR = 227;  // Rouge vif
                int stickerG = 46;   // Vert très faible
                int stickerB = 44;   // Bleu très faible

                // Tolérance TRÈS serrée
                int tolerance = (int)(toleranceToUse * 2.55); // 15% -> ~38 unités

                int redPoints = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = bmp.GetPixel(x, y);

                        // CRITÈRES STRICTS pour les autocollants :
                        // 1. Rouge DOMINANT (R > G + 100 ET R > B + 100)
                        bool redDominant = (pixel.R > pixel.G + 100) && (pixel.R > pixel.B + 100);

                        // 2. Vert et Bleu TRÈS FAIBLES (pour éviter la peau)
                        bool lowGreenBlue = (pixel.G < 80) && (pixel.B < 80);

                        // 3. Rouge SATURÉ (pas de gris/rose)
                        bool saturated = (pixel.R - Math.Min(pixel.G, pixel.B)) > 150;

                        // 4. Proche de #e32e2c spécifiquement
                        double distanceToSticker = Math.Sqrt(
                            Math.Pow(pixel.R - stickerR, 2) +
                            Math.Pow(pixel.G - stickerG, 2) +
                            Math.Pow(pixel.B - stickerB, 2));

                        bool closeToStickerColor = distanceToSticker < tolerance;

                        // TOUS les critères doivent être vrais
                        strictMask[y, x] = redDominant && lowGreenBlue && saturated && closeToStickerColor;

                        // Pour débogage : colorier les pixels détectés
                        if (strictMask[y, x])
                        {
                            debug.SetPixel(x, y, Color.Red);
                            redPoints++;
                        }
                        else
                        {
                            debug.SetPixel(x, y, Color.FromArgb(
                                pixel.R / 4,  // Assombrir pour mieux voir
                                pixel.G / 4,
                                pixel.B / 4));
                        }
                    }
                }

                // ---- ÉTAPE 2: Regroupement en blobs ----
                List<ConnectedComponent> stickers = FindStickers(strictMask, width, height);

                MessageBox.Show($"Pixels rouges stricts: {redPoints}\nBlobs potentiels: {stickers.Count}",
                               "Debug - Étape 1");

                // ---- ÉTAPE 3: Filtrer les vrais autocollants ----
                int id = 1;
                foreach (var sticker in stickers)
                {
                    // Critères pour un autocollant :
                    // 1. Taille appropriée
                    if (sticker.PixelCount < minSizeToUse || sticker.PixelCount > maxSizeToUse)
                        continue;

                    // 2. Forme relativement circulaire
                    double circularity = CalculateCircularity(sticker);
                    if (circularity < 0.5) // Doit être assez rond
                        continue;

                    // 3. Couleur moyenne très rouge
                    Color avgColor = CalculateAverageColor(sticker, bmp);
                    if (!IsStickerRed(avgColor))
                        continue;

                    // 4. Pas de "trous" (autocollant solide)
                    if (HasHoles(sticker, strictMask))
                        continue;

                    // C'EST UN VRAI AUTOCOLLANT !
                    Point center = new Point(
                        (sticker.MinX + sticker.MaxX) / 2,
                        (sticker.MinY + sticker.MaxY) / 2
                    );

                    detectedPoints.Add(new DetectedPoint(
                        center,
                        selectedColor,
                        1.0, // Haute confiance
                        (int)Math.Sqrt(sticker.PixelCount / Math.PI),
                        id++
                    ));

                    // Dessiner sur l'image debug
                    DrawStickerMarker(debug, sticker, id);
                }

                CreateMeasurementsFromDetectedPoints();

                // Sauvegarder et montrer
                //  debug.Save("strict_sticker_detection.png");

                MessageBox.Show($"Autocollants détectés: {detectedPoints.Count}\n" +
                               $"Image sauvegardée: strict_sticker_detection.png",
                               "Résultat final");

                debug.Dispose();
            }

            drawingPanel.Invalidate();
        }

        // ---- FONCTIONS AUXILIAIRES ----

        private List<ConnectedComponent> FindStickers(bool[,] mask, int width, int height)
        {
            List<ConnectedComponent> components = new List<ConnectedComponent>();
            bool[,] visited = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x] && !visited[y, x])
                    {
                        ConnectedComponent comp = new ConnectedComponent();
                        Stack<Point> stack = new Stack<Point>();
                        stack.Push(new Point(x, y));

                        while (stack.Count > 0)
                        {
                            Point p = stack.Pop();

                            if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height ||
                                visited[p.Y, p.X] || !mask[p.Y, p.X])
                                continue;

                            visited[p.Y, p.X] = true;
                            comp.Add(p.X, p.Y);

                            // 8-connexité pour mieux regrouper
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    stack.Push(new Point(p.X + dx, p.Y + dy));
                                }
                            }
                        }

                        if (comp.PixelCount >= 10) // Ignorer les très petits groupes
                        {
                            components.Add(comp);
                        }
                    }
                }
            }

            return components;
        }

        private double CalculateCircularity(ConnectedComponent comp)
        {
            double area = comp.PixelCount;
            double perimeter = 2 * (comp.Width + comp.Height); // Estimation simple

            if (perimeter == 0) return 0;

            return (4 * Math.PI * area) / (perimeter * perimeter);
        }

        private Color CalculateAverageColor(ConnectedComponent comp, Bitmap image)
        {
            long totalR = 0, totalG = 0, totalB = 0;

            foreach (var p in comp.Pixels)
            {
                Color pixel = image.GetPixel(p.X, p.Y);
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
            }

            int count = comp.PixelCount;
            return Color.FromArgb(
                (int)(totalR / count),
                (int)(totalG / count),
                (int)(totalB / count));
        }

        private bool IsStickerRed(Color color)
        {
            // Un autocollant doit avoir :
            // R > 180 (très rouge)
            // G < 100 (peu de vert)
            // B < 100 (peu de bleu)
            // R > G + 80 (rouge dominant)
            // R > B + 80 (rouge dominant)

            return color.R > 180 &&
                   color.G < 100 &&
                   color.B < 100 &&
                   color.R > color.G + 80 &&
                   color.R > color.B + 80;
        }

        private bool HasHoles(ConnectedComponent comp, bool[,] mask)
        {
            // Vérifie si le composant a des "trous" (pixels non-rouges à l'intérieur)
            // Les autocollants sont généralement solides

            int holePixels = 0;
            int totalPixelsInBbox = 0;

            for (int y = comp.MinY + 1; y < comp.MaxY; y++)
            {
                for (int x = comp.MinX + 1; x < comp.MaxX; x++)
                {
                    totalPixelsInBbox++;

                    // Si c'est dans le rectangle mais pas dans le masque, c'est un trou
                    if (!mask[y, x])
                    {
                        holePixels++;
                    }
                }
            }

            // Si plus de 20% de trous, c'est probablement pas un autocollant
            return totalPixelsInBbox > 0 && ((double)holePixels / totalPixelsInBbox) > 0.2;
        }

        private void DrawStickerMarker(Bitmap debug, ConnectedComponent sticker, int id)
        {
            using (Graphics g = Graphics.FromImage(debug))
            {
                // Dessiner un cercle vert autour de l'autocollant
                g.DrawEllipse(Pens.Lime,
                    sticker.MinX, sticker.MinY,
                    sticker.Width, sticker.Height);

                // Dessiner le centre
                Point center = new Point(
                    (sticker.MinX + sticker.MaxX) / 2,
                    (sticker.MinY + sticker.MaxY) / 2);

                g.FillEllipse(Brushes.Cyan, center.X - 3, center.Y - 3, 6, 6);

                // Ajouter un numéro
                g.DrawString(id.ToString(),
                    new System.Drawing.Font("Arial", 10, FontStyle.Bold),
                    Brushes.Yellow,
                    center.X + 5, center.Y - 10);
            }
        }



        public class AutoRenameDialog : Form
        {
            private TextBox textBox;
            private CheckBox dontAskCheckBox;

            public string NewName { get; private set; }
            public bool DontAskAgain { get; private set; }

            public AutoRenameDialog(string defaultName)
            {
                InitializeComponent(defaultName);
            }

            private void InitializeComponent(string defaultName)
            {
                this.Text = "Rename Measurement";
                this.Size = new Size(350, 180);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;

                Label label = new Label
                {
                    Text = "Enter name for measurement:",
                    Location = new Point(20, 20),
                    Size = new Size(300, 20),
                    ForeColor = Color.White
                };

                textBox = new TextBox
                {
                    Text = defaultName,
                    Location = new Point(20, 50),
                    Size = new Size(300, 20),
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                dontAskCheckBox = new CheckBox
                {
                    Text = "Don't ask again (use auto-rename)",
                    Location = new Point(20, 80),
                    Size = new Size(200, 20),
                    ForeColor = Color.LightGray,
                    BackColor = Color.Transparent
                };

                Button okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(80, 110),
                    Size = new Size(80, 25),
                    BackColor = Color.FromArgb(0, 122, 204),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                okButton.Click += OkButton_Click;

                Button cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(180, 110),
                    Size = new Size(80, 25),
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                this.Controls.AddRange(new Control[] { label, textBox, dontAskCheckBox, okButton, cancelButton });
                this.AcceptButton = okButton;
                this.CancelButton = cancelButton;
            }

            private void OkButton_Click(object sender, EventArgs e)
            {
                NewName = textBox.Text.Trim();
                DontAskAgain = dontAskCheckBox.Checked;

                if (string.IsNullOrWhiteSpace(NewName))
                {
                    MessageBox.Show("Please enter a valid name.");
                    this.DialogResult = DialogResult.None;
                }
            }
        }




        public class ImprovedDetectionDialog : DetectionSettingsDialog
        {
            public DetectionMode DetectionMode { get; set; }
            private RadioButton autoRadio;
            private RadioButton manualRadio;

            public ImprovedDetectionDialog(PointColor defaultColor, Color customColor,
                                          int defaultTolerance, int defaultMinSize, int defaultMaxSize)
                : base(defaultColor, customColor, defaultTolerance, defaultMinSize, defaultMaxSize)
            {
                // Add detection mode selection to the dialog
                this.Height += 80;

                GroupBox modeGroup = new GroupBox
                {
                    Text = "Detection Mode",
                    Location = new Point(20, 190),
                    Size = new Size(340, 70),
                    ForeColor = Color.White
                };

                autoRadio = new RadioButton
                {
                    Text = "Automatic (use preset color)",
                    Location = new Point(20, 20),
                    Size = new Size(200, 20),
                    ForeColor = Color.White,
                    Checked = true
                };

                manualRadio = new RadioButton
                {
                    Text = "Manual (pick a reference color)",
                    Location = new Point(20, 45),
                    Size = new Size(200, 20),
                    ForeColor = Color.White
                };

                modeGroup.Controls.Add(autoRadio);
                modeGroup.Controls.Add(manualRadio);

                this.Controls.Add(modeGroup);

                // Move buttons down
                foreach (Control ctrl in this.Controls)
                {
                    if (ctrl is Button && (ctrl.Text == "Detect Points" || ctrl.Text == "Cancel"))
                    {
                        ctrl.Location = new Point(ctrl.Location.X, ctrl.Location.Y + 60);
                    }
                    if (ctrl is Label && ctrl.Text.StartsWith("Tips"))
                    {
                        ctrl.Location = new Point(ctrl.Location.X, ctrl.Location.Y + 60);
                    }
                }
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    DetectionMode = autoRadio.Checked ?
                        DetectionMode.Automatic : DetectionMode.ManualPick;
                }
                base.OnFormClosing(e);
            }
        }



        // //  // // // // //
        private void BtnDetectPoints_Click(object sender, EventArgs e)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Please load an image first.", "No Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var detectionDialog = new ImprovedDetectionDialog(
                selectedColor, customColor, detectionTolerance, minPointSize, maxPointSize))
            {
                if (detectionDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = detectionDialog.SelectedColor;
                    customColor = detectionDialog.CustomColor;
                    detectionTolerance = detectionDialog.Tolerance;
                    minPointSize = detectionDialog.MinSize;
                    maxPointSize = detectionDialog.MaxSize;

                    if (detectionDialog.DetectionMode == DetectionMode.ManualPick)
                    {
                        // Enter color picking mode
                        isPickingReferenceColor = true;
                        referenceColor = null;
                        pickedPointLocation = null;
                        UpdateStatus("Click on a sticker to sample its color");
                        drawingPanel.Cursor = Cursors.Cross;
                    }
                    else
                    {
                        // Use automatic detection with selected color
                        DetectColoredPointsFlexible(null);
                    }
                }
            }
        }

        private void BtnConnectPoints_Click(object sender, EventArgs e)
        {
            if (detectedPoints.Count == 0)
            {
                MessageBox.Show("Aucun point détecté. Utilisez d'abord la détection de points.",
                               "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Activer le mode de connexion
            isCreatingLineBetweenPoints = true;
            selectedPointForLine = null;

            UpdateStatus("Mode Connexion: Cliquez sur le premier point, puis sur le second");
            drawingPanel.Cursor = Cursors.Hand;
            drawingPanel.Invalidate();
        }

        ///////////








        private class ConnectedComponent
        {
            public List<Point> Pixels = new List<Point>();
            public int MinX = int.MaxValue, MinY = int.MaxValue;
            public int MaxX = int.MinValue, MaxY = int.MinValue;

            public int PixelCount => Pixels.Count;
            public int Width => MaxX - MinX + 1;
            public int Height => MaxY - MinY + 1;

            public PointF GeometricCenter =>
                new PointF((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);

            public void Add(int x, int y)
            {
                Pixels.Add(new Point(x, y));
                MinX = Math.Min(MinX, x);
                MinY = Math.Min(MinY, y);
                MaxX = Math.Max(MaxX, x);
                MaxY = Math.Max(MaxY, y);
            }
        }












        ///////////











        private void CreateMeasurementsFromDetectedPoints()
        {
            int startId = idCounter;

            foreach (var detectedPoint in detectedPoints)
            {
                string pointName = $"DP{detectedPoint.ID}";

                Measurement measurement = new Measurement(
                    detectedPoint.Location,
                    detectedPoint.Location,
                    pointName,
                    MeasurementType.Point,
                    idCounter++);

                measurements.Add(measurement);
            }

            UpdateMeasurementsList();
            UpdateStatus($"Créé {detectedPoints.Count} points de mesure.");
        }


        private int Find(int[] parent, int x)
        {
            if (parent[x] != x)
                parent[x] = Find(parent, parent[x]);
            return parent[x];
        }






        // Add these form classes at the end of your file

        public class DetectionSettingsDialog : Form
        {
            private ComboBox colorComboBox;
            private Button colorPickerButton;
            private TrackBar toleranceTrackBar;
            private NumericUpDown minSizeNumeric;
            private NumericUpDown maxSizeNumeric;
            private ColorDialog colorDialog;

            public PointColor SelectedColor { get; private set; }
            public Color CustomColor { get; private set; }
            public int Tolerance { get; private set; }
            public int MinSize { get; private set; }
            public int MaxSize { get; private set; }

            public DetectionSettingsDialog(PointColor defaultColor, Color customColor,
                                          int defaultTolerance, int defaultMinSize, int defaultMaxSize)
            {
                InitializeComponent();

                SelectedColor = defaultColor;
                CustomColor = customColor;
                Tolerance = defaultTolerance;
                MinSize = defaultMinSize;
                MaxSize = defaultMaxSize;

                LoadSettings();
            }

            private void InitializeComponent()
            {
                this.Text = "Point Detection Settings";
                this.Size = new Size(400, 350);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                // Color selection
                Label colorLabel = new Label
                {
                    Text = "Sticker Color:",
                    Location = new Point(20, 20),
                    Size = new Size(100, 20)
                };

                colorComboBox = new ComboBox
                {
                    Location = new Point(130, 20),
                    Size = new Size(150, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                colorComboBox.Items.AddRange(new string[] { "Red", "Green", "Blue", "Yellow", "White", "Custom" });

                colorPickerButton = new Button
                {
                    Text = "Pick Color",
                    Location = new Point(290, 20),
                    Size = new Size(80, 25),
                    Enabled = false
                };

                colorPickerButton.Click += ColorPickerButton_Click;
                colorComboBox.SelectedIndexChanged += ColorComboBox_SelectedIndexChanged;

                // Color tolerance
                Label toleranceLabel = new Label
                {
                    Text = "Color Tolerance:",
                    Location = new Point(20, 60),
                    Size = new Size(100, 20)
                };

                Label toleranceValueLabel = new Label
                {
                    Location = new Point(330, 60),
                    Size = new Size(40, 20)
                };

                toleranceTrackBar = new TrackBar
                {
                    Location = new Point(130, 60),
                    Size = new Size(200, 45),
                    Minimum = 10,
                    Maximum = 100,
                    TickFrequency = 10,
                    Value = 30
                };

                toleranceTrackBar.ValueChanged += (s, e) =>
                {
                    toleranceValueLabel.Text = toleranceTrackBar.Value.ToString();
                };

                // Minimum point size
                Label minSizeLabel = new Label
                {
                    Text = "Min Point Size:",
                    Location = new Point(20, 110),
                    Size = new Size(100, 20)
                };

                minSizeNumeric = new NumericUpDown
                {
                    Location = new Point(130, 110),
                    Size = new Size(100, 25),
                    Minimum = 1,
                    Maximum = 50,
                    Value = 5
                };

                // Maximum point size
                Label maxSizeLabel = new Label
                {
                    Text = "Max Point Size:",
                    Location = new Point(20, 150),
                    Size = new Size(100, 20)
                };

                maxSizeNumeric = new NumericUpDown
                {
                    Location = new Point(130, 150),
                    Size = new Size(100, 25),
                    Minimum = 5,
                    Maximum = 100,
                    Value = 30
                };

                // Buttons
                Button detectButton = new Button
                {
                    Text = "Detect Points",
                    DialogResult = DialogResult.OK,
                    Location = new Point(100, 200),
                    Size = new Size(100, 30)
                };

                Button cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(220, 200),
                    Size = new Size(100, 30)
                };

                // Tips
                Label tipsLabel = new Label
                {
                    Text = "Tips: Use bright, solid-colored stickers.\nEnsure good lighting and contrast.\nAvoid colors similar to background.",
                    Location = new Point(20, 250),
                    Size = new Size(350, 60),
                    Font = new System.Drawing.Font("Arial", 9, FontStyle.Italic)
                };

                this.Controls.AddRange(new Control[]
                {
            colorLabel, colorComboBox, colorPickerButton,
            toleranceLabel, toleranceValueLabel, toleranceTrackBar,
            minSizeLabel, minSizeNumeric,
            maxSizeLabel, maxSizeNumeric,
            detectButton, cancelButton, tipsLabel
                });

                this.AcceptButton = detectButton;
                this.CancelButton = cancelButton;
            }

            private void LoadSettings()
            {
                colorComboBox.SelectedIndex = (int)SelectedColor;
                toleranceTrackBar.Value = Tolerance;
                toleranceTrackBar_ValueChanged(null, null);
                minSizeNumeric.Value = MinSize;
                maxSizeNumeric.Value = MaxSize;
            }

            private void ColorComboBox_SelectedIndexChanged(object sender, EventArgs e)
            {
                colorPickerButton.Enabled = (colorComboBox.SelectedIndex == 5); // Custom
            }

            private void ColorPickerButton_Click(object sender, EventArgs e)
            {
                if (colorDialog == null)
                    colorDialog = new ColorDialog();

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    CustomColor = colorDialog.Color;
                }
            }

            private void toleranceTrackBar_ValueChanged(object sender, EventArgs e)
            {
                // Update the value label
                foreach (Control ctrl in this.Controls)
                {
                    if (ctrl is Label label && label.Location.X == 330 && label.Location.Y == 60)
                    {
                        label.Text = toleranceTrackBar.Value.ToString();
                        break;
                    }
                }
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    SelectedColor = (PointColor)colorComboBox.SelectedIndex;
                    Tolerance = toleranceTrackBar.Value;
                    MinSize = (int)minSizeNumeric.Value;
                    MaxSize = (int)maxSizeNumeric.Value;
                }

                base.OnFormClosing(e);
            }
        }

        public class DetectionSettingsForm : Form
        {
            // UI Controls
            private Panel previewPanel;
            private PictureBox previewPictureBox;
            private ComboBox colorComboBox;
            private Button colorPickerButton;
            private TrackBar toleranceTrackBar;
            private NumericUpDown minSizeNumeric;
            private NumericUpDown maxSizeNumeric;
            private TrackBar brightnessTrackBar;
            private TrackBar contrastTrackBar;
            private CheckBox showOriginalCheckBox;
            private CheckBox showBoundingBoxCheckBox;
            private Label toleranceValueLabel;
            private Label brightnessValueLabel;
            private Label contrastValueLabel;
            private ColorDialog colorDialog;

            // Properties
            public PointColor SelectedColor { get; private set; }
            public Color CustomColor { get; private set; }
            public int Tolerance { get; private set; }
            public int MinSize { get; private set; }
            public int MaxSize { get; private set; }
            public int Brightness { get; private set; }
            public int Contrast { get; private set; }
            public bool ShowOriginal { get; private set; }
            public bool ShowBoundingBox { get; private set; }

            // Image processing
            private Bitmap originalImage;
            private Bitmap previewImage;
            private List<System.Drawing.Rectangle> detectedAreas = new List<System.Drawing.Rectangle>();
            private System.Threading.Timer previewTimer;

            public DetectionSettingsForm(PointColor defaultColor, Color customColor,
                                        int defaultTolerance, int defaultMinSize, int defaultMaxSize,
                                        System.Drawing.Image imageToPreview)
            {
                InitializeComponent();

                SelectedColor = defaultColor;
                CustomColor = customColor;
                Tolerance = defaultTolerance;
                MinSize = defaultMinSize;
                MaxSize = defaultMaxSize;
                Brightness = 0;
                Contrast = 0;
                ShowOriginal = true;
                ShowBoundingBox = true;

                if (imageToPreview != null)
                {
                    originalImage = new Bitmap(imageToPreview);
                    previewPictureBox.Image = originalImage;
                    previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                }

                LoadSettings();
                UpdatePreview();
            }

            private void InitializeComponent()
            {
                this.Text = "Detection Settings with Preview";
                this.Size = new Size(900, 700);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;

                // Main split container
                SplitContainer mainSplit = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Horizontal,
                    SplitterDistance = 400,
                    FixedPanel = FixedPanel.Panel1,
                    BackColor = Color.FromArgb(37, 37, 38)
                };

                // Preview panel
                previewPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    BorderStyle = BorderStyle.FixedSingle
                };

                previewPictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                previewPanel.Controls.Add(previewPictureBox);
                mainSplit.Panel1.Controls.Add(previewPanel);

                // Settings panel
                Panel settingsPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(37, 37, 38),
                    AutoScroll = true
                };

                // Create settings controls
                CreateSettingsControls(settingsPanel);

                mainSplit.Panel2.Controls.Add(settingsPanel);
                this.Controls.Add(mainSplit);

                // Setup timer for preview updates (debouncing)
                previewTimer = new System.Threading.Timer(PreviewTimerCallback, null,
                    Timeout.Infinite, Timeout.Infinite);
            }

            private void CreateSettingsControls(Panel parent)
            {
                int yPos = 20;
                int labelWidth = 120;
                int controlWidth = 200;
                int valueLabelWidth = 40;

                // Color Selection
                Label colorLabel = new Label
                {
                    Text = "Sticker Color:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                colorComboBox = new ComboBox
                {
                    Location = new Point(150, yPos),
                    Size = new Size(controlWidth, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                colorComboBox.Items.AddRange(new string[] { "Red", "Green", "Blue", "Yellow", "White", "Custom" });
                colorComboBox.SelectedIndexChanged += (s, e) =>
                {
                    colorPickerButton.Enabled = (colorComboBox.SelectedIndex == 5);
                    SchedulePreviewUpdate();
                };

                colorPickerButton = new Button
                {
                    Text = "Pick Color",
                    Location = new Point(360, yPos),
                    Size = new Size(80, 25),
                    Enabled = false,
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                colorPickerButton.Click += ColorPickerButton_Click;

                yPos += 35;

                // Color tolerance
                Label toleranceLabel = new Label
                {
                    Text = "Color Tolerance:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                toleranceValueLabel = new Label
                {
                    Location = new Point(360, yPos),
                    Size = new Size(valueLabelWidth, 25),
                    ForeColor = Color.Yellow,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                toleranceTrackBar = new TrackBar
                {
                    Location = new Point(150, yPos),
                    Size = new Size(200, 45),
                    Minimum = 10,
                    Maximum = 100,
                    TickFrequency = 10,
                    Value = 30,
                    BackColor = Color.FromArgb(37, 37, 38)
                };

                toleranceTrackBar.ValueChanged += (s, e) =>
                {
                    toleranceValueLabel.Text = toleranceTrackBar.Value.ToString();
                    SchedulePreviewUpdate();
                };

                yPos += 45;

                // Minimum point size
                Label minSizeLabel = new Label
                {
                    Text = "Min Point Size:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                minSizeNumeric = new NumericUpDown
                {
                    Location = new Point(150, yPos),
                    Size = new Size(100, 25),
                    Minimum = 1,
                    Maximum = 50,
                    Value = 5,
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                minSizeNumeric.ValueChanged += (s, e) => SchedulePreviewUpdate();

                yPos += 35;

                // Maximum point size
                Label maxSizeLabel = new Label
                {
                    Text = "Max Point Size:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                maxSizeNumeric = new NumericUpDown
                {
                    Location = new Point(150, yPos),
                    Size = new Size(100, 25),
                    Minimum = 5,
                    Maximum = 100,
                    Value = 30,
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                maxSizeNumeric.ValueChanged += (s, e) => SchedulePreviewUpdate();

                yPos += 35;

                // Brightness adjustment
                Label brightnessLabel = new Label
                {
                    Text = "Brightness:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                brightnessValueLabel = new Label
                {
                    Location = new Point(360, yPos),
                    Size = new Size(valueLabelWidth, 25),
                    ForeColor = Color.Yellow,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                brightnessTrackBar = new TrackBar
                {
                    Location = new Point(150, yPos),
                    Size = new Size(200, 45),
                    Minimum = -50,
                    Maximum = 50,
                    TickFrequency = 10,
                    Value = 0,
                    BackColor = Color.FromArgb(37, 37, 38)
                };

                brightnessTrackBar.ValueChanged += (s, e) =>
                {
                    brightnessValueLabel.Text = brightnessTrackBar.Value.ToString();
                    SchedulePreviewUpdate();
                };

                yPos += 45;

                // Contrast adjustment
                Label contrastLabel = new Label
                {
                    Text = "Contrast:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                contrastValueLabel = new Label
                {
                    Location = new Point(360, yPos),
                    Size = new Size(valueLabelWidth, 25),
                    ForeColor = Color.Yellow,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                contrastTrackBar = new TrackBar
                {
                    Location = new Point(150, yPos),
                    Size = new Size(200, 45),
                    Minimum = -50,
                    Maximum = 50,
                    TickFrequency = 10,
                    Value = 0,
                    BackColor = Color.FromArgb(37, 37, 38)
                };

                contrastTrackBar.ValueChanged += (s, e) =>
                {
                    contrastValueLabel.Text = contrastTrackBar.Value.ToString();
                    SchedulePreviewUpdate();
                };

                yPos += 45;

                // Checkboxes
                showOriginalCheckBox = new CheckBox
                {
                    Text = "Show Original Image",
                    Location = new Point(20, yPos),
                    Size = new Size(150, 25),
                    Checked = true,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent
                };

                showOriginalCheckBox.CheckedChanged += (s, e) =>
                {
                    ShowOriginal = showOriginalCheckBox.Checked;
                    UpdatePreview();
                };

                showBoundingBoxCheckBox = new CheckBox
                {
                    Text = "Show Bounding Boxes",
                    Location = new Point(180, yPos),
                    Size = new Size(150, 25),
                    Checked = true,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent
                };

                showBoundingBoxCheckBox.CheckedChanged += (s, e) =>
                {
                    ShowBoundingBox = showBoundingBoxCheckBox.Checked;
                    UpdatePreview();
                };

                yPos += 35;

                // Detection Statistics
                Label statsLabel = new Label
                {
                    Text = "Detection Statistics:",
                    Location = new Point(20, yPos),
                    Size = new Size(labelWidth, 25),
                    ForeColor = Color.Cyan,
                    Font = new System.Drawing.Font("Arial", 9, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                yPos += 30;

                // Stats display labels (will be updated)
                for (int i = 0; i < 4; i++)
                {
                    Label statLabel = new Label
                    {
                        Name = $"statLabel{i}",
                        Location = new Point(30, yPos + (i * 25)),
                        Size = new Size(400, 25),
                        ForeColor = Color.LightGray,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    parent.Controls.Add(statLabel);
                }

                yPos += 120;

                // Buttons
                Button applyButton = new Button
                {
                    Text = "Apply Settings",
                    Location = new Point(100, yPos),
                    Size = new Size(120, 35),
                    BackColor = Color.FromArgb(0, 122, 204),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.OK
                };

                Button cancelButton = new Button
                {
                    Text = "Cancel",
                    Location = new Point(250, yPos),
                    Size = new Size(120, 35),
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Cancel
                };

                Button resetButton = new Button
                {
                    Text = "Reset to Default",
                    Location = new Point(400, yPos),
                    Size = new Size(120, 35),
                    BackColor = Color.FromArgb(62, 62, 64),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };

                resetButton.Click += ResetButton_Click;

                // Add all controls to parent
                parent.Controls.AddRange(new Control[]
                {
            colorLabel, colorComboBox, colorPickerButton,
            toleranceLabel, toleranceValueLabel, toleranceTrackBar,
            minSizeLabel, minSizeNumeric,
            maxSizeLabel, maxSizeNumeric,
            brightnessLabel, brightnessValueLabel, brightnessTrackBar,
            contrastLabel, contrastValueLabel, contrastTrackBar,
            showOriginalCheckBox, showBoundingBoxCheckBox,
            statsLabel,
            applyButton, cancelButton, resetButton
                });

                this.AcceptButton = applyButton;
                this.CancelButton = cancelButton;
            }

            private void LoadSettings()
            {
                colorComboBox.SelectedIndex = (int)SelectedColor;
                toleranceTrackBar.Value = Tolerance;
                toleranceValueLabel.Text = Tolerance.ToString();
                minSizeNumeric.Value = MinSize;
                maxSizeNumeric.Value = MaxSize;
                brightnessTrackBar.Value = Brightness;
                brightnessValueLabel.Text = Brightness.ToString();
                contrastTrackBar.Value = Contrast;
                contrastValueLabel.Text = Contrast.ToString();
                showOriginalCheckBox.Checked = ShowOriginal;
                showBoundingBoxCheckBox.Checked = ShowBoundingBox;
            }

            private void ColorPickerButton_Click(object sender, EventArgs e)
            {
                if (colorDialog == null)
                {
                    colorDialog = new ColorDialog
                    {
                        AnyColor = true,
                        FullOpen = true
                    };
                }

                colorDialog.Color = CustomColor;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    CustomColor = colorDialog.Color;
                    SchedulePreviewUpdate();
                }
            }

            private void ResetButton_Click(object sender, EventArgs e)
            {
                // Reset to default values
                colorComboBox.SelectedIndex = 0; // Red
                toleranceTrackBar.Value = 30;
                minSizeNumeric.Value = 5;
                maxSizeNumeric.Value = 30;
                brightnessTrackBar.Value = 0;
                contrastTrackBar.Value = 0;
                showOriginalCheckBox.Checked = true;
                showBoundingBoxCheckBox.Checked = true;

                if (colorDialog != null)
                {
                    CustomColor = Color.Red;
                }

                SchedulePreviewUpdate();
            }

            private void SchedulePreviewUpdate()
            {
                // Debounce preview updates to avoid too many redraws
                previewTimer.Change(300, Timeout.Infinite);
            }

            private void PreviewTimerCallback(object state)
            {
                // This runs on a thread pool thread, so we need to invoke on UI thread
                this.Invoke((MethodInvoker)UpdatePreview);
            }

            private void UpdatePreview()
            {
                if (originalImage == null) return;

                try
                {
                    Cursor = Cursors.WaitCursor;

                    // Get current settings
                    Color targetColor = colorComboBox.SelectedIndex == 5 ?
                        CustomColor : GetColorFromEnum((PointColor)colorComboBox.SelectedIndex);

                    int tolerance = toleranceTrackBar.Value;
                    int minSize = (int)minSizeNumeric.Value;
                    int maxSize = (int)maxSizeNumeric.Value;
                    int brightness = brightnessTrackBar.Value;
                    int contrast = contrastTrackBar.Value;

                    // Process image for preview
                    Bitmap processedImage = ApplyImageAdjustments(originalImage, brightness, contrast);
                    detectedAreas = DetectColoredAreas(processedImage, targetColor, tolerance, minSize, maxSize);

                    // Create preview image
                    previewImage = ShowOriginal ?
                        new Bitmap(originalImage) :
                        new Bitmap(processedImage);

                    // Draw detection results
                    using (Graphics g = Graphics.FromImage(previewImage))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                        if (ShowBoundingBox && detectedAreas.Count > 0)
                        {
                            DrawDetectionResults(g, detectedAreas, targetColor);
                        }
                    }

                    // Update preview
                    previewPictureBox.Image = previewImage;

                    // Update statistics
                    UpdateStatistics(detectedAreas.Count);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating preview: {ex.Message}", "Preview Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }

            private Color GetColorFromEnum(PointColor pointColor)
            {
                switch (pointColor)
                {
                    case PointColor.Red: return Color.Red;
                    case PointColor.Green: return Color.Green;
                    case PointColor.Blue: return Color.Blue;
                    case PointColor.Yellow: return Color.Yellow;
                    case PointColor.White: return Color.White;
                    default: return Color.Red;
                }
            }

            private Bitmap ApplyImageAdjustments(Bitmap source, int brightness, int contrast)
            {
                Bitmap adjusted = new Bitmap(source.Width, source.Height);

                // Simple brightness/contrast adjustment
                float brightnessFactor = brightness / 100.0f;
                float contrastFactor = (contrast + 100) / 100.0f;
                contrastFactor *= contrastFactor; // Square for more noticeable effect

                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        Color pixel = source.GetPixel(x, y);

                        // Apply brightness
                        int r = (int)(pixel.R + (brightnessFactor * 255));
                        int g = (int)(pixel.G + (brightnessFactor * 255));
                        int b = (int)(pixel.B + (brightnessFactor * 255));

                        // Apply contrast
                        r = (int)(((r / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);
                        g = (int)(((g / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);
                        b = (int)(((b / 255.0f - 0.5f) * contrastFactor + 0.5f) * 255);

                        // Clamp values
                        r = Math.Max(0, Math.Min(255, r));
                        g = Math.Max(0, Math.Min(255, g));
                        b = Math.Max(0, Math.Min(255, b));

                        adjusted.SetPixel(x, y, Color.FromArgb(pixel.A, r, g, b));
                    }
                }

                return adjusted;
            }

            private List<System.Drawing.Rectangle> DetectColoredAreas(Bitmap image, Color targetColor,
                                                      int tolerance, int minSize, int maxSize)
            {
                List<System.Drawing.Rectangle> areas = new List<System.Drawing.Rectangle>();
                bool[,] visited = new bool[image.Width, image.Height];

                for (int x = 0; x < image.Width; x += 2) // Sample every 2 pixels for speed
                {
                    for (int y = 0; y < image.Height; y += 2)
                    {
                        if (!visited[x, y])
                        {
                            Color pixelColor = image.GetPixel(x, y);

                            if (IsColorSimilar(pixelColor, targetColor, tolerance))
                            {
                                System.Drawing.Rectangle bounds = FloodFillBounds(image, x, y, targetColor,
                                                                  tolerance, visited);

                                int area = bounds.Width * bounds.Height;
                                if (area >= minSize && area <= maxSize)
                                {
                                    areas.Add(bounds);
                                }
                            }
                        }
                    }
                }

                return areas;
            }

            private bool IsColorSimilar(Color c1, Color c2, int tolerance)
            {
                int rDiff = c1.R - c2.R;
                int gDiff = c1.G - c2.G;
                int bDiff = c1.B - c2.B;

                double distance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
                return distance <= tolerance;
            }

            private System.Drawing.Rectangle FloodFillBounds(Bitmap image, int startX, int startY,
                                             Color targetColor, int tolerance, bool[,] visited)
            {
                int minX = startX, maxX = startX;
                int minY = startY, maxY = startY;
                int pixelCount = 0;

                Stack<Point> stack = new Stack<Point>();
                stack.Push(new Point(startX, startY));

                while (stack.Count > 0)
                {
                    Point p = stack.Pop();

                    if (p.X < 0 || p.X >= image.Width ||
                        p.Y < 0 || p.Y >= image.Height ||
                        visited[p.X, p.Y])
                        continue;

                    Color pixelColor = image.GetPixel(p.X, p.Y);

                    if (IsColorSimilar(pixelColor, targetColor, tolerance))
                    {
                        visited[p.X, p.Y] = true;
                        pixelCount++;

                        // Update bounds
                        minX = Math.Min(minX, p.X);
                        maxX = Math.Max(maxX, p.X);
                        minY = Math.Min(minY, p.Y);
                        maxY = Math.Max(maxY, p.Y);

                        // Add neighbors (4-directional for speed)
                        if (p.X > 0) stack.Push(new Point(p.X - 1, p.Y));
                        if (p.X < image.Width - 1) stack.Push(new Point(p.X + 1, p.Y));
                        if (p.Y > 0) stack.Push(new Point(p.X, p.Y - 1));
                        if (p.Y < image.Height - 1) stack.Push(new Point(p.X, p.Y + 1));
                    }
                }

                return new System.Drawing.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }

            private void DrawDetectionResults(Graphics g, List<System.Drawing.Rectangle> areas, Color targetColor)
            {
                int i = 1;
                foreach (var area in areas)
                {
                    // Draw bounding box
                    using (Pen boxPen = new Pen(Color.Lime, 2))
                    {
                        g.DrawRectangle(boxPen, area);
                    }

                    // Draw center point
                    Point center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
                    using (Brush centerBrush = new SolidBrush(Color.Cyan))
                    {
                        g.FillEllipse(centerBrush, center.X - 3, center.Y - 3, 6, 6);
                    }

                    // Draw area number
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 10, FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.Yellow))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(128, Color.Black)))
                    {
                        string text = i.ToString();
                        SizeF textSize = g.MeasureString(text, font);

                        RectangleF textRect = new RectangleF(
                            area.X,
                            area.Y - textSize.Height - 2,
                            textSize.Width + 4,
                            textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(text, font, textBrush, area.X + 2, area.Y - textSize.Height);
                    }

                    i++;
                }
            }

            private void UpdateStatistics(int detectedCount)
            {
                // Update statistics labels
                for (int i = 0; i < 4; i++)
                {
                    Label statLabel = this.Controls.Find($"statLabel{i}", true).FirstOrDefault() as Label;
                    if (statLabel != null)
                    {
                        switch (i)
                        {
                            case 0:
                                statLabel.Text = $"Detected Points: {detectedCount}";
                                break;
                            case 1:
                                statLabel.Text = $"Color: {colorComboBox.SelectedItem}";
                                break;
                            case 2:
                                statLabel.Text = $"Tolerance: {toleranceTrackBar.Value}";
                                break;
                            case 3:
                                statLabel.Text = $"Size Range: {minSizeNumeric.Value} - {maxSizeNumeric.Value} pixels";
                                break;
                        }
                    }
                }
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                // Save current settings
                if (this.DialogResult == DialogResult.OK)
                {
                    SelectedColor = (PointColor)colorComboBox.SelectedIndex;
                    Tolerance = toleranceTrackBar.Value;
                    MinSize = (int)minSizeNumeric.Value;
                    MaxSize = (int)maxSizeNumeric.Value;
                    Brightness = brightnessTrackBar.Value;
                    Contrast = contrastTrackBar.Value;
                    ShowOriginal = showOriginalCheckBox.Checked;
                    ShowBoundingBox = showBoundingBoxCheckBox.Checked;
                }

                // Clean up timer
                previewTimer?.Dispose();

                base.OnFormClosing(e);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    previewTimer?.Dispose();
                    originalImage?.Dispose();
                    previewImage?.Dispose();
                }
                base.Dispose(disposing);
            }
        }



        /////
        private void FindAllIntersections()
        {
            intersectionPoints.Clear();

            // Filtrer seulement les mesures qui sont des lignes (pas des points simples)
            var lineMeasurements = measurements.Where(m =>
                m.Type == MeasurementType.Line ||
                m.Type == MeasurementType.Distance ||
                m.Type == MeasurementType.ReferenceLine ||
                m.Type == MeasurementType.PerpendicularLine ||
                m.Type == MeasurementType.Angle ||
                m.Type == MeasurementType.AngleWithAxis).ToList();

            // Pour chaque paire de lignes
            for (int i = 0; i < lineMeasurements.Count; i++)
            {
                for (int j = i + 1; j < lineMeasurements.Count; j++)
                {
                    var line1 = lineMeasurements[i];
                    var line2 = lineMeasurements[j];

                    // Obtenir les points de début et fin pour chaque ligne
                    Point line1Start, line1End, line2Start, line2End;

                    // Gérer les segments d'angle différemment
                    if (line1.Type == MeasurementType.Angle && line1.Vertex.HasValue)
                    {
                        line1Start = line1.Vertex.Value;
                        line1End = line1.End;
                    }
                    else
                    {
                        line1Start = line1.Start;
                        line1End = line1.End;
                    }

                    if (line2.Type == MeasurementType.Angle && line2.Vertex.HasValue)
                    {
                        line2Start = line2.Vertex.Value;
                        line2End = line2.End;
                    }
                    else
                    {
                        line2Start = line2.Start;
                        line2End = line2.End;
                    }

                    // 1. Vérifier l'intersection exacte des segments
                    Point? exactIntersection = FindLineIntersection(line1Start, line1End, line2Start, line2End);

                    if (exactIntersection.HasValue)
                    {
                        AddIntersectionPoint(exactIntersection.Value, line1.ID, line2.ID, IntersectionType.Exact);
                    }
                    else
                    {
                        // 2. Vérifier la proximité des extrémités
                        CheckProximityIntersections(line1, line2, line1Start, line1End, line2Start, line2End);

                        // 3. Vérifier si les lignes partagent un point terminal
                        CheckTerminalIntersections(line1, line2, line1Start, line1End, line2Start, line2End);
                    }
                }
            }

            // Calculer les angles pour chaque point d'intersection
            CalculateAllAngles();

            AddIntersectionAnglesToMeasurements();


        }

        private Point? FindLineIntersection(Point p1, Point p2, Point p3, Point p4)
        {
            // Formule d'intersection de segments
            float denom = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);

            if (Math.Abs(denom) < 0.0001)
                return null; // Lignes parallèles

            float ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / denom;
            float ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / denom;

            // Vérifier si l'intersection est dans les segments
            if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
            {
                int x = (int)(p1.X + ua * (p2.X - p1.X));
                int y = (int)(p1.Y + ua * (p2.Y - p1.Y));
                return new Point(x, y);
            }

            return null;
        }

        private void CalculateAllAngles()
        {
            for (int i = 0; i < intersectionPoints.Count; i++)
            {
                var ip = intersectionPoints[i];
                ip.Angles.Clear();

                if (ip.LineIDs.Count < 2) continue;

                // Get the lines at this intersection
                var lines = measurements.Where(m => ip.LineIDs.Contains(m.ID)).ToList();

                // For each pair of lines at this intersection
                for (int j = 0; j < lines.Count; j++)
                {
                    for (int k = j + 1; k < lines.Count; k++)
                    {
                        var line1 = lines[j];
                        var line2 = lines[k];

                        // Get vectors for each line at the intersection point
                        PointF vector1 = GetLineVectorAtIntersection(line1, ip.Location);
                        PointF vector2 = GetLineVectorAtIntersection(line2, ip.Location);

                        // Calculate angle between vectors
                        double dot = vector1.X * vector2.X + vector1.Y * vector2.Y;
                        double cross = vector1.X * vector2.Y - vector1.Y * vector2.X;
                        double mag1 = Math.Sqrt(vector1.X * vector1.X + vector1.Y * vector1.Y);
                        double mag2 = Math.Sqrt(vector2.X * vector2.X + vector2.Y * vector2.Y);

                        if (mag1 == 0 || mag2 == 0) continue;

                        double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
                        double angleRad = Math.Acos(cosTheta);
                        double angleDeg = angleRad * (180 / Math.PI);

                        // Determine which angle to store
                        double acuteAngle = Math.Min(angleDeg, 180 - angleDeg);
                        double obtuseAngle = 180 - acuteAngle;

                        // Add both angles to the intersection point
                        ip.Angles.Add(new Tuple<int, int, double>(
                            line1.ID, line2.ID, Math.Round(acuteAngle, 1)));
                        ip.Angles.Add(new Tuple<int, int, double>(
                            line1.ID, line2.ID, Math.Round(obtuseAngle, 1)));
                    }
                }

                intersectionPoints[i] = ip;
            }
        }

        private PointF GetLineVectorAtIntersection(Measurement line, Point intersection)
        {
            // Determine which endpoint is closer to the intersection
            double distToStart = CalculateDistance(line.Start, intersection);
            double distToEnd = CalculateDistance(line.End, intersection);

            // Return vector from intersection to the other endpoint
            if (distToStart < distToEnd)
            {
                // Intersection is near the start, vector goes to end
                return new PointF(line.End.X - intersection.X, line.End.Y - intersection.Y);
            }
            else
            {
                // Intersection is near the end, vector goes to start
                return new PointF(line.Start.X - intersection.X, line.Start.Y - intersection.Y);
            }
        }
        private PointF GetLineVectorAtPoint(Measurement line, Point intersectionPoint)
        {
            Point start, end;

            if (line.Type == MeasurementType.Angle && line.Vertex.HasValue)
            {
                start = line.Vertex.Value;
                end = line.End;
            }
            else
            {
                start = line.Start;
                end = line.End;
            }

            // Déterminer quelle extrémité est la plus proche du point d'intersection
            double distToStart = CalculateDistance(intersectionPoint, start);
            double distToEnd = CalculateDistance(intersectionPoint, end);

            // Retourner le vecteur depuis l'intersection vers l'autre extrémité
            if (distToStart < distToEnd)
            {
                // Point d'intersection est proche du début, vecteur vers la fin
                return new PointF(end.X - intersectionPoint.X, end.Y - intersectionPoint.Y);
            }
            else
            {
                // Point d'intersection est proche de la fin, vecteur vers le début
                return new PointF(start.X - intersectionPoint.X, start.Y - intersectionPoint.Y);
            }
        }

        private List<PointF> GetLineVectorsAtPoint(Measurement line, Point intersectionPoint)
        {
            List<PointF> vectors = new List<PointF>();
            Point start, end;

            if (line.Type == MeasurementType.Angle && line.Vertex.HasValue)
            {
                start = line.Vertex.Value;
                end = line.End;
            }
            else
            {
                start = line.Start;
                end = line.End;
            }

            // Vecteur depuis l'intersection vers le début
            PointF vectorToStart = new PointF(start.X - intersectionPoint.X, start.Y - intersectionPoint.Y);

            // Vecteur depuis l'intersection vers la fin
            PointF vectorToEnd = new PointF(end.X - intersectionPoint.X, end.Y - intersectionPoint.Y);

            // Si l'intersection est exactement à une extrémité, on ne prend que le vecteur vers l'autre extrémité
            if (CalculateDistance(intersectionPoint, start) < 1)
            {
                vectors.Add(vectorToEnd);
            }
            else if (CalculateDistance(intersectionPoint, end) < 1)
            {
                vectors.Add(vectorToStart);
            }
            else
            {
                // Pour une intersection au milieu de la ligne, on considère les deux directions
                vectors.Add(vectorToStart);
                vectors.Add(vectorToEnd);
            }

            return vectors;
        }
        private List<double> CalculateAnglesBetweenVectors(PointF v1, PointF v2)
        {
            List<double> angles = new List<double>();

            double dot = v1.X * v2.X + v1.Y * v2.Y;
            double cross = v1.X * v2.Y - v1.Y * v2.X; // Produit vectoriel pour le sens
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0) return new List<double> { 0, 180 };

            double cosTheta = Math.Max(-1, Math.Min(1, dot / (mag1 * mag2)));
            double angleRad = Math.Acos(cosTheta);
            double angleDeg = angleRad * (180 / Math.PI);

            // Angle aigu (0-90°) ou droit
            double acuteAngle = Math.Min(angleDeg, 180 - angleDeg);

            // Angle obtus (90-180°)
            double obtuseAngle = 180 - acuteAngle;

            // Si les lignes sont perpendiculaires (≈90°)
            if (Math.Abs(acuteAngle - 90) < 0.1)
            {
                angles.Add(90);
                angles.Add(90);
            }
            else
            {
                angles.Add(Math.Round(acuteAngle, 1));
                angles.Add(Math.Round(obtuseAngle, 1));
            }

            return angles;
        }
        private void DrawIntersectionPoints(Graphics g)
        {
            foreach (var ip in intersectionPoints)
            {
                Color pointColor = GetIntersectionColor(ip.Type);
                int pointSize = Math.Max(4, (int)(8 / zoomFactor));

                using (Brush brush = new SolidBrush(pointColor))
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    // Dessiner le point
                    g.FillEllipse(brush, ip.Location.X - pointSize / 2, ip.Location.Y - pointSize / 2,
                                 pointSize, pointSize);
                    g.DrawEllipse(pen, ip.Location.X - pointSize / 2, ip.Location.Y - pointSize / 2,
                                 pointSize, pointSize);
                }

                // Si c'est le point survolé ou sélectionné, le mettre en évidence
                if ((hoveredIntersection.HasValue && hoveredIntersection.Value.ID == ip.ID) ||
                    (selectedIntersection.HasValue && selectedIntersection.Value.ID == ip.ID))
                {
                    using (Pen highlightPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawEllipse(highlightPen, ip.Location.X - pointSize, ip.Location.Y - pointSize,
                                     pointSize * 2, pointSize * 2);
                    }

                    // Afficher l'ID du point
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", Math.Max(8, 10 / zoomFactor)))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    {
                        string idText = $"P{ip.ID}";
                        SizeF textSize = g.MeasureString(idText, font);

                        RectangleF textRect = new RectangleF(
                            ip.Location.X - textSize.Width / 2,
                            ip.Location.Y - textSize.Height - pointSize - 5,
                            textSize.Width + 4,
                            textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(idText, font, textBrush,
                            ip.Location.X - textSize.Width / 2 + 2,
                            ip.Location.Y - textSize.Height - pointSize - 3);
                    }
                }

                // AJOUTER CETTE PARTIE - Si c'est le point sélectionné, dessiner aussi les angles
                if (selectedIntersection.HasValue && selectedIntersection.Value.ID == ip.ID)
                {
                    DrawIntersectionAngles(g, ip);
                }
            }
        }
        private Color GetIntersectionColor(IntersectionType type)
        {
            switch (type)
            {
                case IntersectionType.Exact: return Color.Red;
                case IntersectionType.Proximity: return Color.Blue;
                case IntersectionType.Terminal: return Color.Green;
                default: return Color.Gray;
            }
        }

        private void CheckProximityIntersections(Measurement line1, Measurement line2,
                                                 Point line1Start, Point line1End,
                                                 Point line2Start, Point line2End)
        {
            // Vérifier la proximité entre les extrémités
            if (CalculateDistance(line1Start, line2Start) < intersectionTolerance)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID, IntersectionType.Proximity);
            }
            if (CalculateDistance(line1Start, line2End) < intersectionTolerance)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID, IntersectionType.Proximity);
            }
            if (CalculateDistance(line1End, line2Start) < intersectionTolerance)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID, IntersectionType.Proximity);
            }
            if (CalculateDistance(line1End, line2End) < intersectionTolerance)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID, IntersectionType.Proximity);
            }
        }

        private void CheckTerminalIntersections(Measurement line1, Measurement line2,
                                                Point line1Start, Point line1End,
                                                Point line2Start, Point line2End)
        {
            // Vérifier les points terminaux communs exacts
            if (line1Start == line2Start || line1Start == line2End)
            {
                AddIntersectionPoint(line1Start, line1.ID, line2.ID, IntersectionType.Terminal);
            }
            if (line1End == line2Start || line1End == line2End)
            {
                AddIntersectionPoint(line1End, line1.ID, line2.ID, IntersectionType.Terminal);
            }
        }

        private void AddIntersectionPoint(Point location, int line1Id, int line2Id, IntersectionType type)
        {
            // Vérifier si un point d'intersection existe déjà à cet emplacement
            var existing = intersectionPoints.FirstOrDefault(ip =>
                CalculateDistance(ip.Location, location) < intersectionTolerance);

            if (existing.ID == 0) // Nouveau point
            {
                IntersectionPoint newPoint = new IntersectionPoint(location, intersectionCounter++);
                newPoint.Type = type;

                if (!newPoint.LineIDs.Contains(line1Id))
                    newPoint.LineIDs.Add(line1Id);
                if (!newPoint.LineIDs.Contains(line2Id))
                    newPoint.LineIDs.Add(line2Id);

                intersectionPoints.Add(newPoint);
            }
            else // Point existant
            {
                int index = intersectionPoints.IndexOf(existing);
                existing = intersectionPoints[index];

                if (!existing.LineIDs.Contains(line1Id))
                    existing.LineIDs.Add(line1Id);
                if (!existing.LineIDs.Contains(line2Id))
                    existing.LineIDs.Add(line2Id);

                intersectionPoints[index] = existing;
            }
        }
        ////
        private string GetIntersectionDataForPdf()
        {
            if (intersectionPoints.Count == 0)
                return "No intersection points detected.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INTERSECTION POINTS ANALYSIS");
            sb.AppendLine("=============================");

            foreach (var ip in intersectionPoints.OrderBy(p => p.ID))
            {
                sb.AppendLine();
                sb.AppendLine($"Intersection Point P{ip.ID}");
                sb.AppendLine($"Type: {ip.Type}");
                sb.AppendLine($"Coordinates: ({ip.Location.X}, {ip.Location.Y})");
                sb.AppendLine($"Lines involved: {string.Join(", ", ip.LineIDs.Select(id => $"L{id}"))}");

                if (ip.Angles.Count > 0)
                {
                    sb.AppendLine("Angles between lines:");

                    // Group angles by line pairs
                    var angleGroups = ip.Angles
                        .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                        .Select(g => new
                        {
                            Line1 = g.Key.Line1,
                            Line2 = g.Key.Line2,
                            Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                        })
                        .OrderBy(g => g.Line1).ThenBy(g => g.Line2);

                    foreach (var group in angleGroups)
                    {
                        if (group.Angles.Count == 2)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}:");
                            sb.AppendLine($"    Acute angle: {group.Angles[0]:F1}°");
                            sb.AppendLine($"    Obtuse angle: {group.Angles[1]:F1}°");
                            sb.AppendLine($"    Sum: {(group.Angles[0] + group.Angles[1]):F1}°");
                        }
                        else if (group.Angles.Count == 1)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}: {group.Angles[0]:F1}°");
                            if (Math.Abs(group.Angles[0] - 90) < 0.1)
                                sb.AppendLine("    → RIGHT ANGLE");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("No angle measurements available");
                }

                sb.AppendLine(new string('-', 50));
            }

            return sb.ToString();
        }
        ////
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

        //////////

        private void AddIntersectionAnglesToMeasurements()
        {
            // First, remove any existing intersection angles to avoid duplicates
            measurements.RemoveAll(m => m.AngleValue.HasValue);

            int newIdStart = idCounter; // Start from current idCounter

            foreach (var ip in intersectionPoints)
            {
                if (ip.Angles.Count == 0) continue;

                // Group angles by line pairs and get distinct values
                var distinctAngles = ip.Angles
                    .GroupBy(a => new {
                        Line1 = Math.Min(a.Item1, a.Item2),
                        Line2 = Math.Max(a.Item1, a.Item2),
                        Angle = a.Item3
                    })
                    .Select(g => new
                    {
                        Line1 = g.Key.Line1,
                        Line2 = g.Key.Line2,
                        Angle = g.Key.Angle
                    })
                    .ToList();

                // Create measurements for each angle
                foreach (var angleData in distinctAngles)
                {
                    // Generate a name based on angle type
                    string angleType = (angleData.Angle < 90) ? "A" :
                                      (Math.Abs(angleData.Angle - 90) < 0.5) ? "R" : "O";

                    string name = $"IA{idCounter}{angleType}";

                    // Create the measurement
                    Measurement angleMeasurement = CreateIntersectionAngleMeasurement(
                        name, idCounter, ip.Location, angleData.Angle,
                        angleData.Line1, angleData.Line2);

                    measurements.Add(angleMeasurement);
                    idCounter++;
                }
            }

            UpdateMeasurementsList();
            drawingPanel.Invalidate();
        }
        private int GetNextAngleMeasurementNumber()
        {
            // Find the highest existing intersection angle measurement number
            int maxNumber = 0;
            foreach (var m in measurements)
            {
                if (m.AngleValue.HasValue && m.Name.StartsWith("IA"))
                {
                    // Extract number from name like "IA1A" or "IA2"
                    string numberPart = m.Name.Substring(2); // Remove "IA"

                    // Remove any trailing letters
                    while (numberPart.Length > 0 && !char.IsDigit(numberPart.Last()))
                    {
                        numberPart = numberPart.Substring(0, numberPart.Length - 1);
                    }

                    if (int.TryParse(numberPart, out int num))
                    {
                        maxNumber = Math.Max(maxNumber, num);
                    }
                }
            }
            return maxNumber + 1;
        }

        // Create a new static method to properly create intersection angle measurements
        private static Measurement CreateIntersectionAngleMeasurement(string name, int id, Point vertex,
                                                                     double angleValue, int line1Id, int line2Id)
        {
            var measurement = new Measurement(vertex, vertex, name, MeasurementType.Angle, id);
            measurement.Vertex = vertex;
            measurement.AngleValue = angleValue;
            measurement.RelatedLineIDs = new List<int> { line1Id, line2Id };
            return measurement;
        }
        private void CreateIntersectionAngleMeasurement(IntersectionPoint ip, int line1Id, int line2Id,
                                                        double angleValue, string name, int id)
        {
            // Find the lines that create this angle
            var line1 = measurements.FirstOrDefault(m => m.ID == line1Id);
            var line2 = measurements.FirstOrDefault(m => m.ID == line2Id);

            if (line1.Type == MeasurementType.None || line2.Type == MeasurementType.None)
                return;

            // Create a new measurement for this intersection angle
            Measurement angleMeasurement = new Measurement(
                ip.Location, // Use intersection point as start
                ip.Location, // Same point for end (since it's just an angle)
                name,
                MeasurementType.Angle,
                idCounter++
            );

            // Store additional information in a custom way
            // We'll add properties to store the angle value and which lines it's between
            angleMeasurement.Vertex = ip.Location; // Store vertex location

            // We need to store the angle value somehow. Let's modify the Measurement struct:
            // (I'll show how to modify it after this method)

            measurements.Add(angleMeasurement);
        }


        /////////
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
                    // Draw placeholder text...
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

                DrawDetectedPoints(e.Graphics);

                // AUGMENTATION: Dessiner les points d'intersection
                DrawIntersectionPoints(e.Graphics);

                // Draw current tool preview
                if (currentTool != ToolMode.None)
                {
                    DrawCurrentToolPreview(e.Graphics);
                }

                // Reset transformation for UI elements
                e.Graphics.ResetTransform();

                // Draw hover information
                if (hoverPoint.HasValue && !string.IsNullOrEmpty(hoverMeasurementName))
                {
                    PointF screenHoverPoint = TransformPointToScreen(hoverPoint.Value);
                    DrawHoverLabel(e.Graphics, new Point((int)screenHoverPoint.X, (int)screenHoverPoint.Y),
                                 hoverMeasurementName);
                }

                // Draw zoom level
                DrawZoomLevel(e.Graphics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drawing error: {ex.Message}");
                // Error handling...
            }
        }

        // CRÉER la fonction CreateLineBetweenPoints :
        private void CreateLineBetweenPoints(Point point1, DetectedPoint point2)
        {
            // Trouver les IDs des points dans les mesures
            int point1Id = 0;
            int point2Id = point2.ID;

            // Chercher point1 dans les mesures
            foreach (var measurement in measurements)
            {
                if (measurement.Type == MeasurementType.Point &&
                    measurement.Start == point1)
                {
                    point1Id = measurement.ID;
                    break;
                }
            }

            // Si point1 vient d'un point détecté, chercher dans detectedPoints
            if (point1Id == 0)
            {
                foreach (var point in detectedPoints)
                {
                    if (point.Location == point1)
                    {
                        point1Id = point.ID;
                        break;
                    }
                }
            }

            // Créer le nom de la ligne
            string lineName = $"L{measurementCounter++}";

            // Demander un nom personnalisé (optionnel)
            using (var renameDialog = new CustomRenameDialog(lineName,
                $"Créer une ligne entre le point {point1Id} et le point {point2Id}"))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    lineName = string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                              lineName : renameDialog.NewName.Trim();
                }
            }

            // Créer la mesure de ligne
            Measurement lineMeasurement = new Measurement(
                point1,
                point2.Location,
                lineName,
                MeasurementType.Line,
                idCounter++);

            measurements.Add(lineMeasurement);

            // Recalculer les intersections
            FindAllIntersections();

            UpdateMeasurementsList();
            drawingPanel.Invalidate();

            UpdateStatus($"Ligne créée: {lineName} entre P{point1Id} et P{point2Id}");
        }

        ////////
        private void DrawDetectedPoints(Graphics g)
        {
            if (detectedPoints == null || detectedPoints.Count == 0)
                return;

            g.Transform = transformMatrix;

            foreach (var point in detectedPoints)
            {
                Color pointColor = colorMap[point.Color];
                int pointSize = Math.Max(3, (int)(point.Radius / zoomFactor));

                // Vérifier si ce point est surligné
                bool isHighlighted =
                    highlightedPoint.HasValue &&
                    point.Location == highlightedPoint.Value;

                // --- SURBRILLANCE ---
                if (isHighlighted)
                {
                    using (Pen highlightPen = new Pen(Color.Yellow, 2))
                    {
                        g.DrawEllipse(
                            highlightPen,
                            point.Location.X - pointSize - 5,
                            point.Location.Y - pointSize - 5,
                            (pointSize + 5) * 2,
                            (pointSize + 5) * 2
                        );
                    }
                }

                // --- DESSIN DU POINT ---
                using (Brush brush = new SolidBrush(pointColor))
                using (Pen pen = new Pen(Color.Black, 1))
                {
                    g.FillEllipse(
                        brush,
                        point.Location.X - pointSize / 2,
                        point.Location.Y - pointSize / 2,
                        pointSize,
                        pointSize
                    );

                    g.DrawEllipse(
                        pen,
                        point.Location.X - pointSize / 2,
                        point.Location.Y - pointSize / 2,
                        pointSize,
                        pointSize
                    );

                    // --- DESSIN DE L'ID ---
                    using (System.Drawing.Font font = new System.Drawing.Font(
                        "Arial",
                        Math.Max(8, 10 / zoomFactor),
                        FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black)))
                    {
                        string idText = $"P{point.ID}";
                        SizeF textSize = g.MeasureString(idText, font);

                        RectangleF textRect = new RectangleF(
                            point.Location.X - textSize.Width / 2,
                            point.Location.Y + pointSize + 2,
                            textSize.Width + 4,
                            textSize.Height
                        );

                        g.FillRectangle(bgBrush, textRect);

                        g.DrawString(
                            idText,
                            font,
                            textBrush,
                            point.Location.X - textSize.Width / 2 + 2,
                            point.Location.Y + pointSize + 4
                        );
                    }
                }
            }

            // --- LANDMARKS CORPORELS ---
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
                    // Draw landmark point
                    g.FillEllipse(brush,
                        landmark.Location.X - pointSize / 2,
                        landmark.Location.Y - pointSize / 2,
                        pointSize, pointSize);
                    g.DrawEllipse(pen,
                        landmark.Location.X - pointSize / 2,
                        landmark.Location.Y - pointSize / 2,
                        pointSize, pointSize);

                    // Draw landmark name
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", Math.Max(8, 10 / zoomFactor), FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(200, Color.Blue)))
                    {
                        SizeF textSize = g.MeasureString(landmark.Name, font);

                        RectangleF textRect = new RectangleF(
                            landmark.Location.X - textSize.Width / 2,
                            landmark.Location.Y - textSize.Height - pointSize - 5,
                            textSize.Width + 4,
                            textSize.Height);

                        g.FillRectangle(bgBrush, textRect);
                        g.DrawString(landmark.Name, font, textBrush,
                            landmark.Location.X - textSize.Width / 2 + 2,
                            landmark.Location.Y - textSize.Height - pointSize - 3);
                    }
                }
            }
        }

        ////////
        private void DrawCurrentToolPreview(Graphics g)
        {
            Point currentPos = drawingPanel.PointToClient(Cursor.Position);
            PointF imageCurrentPos = TransformPointToImage(currentPos);

            // Validation
            if (float.IsNaN(imageCurrentPos.X) || float.IsNaN(imageCurrentPos.Y))
                return;

            Point imagePoint = new Point(
                (int)imageCurrentPos.X,
                (int)imageCurrentPos.Y);

            // =========================================================
            // INDICATEUR VISUEL : CONNEXION ENTRE DEUX POINTS
            // =========================================================
            if (isCreatingLineBetweenPoints && selectedPointForLine.HasValue)
            {
                using (Pen connectionPen = new Pen(Color.Cyan, 2)
                {
                    DashStyle = DashStyle.Dash
                })
                {
                    g.DrawLine(connectionPen,
                               selectedPointForLine.Value,
                               imagePoint);
                }
            }

            // =========================================================
            // PREVIEW DES OUTILS ACTIFS
            // =========================================================
            using (Pen tempPen = new Pen(Color.Yellow, 2)
            {
                DashStyle = DashStyle.Dash
            })
            {
                if (currentTool == ToolMode.Angle)
                {
                    if (angleVertex.HasValue && angleFirstPoint.HasValue)
                    {
                        if (IsValidPoint(angleVertex.Value) &&
                            IsValidPoint(angleFirstPoint.Value))
                        {
                            g.DrawLine(tempPen, angleVertex.Value, angleFirstPoint.Value);
                            g.DrawLine(tempPen, angleVertex.Value, imagePoint);

                            DrawAngleArcPreview(
                                g,
                                angleVertex.Value,
                                angleFirstPoint.Value,
                                imagePoint);
                        }
                    }
                    else if (angleVertex.HasValue && IsValidPoint(angleVertex.Value))
                    {
                        g.DrawLine(tempPen, angleVertex.Value, imagePoint);
                    }
                }
                else if (currentTool == ToolMode.AngleWithAxis)
                {
                    if (currentStartPoint.HasValue &&
                        IsValidPoint(currentStartPoint.Value))
                    {
                        g.DrawLine(tempPen, currentStartPoint.Value, imagePoint);
                    }
                }
                else if (currentStartPoint.HasValue &&
                         IsValidPoint(currentStartPoint.Value))
                {
                    g.DrawLine(tempPen, currentStartPoint.Value, imagePoint);

                    // Aide visuelle pour angles droits
                    if (currentTool == ToolMode.Line ||
                        currentTool == ToolMode.Distance)
                    {
                        DrawAngleHelpers(
                            g,
                            currentStartPoint.Value,
                            imagePoint);
                    }
                }
                else if (currentTool == ToolMode.Perpendicular &&
                         isSelectingBaseLine &&
                         selectedLineForPerpendicular.HasValue)
                {
                    Point foot;

                    if (selectedLineForPerpendicular.Value.Type == MeasurementType.Angle &&
                        selectedLineForPerpendicular.Value.Vertex.HasValue)
                    {
                        foot = CalculatePerpendicularFoot(
                            new Measurement(
                                selectedLineForPerpendicular.Value.Vertex.Value,
                                selectedLineForPerpendicular.Value.End,
                                "",
                                MeasurementType.Line,
                                0),
                            imagePoint);
                    }
                    else
                    {
                        foot = CalculatePerpendicularFoot(
                            selectedLineForPerpendicular.Value,
                            imagePoint);
                    }

                    if (IsValidPoint(foot))
                    {
                        using (Pen previewPen = new Pen(Color.Cyan, 2)
                        {
                            DashStyle = DashStyle.Dash
                        })
                        {
                            g.DrawLine(previewPen, foot, imagePoint);
                        }

                        // Symbole perpendiculaire
                        using (Brush symbolBrush = new SolidBrush(Color.Cyan))
                        {
                            g.FillRectangle(
                                symbolBrush,
                                foot.X - 3,
                                foot.Y - 3,
                                6,
                                6);
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

            // REMOVE: float fontSize = Math.Max(6, 9 / zoomFactor); // Not needed anymore

            using (Pen pen = new Pen(color, lineWidth))
            using (Brush brush = new SolidBrush(color))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);

                        // REMOVE: All text drawing for points
                        // string pointId = m.ID.ToString();
                        // SizeF idSize = g.MeasureString(pointId, font);
                        // RectangleF idRect = new RectangleF(
                        //     m.Start.X + 8, m.Start.Y - idSize.Height / 2,
                        //     idSize.Width + 4, idSize.Height);
                        // g.FillRectangle(bgBrush, idRect);
                        // g.DrawString(pointId, font, textBrush, m.Start.X + 10, m.Start.Y - idSize.Height / 2);
                        break;

                    case MeasurementType.Line:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // REMOVE: All text drawing for lines
                        // string lineId = m.ID.ToString();
                        // SizeF lineIdSize = g.MeasureString(lineId, font);
                        // PointF lineMidPoint = new PointF((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        // RectangleF lineIdRect = new RectangleF(
                        //     lineMidPoint.X - lineIdSize.Width / 2, lineMidPoint.Y - lineIdSize.Height - 10,
                        //     lineIdSize.Width + 4, lineIdSize.Height);
                        // g.FillRectangle(bgBrush, lineIdRect);
                        // g.DrawString(lineId, font, textBrush, lineMidPoint.X - lineIdSize.Width / 2 + 2, lineMidPoint.Y - lineIdSize.Height - 8);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // REMOVE: All text drawing for distance measurements
                        // double distance = CalculateDistance(m.Start, m.End);
                        // string distText = m.Type == MeasurementType.ReferenceLine ?
                        //     $"{m.ID}: {distance / pixelToRealRatio:F1} cm" :
                        //     isReferenceSet ? $"{m.ID}" : $"{m.ID}";

                        // PointF midPoint = new PointF((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        // SizeF textSize = g.MeasureString(distText, font);
                        // RectangleF textRect = new RectangleF(
                        //     midPoint.X - textSize.Width / 2, midPoint.Y - textSize.Height - 10,
                        //     textSize.Width + 4, textSize.Height);
                        // g.FillRectangle(bgBrush, textRect);
                        // g.DrawString(distText, font, textBrush, midPoint.X - textSize.Width / 2 + 2, midPoint.Y - textSize.Height - 8);
                        break;

                    case MeasurementType.Angle:
                        if (m.Vertex.HasValue)
                        {
                            if (m.AngleValue.HasValue)
                            {
                                // This is an INTERSECTION ANGLE
                                // Draw intersection angle point at the vertex
                                g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);

                                // REMOVE: All text drawing for intersection angles
                                // string angleText = m.AngleValue.Value.ToString("F1") + "°";
                                // if (m.RelatedLineIDs != null && m.RelatedLineIDs.Count >= 2)
                                // {
                                //     angleText = $"∠L{m.RelatedLineIDs[0]}-L{m.RelatedLineIDs[1]}: {angleText}";
                                // }

                                // SizeF angleTextSize = g.MeasureString(angleText, font);
                                // RectangleF angleTextRect = new RectangleF(
                                //     m.Vertex.Value.X - angleTextSize.Width / 2,
                                //     m.Vertex.Value.Y - angleTextSize.Height - 20,
                                //     angleTextSize.Width + 4,
                                //     angleTextSize.Height);
                                // g.FillRectangle(bgBrush, angleTextRect);
                                // g.DrawString(angleText, font, textBrush,
                                //     m.Vertex.Value.X - angleTextSize.Width / 2 + 2,
                                //     m.Vertex.Value.Y - angleTextSize.Height - 18);

                                // Draw a small arc to indicate it's an angle
                                using (Pen arcPen = new Pen(Color.FromArgb(150, Color.Orange), 1))
                                {
                                    // Adjust arc radius based on zoom
                                    float arcRadius = 15f / zoomFactor;
                                    g.DrawArc(arcPen,
                                        m.Vertex.Value.X - arcRadius,
                                        m.Vertex.Value.Y - arcRadius,
                                        arcRadius * 2,
                                        arcRadius * 2, 0, 120);
                                }
                            }
                            else
                            {
                                // This is a REGULAR ANGLE (created with angle tool)
                                // Draw the segment
                                g.DrawLine(pen, m.Vertex.Value, m.End);
                                g.FillEllipse(brush, m.Vertex.Value.X - pointSize / 2, m.Vertex.Value.Y - pointSize / 2, pointSize, pointSize);
                                g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                                // REMOVE: Find the other segment and draw angle value
                                // Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                //     meas.Type == MeasurementType.Angle &&
                                //     meas.Vertex.HasValue &&
                                //     meas.Vertex.Value == m.Vertex.Value &&
                                //     meas.ID == m.ID &&
                                //     meas.End != m.End);

                                // if (otherSegment.Type == MeasurementType.Angle)
                                // {
                                //     // Draw angle value at vertex with ID
                                //     double angle = CalculateAngle(m, otherSegment);
                                //     string angleText = $"{m.ID}: {angle:F1}°";

                                //     SizeF angleTextSize = g.MeasureString(angleText, font);
                                //     RectangleF angleTextRect = new RectangleF(
                                //         m.Vertex.Value.X - angleTextSize.Width / 2,
                                //         m.Vertex.Value.Y - angleTextSize.Height - 20,
                                //         angleTextSize.Width + 4,
                                //         angleTextSize.Height);
                                //     g.FillRectangle(bgBrush, angleTextRect);
                                //     g.DrawString(angleText, font, textBrush,
                                //         m.Vertex.Value.X - angleTextSize.Width / 2 + 2,
                                //         m.Vertex.Value.Y - angleTextSize.Height - 18);

                                //     // Draw angle arc
                                //     DrawAngleArc(g, m, otherSegment);
                                // }

                                // Keep only the visual arc, no text
                                Measurement otherSegment = measurements.FirstOrDefault(meas =>
                                    meas.Type == MeasurementType.Angle &&
                                    meas.Vertex.HasValue &&
                                    meas.Vertex.Value == m.Vertex.Value &&
                                    meas.ID == m.ID &&
                                    meas.End != m.End);

                                if (otherSegment.Type == MeasurementType.Angle)
                                {
                                    // Draw angle arc only, no text
                                    DrawAngleArc(g, m, otherSegment);
                                }
                            }
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        g.DrawLine(pen, m.Start, m.End);
                        g.FillEllipse(brush, m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2, pointSize, pointSize);
                        g.FillEllipse(brush, m.End.X - pointSize / 2, m.End.Y - pointSize / 2, pointSize, pointSize);

                        // REMOVE: All text drawing for axis angles
                        // double axisAngle = CalculateAngleWithAxis(m);
                        // string axisAngleText = $"{m.ID}: {axisAngle:F1}° to {m.Axis}";

                        // SizeF axisTextSize = g.MeasureString(axisAngleText, font);
                        // Point lineMidPoint1 = new Point(
                        //     (m.Start.X + m.End.X) / 2,
                        //     (m.Start.Y + m.End.Y) / 2);
                        // RectangleF axisTextRect = new RectangleF(
                        //     lineMidPoint1.X - axisTextSize.Width / 2,
                        //     lineMidPoint1.Y - axisTextSize.Height - 10,
                        //     axisTextSize.Width + 4,
                        //     axisTextSize.Height);
                        // g.FillRectangle(bgBrush, axisTextRect);
                        // g.DrawString(axisAngleText, font, textBrush,
                        //     lineMidPoint1.X - axisTextSize.Width / 2 + 2,
                        //     lineMidPoint1.Y - axisTextSize.Height - 8);

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
                            int symbolSize = (int)(4 / zoomFactor);
                            symbolSize = Math.Max(2, symbolSize);
                            g.DrawRectangle(perpendicularPen,
                                m.Start.X - symbolSize,
                                m.Start.Y - symbolSize,
                                symbolSize * 2,
                                symbolSize * 2);
                        }

                        // REMOVE: All text drawing for perpendicular lines
                        // string perpId = m.ID.ToString();
                        // SizeF perpTextSize = g.MeasureString(perpId, font);
                        // Point perpMidPoint = new Point((m.Start.X + m.End.X) / 2, (m.Start.Y + m.End.Y) / 2);
                        // RectangleF perpTextRect = new RectangleF(
                        //     perpMidPoint.X - perpTextSize.Width / 2, perpMidPoint.Y - perpTextSize.Height - 10,
                        //     perpTextSize.Width + 4, perpTextSize.Height);
                        // g.FillRectangle(bgBrush, perpTextRect);
                        // g.DrawString(perpId, font, textBrush, perpMidPoint.X - perpTextSize.Width / 2 + 2, perpMidPoint.Y - perpTextSize.Height - 8);
                        break;

                    default:
                        // Handle any other measurement types if needed
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

            // 1. D'abord vérifier si on est en mode création de ligne entre points
            if (isCreatingLineBetweenPoints && e.Button == MouseButtons.Left)
            {
                HandlePointConnection(imagePoint);
                return;
            }

            // 2. Ensuite vérifier les intersections (clic droit)
         

            // Détection des points d'intersection - Clic Droit
            if (e.Button == MouseButtons.Right)
            {
                // Chercher un point d'intersection proche
                var intersection = FindIntersectionAtPoint(imagePoint);

                if (intersection.HasValue)
                {
                    selectedIntersection = intersection;
                    ShowAngleContextMenu(e.Location, intersection.Value);
                    return;
                }
            }




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

            // Handle color picking mode
            if (isPickingReferenceColor && originalImage != null && e.Button == MouseButtons.Left)
            {
                 imagePointF = TransformPointToImage(e.Location);
                 imagePoint = new Point((int)imagePointF.X, (int)imagePointF.Y);

                using (Bitmap bmp = new Bitmap(originalImage))
                {
                    if (imagePoint.X >= 0 && imagePoint.X < bmp.Width &&
                        imagePoint.Y >= 0 && imagePoint.Y < bmp.Height)
                    {
                        Color pickedColor = bmp.GetPixel(imagePoint.X, imagePoint.Y);
                        referenceColor = pickedColor;
                        pickedPointLocation = imagePoint;

                        // Show color preview and detection options
                        ShowColorPreviewAndDetect(pickedColor, imagePoint);
                    }
                }

                isPickingReferenceColor = false;
                drawingPanel.Cursor = Cursors.Default;
            }
        }




        // New method to show color preview
        private void ShowColorPreviewAndDetect(Color pickedColor, Point pickPoint)
        {
            // Create a preview form
            Form previewForm = new Form
            {
                Text = "Color Sampled - Adjust Detection",
                Size = new Size(450, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // Sampled color preview
            Label sampledLabel = new Label
            {
                Text = "Sampled Color:",
                Location = new Point(20, 20),
                Size = new Size(100, 25),
                ForeColor = Color.White
            };

            Panel colorPanel = new Panel
            {
                BackColor = pickedColor,
                Location = new Point(130, 20),
                Size = new Size(100, 25),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label rgbLabel = new Label
            {
                Text = $"RGB: {pickedColor.R}, {pickedColor.G}, {pickedColor.B}",
                Location = new Point(240, 20),
                Size = new Size(150, 25),
                ForeColor = Color.White
            };

            // HSV values
            HsvColor hsv = RgbToHsv(pickedColor);
            Label hsvLabel = new Label
            {
                Text = $"HSV: H={hsv.H:F0}°, S={hsv.S:F2}, V={hsv.V:F2}",
                Location = new Point(20, 55),
                Size = new Size(300, 25),
                ForeColor = Color.Cyan
            };

            // Tolerance slider
            Label toleranceLabel = new Label
            {
                Text = "Color Tolerance:",
                Location = new Point(20, 100),
                Size = new Size(100, 25),
                ForeColor = Color.White
            };

            TrackBar toleranceTrackBar = new TrackBar
            {
                Location = new Point(130, 100),
                Size = new Size(200, 45),
                Minimum = 5,
                Maximum = 50,
                Value = detectionTolerance,
                TickFrequency = 5
            };

            Label toleranceValue = new Label
            {
                Text = detectionTolerance.ToString(),
                Location = new Point(340, 100),
                Size = new Size(40, 25),
                ForeColor = Color.Yellow
            };

            toleranceTrackBar.ValueChanged += (s, ev) =>
            {
                toleranceValue.Text = toleranceTrackBar.Value.ToString();
            };

            // Preview panel
            Panel previewPanel = new Panel
            {
                Location = new Point(20, 160),
                Size = new Size(400, 100),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            PictureBox previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            previewPanel.Controls.Add(previewBox);

            // Update preview when tolerance changes
            toleranceTrackBar.ValueChanged += (s, ev) =>
            {
                UpdateDetectionPreview(previewBox, pickedColor, toleranceTrackBar.Value);
            };

            // Buttons
            Button detectButton = new Button
            {
                Text = "Detect Stickers",
                Location = new Point(100, 280),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(230, 280),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            Button resetButton = new Button
            {
                Text = "Pick Another Color",
                Location = new Point(100, 320),
                Size = new Size(250, 30),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            detectButton.Click += (s, ev) =>
            {
                detectionTolerance = toleranceTrackBar.Value;
                previewForm.Close();
                DetectColoredPointsFlexible(pickedColor);
            };

            resetButton.Click += (s, ev) =>
            {
                previewForm.Close();
                isPickingReferenceColor = true;
                UpdateStatus("Click on a sticker to sample its color");
                drawingPanel.Cursor = Cursors.Cross;
            };

            // Add controls
            previewForm.Controls.AddRange(new Control[]
            {
        sampledLabel, colorPanel, rgbLabel, hsvLabel,
        toleranceLabel, toleranceTrackBar, toleranceValue,
        previewPanel, detectButton, cancelButton, resetButton
            });

            // Initial preview
            UpdateDetectionPreview(previewBox, pickedColor, detectionTolerance);

            previewForm.ShowDialog(this);
        }

        // Update preview image
        private void UpdateDetectionPreview(PictureBox previewBox, Color targetColor, int tolerance)
        {
            if (originalImage == null) return;

            using (Bitmap bmp = new Bitmap(originalImage))
            using (Bitmap preview = new Bitmap(bmp.Width, bmp.Height))
            {
                HsvColor targetHsv = RgbToHsv(targetColor);
                float hueTolerance = tolerance; // 0-50 range

                for (int y = 0; y < bmp.Height; y += 3) // Sample every 3 pixels for speed
                {
                    for (int x = 0; x < bmp.Width; x += 3)
                    {
                        Color pixel = bmp.GetPixel(x, y);
                        HsvColor pixelHsv = RgbToHsv(pixel);

                        float hueDiff = Math.Abs(pixelHsv.H - targetHsv.H);
                        hueDiff = Math.Min(hueDiff, 360 - hueDiff);

                        // Highlight detected pixels in red
                        if (hueDiff <= hueTolerance &&
                            Math.Abs(pixelHsv.S - targetHsv.S) < 0.3f &&
                            Math.Abs(pixelHsv.V - targetHsv.V) < 0.3f)
                        {
                            preview.SetPixel(x, y, Color.Red);
                        }
                        else
                        {
                            // Darken non-detected pixels
                            preview.SetPixel(x, y, Color.FromArgb(
                                pixel.R / 3,
                                pixel.G / 3,
                                pixel.B / 3));
                        }
                    }
                }

                previewBox.Image = new Bitmap(preview);
            }
        }


        // The main flexible detection method
        // The main flexible detection method
        // Replace your DetectColoredPointsFlexible method with this:
        private void DetectColoredPointsFlexible(Color? referenceColor)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Please load an image first.", "No Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            detectedPoints.Clear();
            Color targetColor;

            if (referenceColor.HasValue)
            {
                targetColor = referenceColor.Value;
                UpdateStatus($"Detecting points using sampled color: RGB({targetColor.R},{targetColor.G},{targetColor.B})");
            }
            else
            {
                targetColor = GetColorFromEnum(selectedColor);
                UpdateStatus($"Detecting points using preset color: {selectedColor}");
            }

            using (Bitmap bmp = new Bitmap(originalImage))
            {
                int width = bmp.Width;
                int height = bmp.Height;

                // Create a debug bitmap to visualize what's being detected
                Bitmap debugBmp = new Bitmap(width, height);

                // SIMPLE RGB DETECTION - Look for pixels similar to the target color
                bool[,] mask = new bool[height, width];
                int totalSimilarPixels = 0;

                // Tolerance for RGB difference
                int rgbTolerance = 50; // How close the color needs to be

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = bmp.GetPixel(x, y);

                        // Calculate RGB difference
                        int rDiff = Math.Abs(pixel.R - targetColor.R);
                        int gDiff = Math.Abs(pixel.G - targetColor.G);
                        int bDiff = Math.Abs(pixel.B - targetColor.B);

                        // Check if pixel is similar to target color
                        bool isSimilar = rDiff <= rgbTolerance &&
                                         gDiff <= rgbTolerance &&
                                         bDiff <= rgbTolerance;

                        // For debugging, color the debug image
                        if (isSimilar)
                        {
                            debugBmp.SetPixel(x, y, Color.Red); // Mark detected pixels in red
                        }
                        else
                        {
                            // Darken non-detected pixels
                            debugBmp.SetPixel(x, y, Color.FromArgb(
                                pixel.R / 3,
                                pixel.G / 3,
                                pixel.B / 3));
                        }

                        mask[y, x] = isSimilar;
                        if (isSimilar) totalSimilarPixels++;
                    }
                }

                // Save debug image to see what was detected
                string debugPath = Path.Combine(Path.GetTempPath(), "detection_debug.png");
                debugBmp.Save(debugPath);
                debugBmp.Dispose();

                if (totalSimilarPixels == 0)
                {
                    MessageBox.Show($"No pixels found with color similar to RGB({targetColor.R},{targetColor.G},{targetColor.B})\n" +
                                   $"Tolerance used: {rgbTolerance}\n\n" +
                                   $"Debug image saved to:\n{debugPath}\n\n" +
                                   "Try clicking directly on a brighter part of the sticker.",
                                   "Detection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Find connected components - use a VERY low minimum size
                List<ConnectedComponent> components = FindAllComponents(mask, width, height);

                // Build detailed debug info
                StringBuilder debugInfo = new StringBuilder();
                debugInfo.AppendLine($"=== DETECTION DEBUG ===");
                debugInfo.AppendLine($"Total similar pixels: {totalSimilarPixels}");
                debugInfo.AppendLine($"Components found: {components.Count}");
                debugInfo.AppendLine($"Debug image: {debugPath}");
                debugInfo.AppendLine($"\nComponent details:");

                int id = 1;
                List<ConnectedComponent> validComponents = new List<ConnectedComponent>();

                foreach (var component in components)
                {
                    debugInfo.AppendLine($"\nComponent {id}:");
                    debugInfo.AppendLine($"  Pixel count: {component.PixelCount}");
                    debugInfo.AppendLine($"  Bounds: {component.Width} x {component.Height}");
                    debugInfo.AppendLine($"  Area: {component.Width * component.Height} pixels");

                    // MUCH MORE PERMISSIVE FILTERING
                    // Accept almost anything that's not tiny
                    if (component.PixelCount >= 10) // Only filter out extremely tiny groups
                    {
                        // Calculate center
                        Point center = new Point(
                            (component.MinX + component.MaxX) / 2,
                            (component.MinY + component.MaxY) / 2
                        );

                        detectedPoints.Add(new DetectedPoint(
                            center,
                            selectedColor,
                            1.0,
                            (int)Math.Sqrt(component.PixelCount / Math.PI),
                            id
                        ));

                        validComponents.Add(component);
                        debugInfo.AppendLine($"  ✓ ACCEPTED as sticker {id}");
                        id++;
                    }
                    else
                    {
                        debugInfo.AppendLine($"  ✗ REJECTED (too small)");
                    }
                }

                // Show detailed debug info
                debugInfo.AppendLine($"\n=== RESULT ===");
                debugInfo.AppendLine($"Final stickers detected: {detectedPoints.Count}");
                debugInfo.AppendLine($"\nPress OK to continue with detection.");

                MessageBox.Show(debugInfo.ToString(), "Detection Debug",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (detectedPoints.Count > 0)
                {
                    CreateMeasurementsFromDetectedPoints();
                    drawingPanel.Invalidate();

                    MessageBox.Show($"Success! Found {detectedPoints.Count} stickers.\n\n" +
                                   $"Debug image saved to:\n{debugPath}",
                                   "Detection Complete",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Found {components.Count} color regions but none passed the filters.\n\n" +
                                   $"Check the debug image to see what was detected:\n{debugPath}\n\n" +
                                   "The detected pixels are shown in RED in the debug image.",
                                   "No Stickers Detected",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Warning);
                }
            }
        }

        // New method to find ALL components without filtering
        private List<ConnectedComponent> FindAllComponents(bool[,] mask, int width, int height)
        {
            List<ConnectedComponent> components = new List<ConnectedComponent>();
            bool[,] visited = new bool[height, width];
            Queue<Point> queue = new Queue<Point>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x] && !visited[y, x])
                    {
                        ConnectedComponent comp = new ConnectedComponent();
                        queue.Clear();
                        queue.Enqueue(new Point(x, y));

                        while (queue.Count > 0)
                        {
                            Point p = queue.Dequeue();

                            if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height ||
                                visited[p.Y, p.X] || !mask[p.Y, p.X])
                                continue;

                            visited[p.Y, p.X] = true;
                            comp.Add(p.X, p.Y);

                            // Check all 8 neighbors
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    if (dx == 0 && dy == 0) continue;
                                    queue.Enqueue(new Point(p.X + dx, p.Y + dy));
                                }
                            }
                        }

                        components.Add(comp);
                    }
                }
            }

            return components;
        }
        // Fix the RgbToHsv method:
        private HsvColor RgbToHsv(Color rgb)
        {
            float r = rgb.R / 255f;
            float g = rgb.G / 255f;
            float b = rgb.B / 255f;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0;
            float s = (max == 0) ? 0 : delta / max;
            float v = max;

            if (delta != 0)
            {
                if (max == r)
                    h = 60 * (((g - b) / delta) % 6);
                else if (max == g)
                    h = 60 * (((b - r) / delta) + 2);
                else
                    h = 60 * (((r - g) / delta) + 4);
            }

            if (h < 0) h += 360;

            return new HsvColor(h, s, v);
        }

        // Fix the FindStickersFlexible method:
        private List<ConnectedComponent> FindStickersFlexible(bool[,] mask, int width, int height)
        {
            List<ConnectedComponent> components = new List<ConnectedComponent>();
            bool[,] visited = new bool[height, width];
            Queue<Point> queue = new Queue<Point>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[y, x] && !visited[y, x])
                    {
                        ConnectedComponent comp = new ConnectedComponent();
                        queue.Clear();
                        queue.Enqueue(new Point(x, y));

                        while (queue.Count > 0)
                        {
                            Point p = queue.Dequeue();

                            if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height ||
                                visited[p.Y, p.X] || !mask[p.Y, p.X])
                                continue;

                            visited[p.Y, p.X] = true;
                            comp.Add(p.X, p.Y);

                            // Check 4-connected neighbors (faster, better for stickers)
                            queue.Enqueue(new Point(p.X + 1, p.Y));
                            queue.Enqueue(new Point(p.X - 1, p.Y));
                            queue.Enqueue(new Point(p.X, p.Y + 1));
                            queue.Enqueue(new Point(p.X, p.Y - 1));

                            // Also check diagonals for better connectivity
                            queue.Enqueue(new Point(p.X + 1, p.Y + 1));
                            queue.Enqueue(new Point(p.X - 1, p.Y - 1));
                            queue.Enqueue(new Point(p.X + 1, p.Y - 1));
                            queue.Enqueue(new Point(p.X - 1, p.Y + 1));
                        }

                        // Keep all components, we'll filter later
                        components.Add(comp);
                    }
                }
            }

            return components;
        }

        // Fix the SimpleDetectionTest to work immediately:
        private void SimpleDetectionTest()
        {
            if (originalImage == null)
            {
                MessageBox.Show("Load an image first!");
                return;
            }

            detectedPoints.Clear();

            using (Bitmap bmp = new Bitmap(originalImage))
            {
                int id = 1;

                // Scan the entire image
                for (int x = 0; x < bmp.Width; x += 2) // Sample every 2 pixels
                {
                    for (int y = 0; y < bmp.Height; y += 2)
                    {
                        Color pixel = bmp.GetPixel(x, y);

                        // SIMPLE: Look for bright red pixels
                        if (pixel.R > 200 && pixel.G < 100 && pixel.B < 100)
                        {
                            // Check if this is part of a larger region
                            bool isNewPoint = true;
                            foreach (var existing in detectedPoints)
                            {
                                double distance = Math.Sqrt(
                                    Math.Pow(existing.Location.X - x, 2) +
                                    Math.Pow(existing.Location.Y - y, 2));
                                if (distance < 30) // Within 30 pixels of existing point
                                {
                                    isNewPoint = false;
                                    break;
                                }
                            }

                            if (isNewPoint)
                            {
                                detectedPoints.Add(new DetectedPoint(
                                    new Point(x, y),
                                    PointColor.Red,
                                    1.0,
                                    10,
                                    id++
                                ));
                            }
                        }
                    }
                }
            }

            MessageBox.Show($"Simple detection found {detectedPoints.Count} red pixels");

            if (detectedPoints.Count > 0)
            {
                CreateMeasurementsFromDetectedPoints();
                drawingPanel.Invalidate();
            }
        }

        // Calculate confidence based on color similarity
        private double CalculateColorConfidence(Color c1, Color c2)
        {
            // Calculate Euclidean distance in RGB space
            double rDiff = c1.R - c2.R;
            double gDiff = c1.G - c2.G;
            double bDiff = c1.B - c2.B;

            double distance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
            double maxDistance = Math.Sqrt(3 * 255 * 255); // Maximum possible distance

            // Convert to confidence (1.0 = perfect match, 0.0 = completely different)
            return 1.0 - (distance / maxDistance);
        }
        // More efficient connected component finding
       
    

        // HSV color structure
        private struct HsvColor
        {
            public float H; // Hue: 0-360
            public float S; // Saturation: 0-1
            public float V; // Value: 0-1

            public HsvColor(float h, float s, float v)
            {
                H = h;
                S = s;
                V = v;
            }
        }

        // Convert RGB to HSV

        // Helper to get color from enum
        private Color GetColorFromEnum(PointColor color)
        {
            switch (color)
            {
                case PointColor.Red: return Color.Red;
                case PointColor.Green: return Color.Green;
                case PointColor.Blue: return Color.Blue;
                case PointColor.Yellow: return Color.Yellow;
                case PointColor.White: return Color.White;
                case PointColor.Custom: return customColor;
                default: return Color.Red;
            }
        }












        // CRÉER la fonction HandlePointConnection :
        private void HandlePointConnection(Point clickPoint)
        {
            // Rechercher le point détecté le plus proche
            DetectedPoint? nearestDetectedPoint = null;
            double minDistance = double.MaxValue;

            foreach (var point in detectedPoints)
            {
                double distance = CalculateDistance(clickPoint, point.Location);
                if (distance < 20) // Tolérance de 20 pixels
                {
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestDetectedPoint = point;
                    }
                }
            }

            // Si aucun point détecté trouvé, chercher parmi les points de mesure existants
            if (nearestDetectedPoint == null)
            {
                foreach (var measurement in measurements)
                {
                    if (measurement.Type == MeasurementType.Point)
                    {
                        double distance = CalculateDistance(clickPoint, measurement.Start);
                        if (distance < 20) // Tolérance de 20 pixels
                        {
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                nearestDetectedPoint = new DetectedPoint(
                                    measurement.Start,
                                    PointColor.Red, // Couleur par défaut
                                    1.0, // Confiance par défaut
                                    10, // Rayon par défaut
                                    measurement.ID
                                );
                            }
                        }
                    }
                }
            }

            if (nearestDetectedPoint == null)
            {
                UpdateStatus("Aucun point trouvé près du clic. Cliquez sur un point détecté.");
                return;
            }

            // Mettre en surbrillance le point sélectionné
            HighlightSelectedPoint(nearestDetectedPoint.Value);

            if (selectedPointForLine == null)
            {
                // Premier point sélectionné
                selectedPointForLine = nearestDetectedPoint.Value.Location;
                UpdateStatus($"Premier point sélectionné (P{nearestDetectedPoint.Value.ID}). Cliquez sur le second point.");
            }
            else
            {
                // Deuxième point sélectionné - créer la ligne
                CreateLineBetweenPoints(selectedPointForLine.Value, nearestDetectedPoint.Value);
                selectedPointForLine = null;

                // Demander si on veut continuer à connecter des points
                var result = MessageBox.Show($"Ligne créée entre les points !\n\nVoulez-vous créer une autre ligne ?",
                                            "Connexion réussie",
                                            MessageBoxButtons.YesNo,
                                            MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    isCreatingLineBetweenPoints = false;
                    drawingPanel.Cursor = Cursors.Default;
                    UpdateStatus("Mode Connexion terminé.");
                }
                else
                {
                    UpdateStatus("Mode Connexion: Cliquez sur le premier point, puis sur le second");
                }
            }

            drawingPanel.Invalidate();
        }


        private void HighlightSelectedPoint(DetectedPoint point)
        {
            // Mettre en surbrillance temporairement le point
            // Nous allons dessiner un cercle plus grand autour du point
            // Cette information sera utilisée dans DrawDetectedPoints
            // Pour cela, nous allons ajouter une variable temporaire
            highlightedPoint = point.Location;

            // Effacer la surbrillance après 1 seconde
            System.Windows.Forms.Timer highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 1000;
            highlightTimer.Tick += (s, e) => {
                highlightedPoint = null;
                highlightTimer.Stop();
                drawingPanel.Invalidate();
            };
            highlightTimer.Start();
        }

        private IntersectionPoint? FindIntersectionAtPoint(Point point)
        {
            foreach (var ip in intersectionPoints)
            {
                if (CalculateDistance(ip.Location, point) < intersectionTolerance)
                {
                    return ip;
                }
            }
            return null;
        }

        private void ShowAngleContextMenu(Point screenLocation, IntersectionPoint intersection)
        {
            if (intersection.Equals(default(IntersectionPoint))) return;

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.BackColor = Color.FromArgb(62, 62, 64);
            contextMenu.ForeColor = Color.White;
            contextMenu.Renderer = new CustomToolStripRenderer();

            // Titre
            ToolStripMenuItem titleItem = new ToolStripMenuItem(
                $"📐 Point P{intersection.ID} - {intersection.LineIDs.Count} lines");
            titleItem.Enabled = false;
            titleItem.Font = new System.Drawing.Font("Arial", 9, FontStyle.Bold);
            contextMenu.Items.Add(titleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // Grouper les angles par paires de lignes
            var angleGroups = intersection.Angles
                .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                .Select(g => new
                {
                    Line1 = g.Key.Line1,
                    Line2 = g.Key.Line2,
                    Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                })
                .ToList();

            if (angleGroups.Count == 0)
            {
                ToolStripMenuItem noAnglesItem = new ToolStripMenuItem("No angles detected");
                noAnglesItem.Enabled = false;
                contextMenu.Items.Add(noAnglesItem);
            }
            else
            {
                foreach (var group in angleGroups)
                {
                    if (group.Angles.Count == 2)
                    {
                        string angleText = $"∠(L{group.Line1}-L{group.Line2}): {group.Angles[0]:F1}° & {group.Angles[1]:F1}°";
                        ToolStripMenuItem angleItem = new ToolStripMenuItem(angleText);


                        contextMenu.Items.Add(angleItem);
                    }
                    else if (group.Angles.Count == 1)
                    {
                        // Cas particulier (angle droit = 90°)
                        string angleText = $"∠(L{group.Line1}-L{group.Line2}) = {group.Angles[0]:F1}°";
                        if (Math.Abs(group.Angles[0] - 90) < 0.1)
                        {
                            angleText += " (Right angle)";
                        }
                        contextMenu.Items.Add(new ToolStripMenuItem(angleText));
                    }
                }
            }

            contextMenu.Items.Add(new ToolStripSeparator());



            // Boutons d'action
            ToolStripMenuItem copyItem = new ToolStripMenuItem("📋 Copy All Data");
            copyItem.Click += (s, ev) => CopyAnglesToClipboard(intersection);
            contextMenu.Items.Add(copyItem);

            ToolStripMenuItem clearItem = new ToolStripMenuItem("❌ Clear Selection");
            clearItem.Click += (s, ev) => { selectedIntersection = default(IntersectionPoint); drawingPanel.Invalidate(); };
            contextMenu.Items.Add(clearItem);

            // Afficher le menu
            contextMenu.Show(drawingPanel, screenLocation);
        }
        private void CopyAnglesToClipboard(IntersectionPoint intersection)
        {
            if (intersection.Angles.Count == 0)
            {
                Clipboard.SetText("No angles at this intersection");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== INTERSECTION POINT P{intersection.ID} ===");
            sb.AppendLine($"Type: {intersection.Type}");
            sb.AppendLine($"Coordinates: ({intersection.Location.X}, {intersection.Location.Y})");
            sb.AppendLine($"Lines involved: {string.Join(", ", intersection.LineIDs.Select(id => $"L{id}"))}");
            sb.AppendLine();
            sb.AppendLine("ANGLES:");
            sb.AppendLine("-------");

            // Grouper les angles par paires de lignes
            var angleGroups = intersection.Angles
                .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                .Select(g => new
                {
                    Line1 = g.Key.Line1,
                    Line2 = g.Key.Line2,
                    Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                })
                .OrderBy(g => g.Line1).ThenBy(g => g.Line2);

            foreach (var group in angleGroups)
            {
                sb.AppendLine($"Between L{group.Line1} and L{group.Line2}:");

                if (group.Angles.Count == 2)
                {
                    sb.AppendLine($"  • Acute angle: {group.Angles[0]:F2}°");
                    sb.AppendLine($"  • Obtuse angle: {group.Angles[1]:F2}°");
                    sb.AppendLine($"  • Sum: {(group.Angles[0] + group.Angles[1]):F2}°");
                    sb.AppendLine($"  • Acute/Obtuse ratio: {group.Angles[0] / group.Angles[1]:F3}");
                }
                else if (group.Angles.Count == 1)
                {
                    sb.AppendLine($"  • Angle: {group.Angles[0]:F2}°");
                    if (Math.Abs(group.Angles[0] - 90) < 0.1)
                        sb.AppendLine("    → RIGHT ANGLE (90°)");
                }
                sb.AppendLine();
            }

            // Statistiques
            var allAngles = intersection.Angles.Select(a => a.Item3).Distinct().ToList();
            sb.AppendLine("STATISTICS:");
            sb.AppendLine("-----------");
            sb.AppendLine($"Total distinct angles: {allAngles.Count}");
            sb.AppendLine($"Acute angles (<90°): {allAngles.Where(a => a < 90).Count()}");
            sb.AppendLine($"Right angles (≈90°): {allAngles.Where(a => Math.Abs(a - 90) < 0.5).Count()}");
            sb.AppendLine($"Obtuse angles (>90°): {allAngles.Where(a => a > 90).Count()}");

            if (allAngles.Count > 0)
            {
                sb.AppendLine($"Minimum: {allAngles.Min():F2}°");
                sb.AppendLine($"Maximum: {allAngles.Max():F2}°");
                sb.AppendLine($"Average: {allAngles.Average():F2}°");
                sb.AppendLine($"Median: {CalculateMedian(allAngles):F2}°");
            }

            // Détecter les angles spéciaux
            sb.AppendLine();
            sb.AppendLine("SPECIAL ANGLES:");
            sb.AppendLine("---------------");

            foreach (var angle in allAngles.OrderBy(a => a))
            {
                string special = "";
                if (Math.Abs(angle - 30) < 0.5) special = " (Common: 30°)";
                else if (Math.Abs(angle - 45) < 0.5) special = " (Half right: 45°)";
                else if (Math.Abs(angle - 60) < 0.5) special = " (Common: 60°)";
                else if (Math.Abs(angle - 90) < 0.5) special = " (Right angle: 90°)";
                else if (Math.Abs(angle - 120) < 0.5) special = " (Supplementary to 60°)";
                else if (Math.Abs(angle - 135) < 0.5) special = " (Supplementary to 45°)";
                else if (Math.Abs(angle - 150) < 0.5) special = " (Supplementary to 30°)";

                sb.AppendLine($"{angle:F2}°{special}");
            }

            Clipboard.SetText(sb.ToString());
            UpdateStatus($"All angles at point P{intersection.ID} copied to clipboard");
        }

        // Méthode utilitaire pour calculer la médiane
        private double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count == 0) return 0;
            if (count % 2 == 0)
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            else
                return sorted[count / 2];
        }
        private void DrawIntersectionAngles(Graphics g, IntersectionPoint ip)
        {
            if (ip.LineIDs.Count < 2 || ip.Angles.Count == 0) return;

            // Get angle pair for display
            var anglePair = ip.Angles
                .GroupBy(a => new { L1 = Math.Min(a.Item1, a.Item2), L2 = Math.Max(a.Item1, a.Item2) })
                .Select(gg => gg.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList())
                .FirstOrDefault(a => a.Count >= 2);

            if (anglePair == null || anglePair.Count < 2) return;

            double acuteAngle = anglePair[0];
            double obtuseAngle = anglePair[1];

            // Get the two intersecting lines
            var lines = measurements.Where(m => ip.LineIDs.Contains(m.ID)).Take(2).ToList();
            if (lines.Count < 2) return;

            // Calculate the actual line angles from intersection point
            double[] lineAngles = new double[2];
            for (int i = 0; i < 2; i++)
            {
                Point start = lines[i].Type == MeasurementType.Angle && lines[i].Vertex.HasValue ?
                             lines[i].Vertex.Value : lines[i].Start;
                Point end = lines[i].End;

                // Calculate angle from intersection point to line end
                double dx = end.X - ip.Location.X;
                double dy = end.Y - ip.Location.Y;

                // If intersection is closer to start, flip direction
                double distToStart = Math.Sqrt(Math.Pow(start.X - ip.Location.X, 2) +
                                               Math.Pow(start.Y - ip.Location.Y, 2));
                double distToEnd = Math.Sqrt(Math.Pow(end.X - ip.Location.X, 2) +
                                             Math.Pow(end.Y - ip.Location.Y, 2));

                if (distToStart > distToEnd)
                {
                    dx = start.X - ip.Location.X;
                    dy = start.Y - ip.Location.Y;
                }

                lineAngles[i] = Math.Atan2(dy, dx) * (180 / Math.PI);
                if (lineAngles[i] < 0) lineAngles[i] += 360;
            }

            // Normalize angles to find the acute angle region
            double angle1 = lineAngles[0];
            double angle2 = lineAngles[1];

            // Calculate angular difference
            double diff = Math.Abs(angle2 - angle1);
            if (diff > 180) diff = 360 - diff;

            // Determine which is the smaller angle region
            double acuteStartAngle, obtuseStartAngle;

            if (diff < 180)
            {
                // Acute angle is between the two lines
                acuteStartAngle = Math.Min(angle1, angle2);
                if (Math.Abs(angle2 - angle1) > 180)
                {
                    acuteStartAngle = Math.Max(angle1, angle2);
                }
                obtuseStartAngle = acuteStartAngle + acuteAngle;
            }
            else
            {
                // Lines are more than 180° apart
                acuteStartAngle = Math.Max(angle1, angle2);
                obtuseStartAngle = Math.Min(angle1, angle2);
            }

            // Normalize to 0-360
            while (acuteStartAngle < 0) acuteStartAngle += 360;
            while (acuteStartAngle >= 360) acuteStartAngle -= 360;
            while (obtuseStartAngle < 0) obtuseStartAngle += 360;
            while (obtuseStartAngle >= 360) obtuseStartAngle -= 360;

            // Arc radii
            float acuteRadius = 28f;
            float obtuseRadius = 36f;

            using (System.Drawing.Font angleFont = new System.Drawing.Font("Arial", Math.Max(9, 11 / zoomFactor), FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
            {
                // --- ACUTE ANGLE ARC ---
                using (Pen acutePen = new Pen(Color.Cyan, 1.5f))
                {
                    RectangleF acuteRect = new RectangleF(
                        ip.Location.X - acuteRadius,
                        ip.Location.Y - acuteRadius,
                        acuteRadius * 2,
                        acuteRadius * 2);

                    g.DrawArc(acutePen, acuteRect, (float)acuteStartAngle, (float)acuteAngle);

                    // Position text at the middle of the arc
                    double acuteMidAngle = (acuteStartAngle + acuteAngle / 2) * Math.PI / 180;
                    PointF acuteTextPos = new PointF(
                        ip.Location.X + (float)(acuteRadius * 1.4 * Math.Cos(acuteMidAngle)),
                        ip.Location.Y + (float)(acuteRadius * 1.4 * Math.Sin(acuteMidAngle))
                    );

                    string acuteText = $"{acuteAngle:F1}°";
                    SizeF acuteTextSize = g.MeasureString(acuteText, angleFont);

                    RectangleF acuteTextRect = new RectangleF(
                        acuteTextPos.X - acuteTextSize.Width / 2,
                        acuteTextPos.Y - acuteTextSize.Height / 2,
                        acuteTextSize.Width + 6,
                        acuteTextSize.Height + 2);

                    g.FillRectangle(bgBrush, acuteTextRect);
                    g.DrawString(acuteText, angleFont, textBrush,
                        acuteTextRect.X + 3,
                        acuteTextRect.Y + 1);
                }

                // --- OBTUSE ANGLE ARC ---
                using (Pen obtusePen = new Pen(Color.Magenta, 1.5f))
                {
                    RectangleF obtuseRect = new RectangleF(
                        ip.Location.X - obtuseRadius,
                        ip.Location.Y - obtuseRadius,
                        obtuseRadius * 2,
                        obtuseRadius * 2);

                    g.DrawArc(obtusePen, obtuseRect, (float)obtuseStartAngle, (float)obtuseAngle);

                    // Position text at the middle of the arc
                    double obtuseMidAngle = (obtuseStartAngle + obtuseAngle / 2) * Math.PI / 180;
                    PointF obtuseTextPos = new PointF(
                        ip.Location.X + (float)(obtuseRadius * 1.4 * Math.Cos(obtuseMidAngle)),
                        ip.Location.Y + (float)(obtuseRadius * 1.4 * Math.Sin(obtuseMidAngle))
                    );

                    string obtuseText = $"{obtuseAngle:F1}°";
                    SizeF obtuseTextSize = g.MeasureString(obtuseText, angleFont);

                    RectangleF obtuseTextRect = new RectangleF(
                        obtuseTextPos.X - obtuseTextSize.Width / 2,
                        obtuseTextPos.Y - obtuseTextSize.Height / 2,
                        obtuseTextSize.Width + 6,
                        obtuseTextSize.Height + 2);

                    g.FillRectangle(bgBrush, obtuseTextRect);
                    g.DrawString(obtuseText, angleFont, textBrush,
                        obtuseTextRect.X + 3,
                        obtuseTextRect.Y + 1);
                }

                // --- INTERSECTION POINT LABEL ---
                using (System.Drawing.Font pointFont = new System.Drawing.Font("Arial", Math.Max(8, 10 / zoomFactor), FontStyle.Bold))
                {
                    string pointLabel = $"P{ip.ID}";
                    SizeF pointLabelSize = g.MeasureString(pointLabel, pointFont);

                    // Position label offset from the intersection
                    PointF pointLabelPos = new PointF(
                        ip.Location.X - pointLabelSize.Width / 2,
                        ip.Location.Y - Math.Max(acuteRadius, obtuseRadius) - 18
                    );

                    g.DrawString(pointLabel, pointFont, textBrush, pointLabelPos);
                }
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
            string measurementName = ""; // Nom par défaut
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
                        newMeasurement = new Measurement(
                            currentStartPoint.Value,
                            location,
                            measurementName,
                            MeasurementType.Line,
                            newId);

                        // Demander le renommage
                        measurementName = PromptForRename(measurementName);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);

                        FindAllIntersections();

                        currentStartPoint = null;
                        UpdateMeasurementsList();
                        drawingPanel.Invalidate();
                        UpdateStatus($"Line created: {measurementName}");
                    }
                    break;

                case ToolMode.Point:
                    measurementName = $"P{measurementCounter++}";
                    newMeasurement = new Measurement(
                        location,
                        location,
                        measurementName,
                        MeasurementType.Point,
                        newId);

                    // Demander le renommage
                    measurementName = PromptForRename(measurementName);
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

                        // Demander le nom de l'angle une seule fois
                        measurementName = PromptForRename(measurementName);

                        int angleId = newId;

                        Measurement firstSegment = new Measurement(
                            angleVertex.Value,
                            angleFirstPoint.Value,
                            measurementName,
                            MeasurementType.Angle,
                            angleId);
                        firstSegment.Vertex = angleVertex.Value;
                        measurements.Add(firstSegment);

                        Measurement secondSegment = new Measurement(
                            angleVertex.Value,
                            location,
                            measurementName,
                            MeasurementType.Angle,
                            angleId);
                        secondSegment.Vertex = angleVertex.Value;
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
                        newMeasurement = new Measurement(
                            currentStartPoint.Value,
                            location,
                            measurementName,
                            MeasurementType.AngleWithAxis,
                            newId);

                        // Demander le renommage
                        measurementName = PromptForRename(measurementName);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);

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
                        newMeasurement = new Measurement(
                            currentStartPoint.Value,
                            location,
                            measurementName,
                            MeasurementType.Distance,
                            newId);

                        // Demander le renommage
                        measurementName = PromptForRename(measurementName);
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
                        newMeasurement = new Measurement(
                            currentStartPoint.Value,
                            location,
                            measurementName,
                            MeasurementType.Distance, // Temporairement Distance
                            newId);

                        // Demander le renommage
                        measurementName = PromptForRename(measurementName);
                        newMeasurement.Name = measurementName;

                        measurements.Add(newMeasurement);
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
                                              measurements[lineIndex].Type == MeasurementType.Angle))
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
                            measurementName = $"P{measurementCounter++}";

                            // Créer la ligne perpendiculaire
                            CreatePerpendicularLine(selectedLineForPerpendicular.Value, location, newId, measurementName);

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


        private void CreatePerpendicularLine(Measurement baseLine, Point endPoint, int id, string name)
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
                // Demander le renommage
                name = PromptForRename(name);

                Measurement perpendicularLine = new Measurement(
                    perpendicularFoot,
                    C,
                    name,
                    MeasurementType.PerpendicularLine,
                    id
                );

                measurements.Add(perpendicularLine);
                UpdateStatus($"Perpendicular line created: {name}");
            }
            else
            {
                UpdateStatus("Perpendicular line too short - not created");
            }
        }


        private string PromptForRename(string defaultName)
        {
            // Vérifier si l'utilisateur veut activer/désactiver le renommage automatique
            if (!autoRenameEnabled)
            {
                return defaultName;
            }

            using (var renameDialog = new AutoRenameDialog(defaultName))
            {
                if (renameDialog.ShowDialog() == DialogResult.OK)
                {
                    if (renameDialog.DontAskAgain)
                    {
                        // Sauvegarder la préférence
                        autoRenameEnabled = false;

                    }

                    return string.IsNullOrWhiteSpace(renameDialog.NewName) ?
                           defaultName : renameDialog.NewName.Trim();
                }
                else
                {
                    // Si l'utilisateur annule, utiliser le nom par défaut
                    return defaultName;
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

                    FindAllIntersections();

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

            string currentName = m.Name;
            string prompt = "Enter new name for measurement:";

            // Special prompt for intersection angles
            if (m.AngleValue.HasValue)
            {
                string angleInfo = $"Current angle: {m.AngleValue:F1}°";
                if (m.RelatedLineIDs != null && m.RelatedLineIDs.Count >= 2)
                {
                    angleInfo += $" (between L{m.RelatedLineIDs[0]} and L{m.RelatedLineIDs[1]})";
                }
                prompt = $"Enter new name for intersection angle:\n{angleInfo}";
            }

            using (var renameDialog = new CustomRenameDialog(currentName, prompt))
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

        public class CustomRenameDialog : Form
        {
            private TextBox textBox;
            public string NewName { get; private set; }

            public CustomRenameDialog(string currentName, string prompt = "Enter new name for measurement:")
            {
                InitializeComponent(currentName, prompt);
            }

            private void InitializeComponent(string currentName, string prompt)
            {
                this.Text = "Rename Measurement";
                this.Size = new Size(350, 150);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label label = new Label();
                label.Text = prompt;
                label.Location = new Point(20, 20);
                label.Size = new Size(300, 30);
                label.AutoSize = true;

                textBox = new TextBox();
                textBox.Text = currentName;
                textBox.Location = new Point(20, 60);
                textBox.Size = new Size(300, 20);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(80, 90);
                okButton.Size = new Size(75, 25);
                okButton.Click += OkButton_Click;

                Button cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(170, 90);
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
            // D'abord vérifier les intersections
            var intersection = FindIntersectionAtPoint(imagePoint);
            if (intersection.HasValue)
            {
                hoveredIntersection = intersection;
                hoverPoint = intersection.Value.Location;

                // Créer le texte d'info-bulle
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

            // Si pas d'intersection, vérifier les mesures normales
            hoveredIntersection = null;
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
                    if (m.AngleValue.HasValue)
                    {
                        // This is an intersection angle
                        if (m.RelatedLineIDs.Count >= 2)
                        {
                            return $"{m.Name} (ID: {m.ID}): {m.AngleValue:F1}° between L{m.RelatedLineIDs[0]} and L{m.RelatedLineIDs[1]}";
                        }
                        else
                        {
                            return $"{m.Name} (ID: {m.ID}): {m.AngleValue:F1}°";
                        }
                    }
                    else
                    {
                        // Regular angle measurement
                        double angle = CalculateAngle(m);
                        return $"{m.Name} (ID: {m.ID}): {angle:F1}°";
                    }
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


            // AUGMENTATION: Effacer aussi les intersections
            intersectionPoints.Clear();
            intersectionCounter = 1;
            selectedIntersection = null;
            hoveredIntersection = null;

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

            // Sort measurements: regular measurements first, then intersection angles
            var sortedMeasurements = measurements
                .OrderBy(m => m.AngleValue.HasValue) // Change from !m.AngleValue to m.AngleValue
                .ThenBy(m => m.ID)
                .ToList();

            foreach (var m in sortedMeasurements)
            {
                string typeText = GetMeasurementTypeString(m.Type);

                // Special display for intersection angles
                if (m.Type == MeasurementType.Angle && m.AngleValue.HasValue)
                {
                    typeText = "Intersection Angle";
                }

                string valueText = GetMeasurementValueText(m);

                ListViewItem item = new ListViewItem(m.ID.ToString());
                item.SubItems.Add(typeText);
                item.SubItems.Add(m.Name);
                item.SubItems.Add(valueText);

                if (m.IsSelected)
                {
                    item.BackColor = Color.FromArgb(75, 110, 175);
                    item.ForeColor = Color.White;
                }
                else
                {
                    //// Color code intersection angles differently
                    //if (m.Type == MeasurementType.Angle && m.AngleValue.HasValue)
                    //{
                    //    item.BackColor = Color.FromArgb(255, 240, 245); // Light pink
                    //}
                    //else
                    //{
                    item.BackColor = measurementsList.BackColor;
                    //}
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
                    if (m.AngleValue.HasValue)
                    {
                        // Intersection angle
                        if (m.RelatedLineIDs.Count >= 2)
                        {
                            return $"{m.AngleValue:F1}° (L{m.RelatedLineIDs[0]}-L{m.RelatedLineIDs[1]})";
                        }
                        else
                        {
                            return $"{m.AngleValue:F1}°";
                        }
                    }
                    else
                    {
                        // Regular angle
                        double angle = CalculateAngle(m);
                        return $"{angle:F1}°";
                    }

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

            FindAllIntersections();

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

            // ===== Title =====
            iTextSharp.text.Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
            Paragraph title = new Paragraph("Body Measurement Analysis Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // ===== Date =====
            iTextSharp.text.Font dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);
            Paragraph date = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", dateFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(date);

            // ===== Image with Measurements =====
            if (originalImage != null)
            {
                try
                {
                    using (Bitmap bmp = new Bitmap(originalImage.Width, originalImage.Height))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                        foreach (var m in measurements)
                            DrawMeasurementOnBitmap(g, m);

                        string tempImagePath = Path.GetTempFileName() + ".png";
                        bmp.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);

                        iTextSharp.text.Image pdfImage = iTextSharp.text.Image.GetInstance(tempImagePath);
                        pdfImage.Alignment = Element.ALIGN_CENTER;

                        float maxWidth = document.PageSize.Width - 72;
                        float maxHeight = document.PageSize.Height - 200;
                        pdfImage.ScaleToFit(maxWidth, maxHeight);

                        if (writer.GetVerticalPosition(false) - pdfImage.ScaledHeight < document.BottomMargin)
                            document.NewPage();

                        pdfImage.SpacingAfter = 20;
                        document.Add(pdfImage);

                        File.Delete(tempImagePath);
                    }
                }
                catch (Exception ex)
                {
                    document.Add(new Paragraph($"Error adding image: {ex.Message}"));
                }
            }

            // ===== Measurements Table =====
            if (measurements.Any())
            {
                float estimatedHeight = measurements.Count * 20 + 50;
                if (writer.GetVerticalPosition(false) - estimatedHeight < document.BottomMargin + 100)
                    document.NewPage();

                Paragraph measurementsHeader = new Paragraph(
                    "Measurement Summary",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.DARK_GRAY))
                {
                    SpacingBefore = 10,
                    SpacingAfter = 10
                };
                document.Add(measurementsHeader);

                PdfPTable table = new PdfPTable(5)
                {
                    WidthPercentage = 100
                };
                table.SetWidths(new float[] { 1, 2, 3, 2, 3 });

                iTextSharp.text.Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                AddTableHeaderCell(table, "ID", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Type", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Name", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Pixel Value", headerFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(table, "Real Value", headerFont, BaseColor.DARK_GRAY);

                var groupedMeasurements = measurements
                    .GroupBy(m => m.ID)
                    .Select(g => g.First())
                    .OrderBy(m => m.ID);

                iTextSharp.text.Font cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                foreach (var m in groupedMeasurements)
                    AddMeasurementToTable(table, m, cellFont);

                document.Add(table);
            }
            else
            {
                document.Add(new Paragraph(
                    "No measurements recorded.",
                    FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, BaseColor.GRAY)));
            }

            // ===== Intersection Points Analysis =====
            if (intersectionPoints != null && intersectionPoints.Count > 0)
            {
                if (writer.GetVerticalPosition(false) < document.BottomMargin + 100)
                    document.NewPage();

                Paragraph intersectionHeader = new Paragraph(
                    "Intersection Points Analysis",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.DARK_GRAY))
                {
                    SpacingBefore = 20,
                    SpacingAfter = 10
                };
                document.Add(intersectionHeader);

                PdfPTable intersectionTable = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };
                intersectionTable.SetWidths(new float[] { 1, 2, 3, 4 });

                iTextSharp.text.Font intHeaderFont =
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);

                AddTableHeaderCell(intersectionTable, "ID", intHeaderFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(intersectionTable, "Type", intHeaderFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(intersectionTable, "Coordinates", intHeaderFont, BaseColor.DARK_GRAY);
                AddTableHeaderCell(intersectionTable, "Lines & Angles", intHeaderFont, BaseColor.DARK_GRAY);

                iTextSharp.text.Font intCellFont =
                    FontFactory.GetFont(FontFactory.HELVETICA, 9);

                foreach (var ip in intersectionPoints.OrderBy(p => p.ID))
                    AddIntersectionToTable(intersectionTable, ip, intCellFont);

                document.Add(intersectionTable);

                if (writer.GetVerticalPosition(false) < document.BottomMargin + 200)
                    document.NewPage();

                Paragraph detailHeader = new Paragraph(
                    "Detailed Angle Analysis",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.DARK_GRAY))
                {
                    SpacingBefore = 15,
                    SpacingAfter = 10
                };
                document.Add(detailHeader);

                Paragraph detailContent = new Paragraph(
                    GetIntersectionDataForPdf(),
                    FontFactory.GetFont(FontFactory.HELVETICA, 10))
                {
                    SpacingAfter = 15
                };
                document.Add(detailContent);

                //  AddIntersectionStatistics(document);
            }

            // ===== Reference Scale =====
            if (isReferenceSet)
            {
                if (writer.GetVerticalPosition(false) < document.BottomMargin + 50)
                    document.NewPage();

                document.Add(new Paragraph(
                    $"Reference Scale: 1 cm = {pixelToRealRatio:F2} pixels",
                    FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.GRAY)));
            }

            // ===== Footer =====
            Paragraph footer = new Paragraph(
                "Generated by Body Measurement Analysis Tool",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.LIGHT_GRAY))
            {
                Alignment = Element.ALIGN_RIGHT,
                SpacingBefore = 20
            };
            document.Add(footer);

            document.Close();
        }


        private void AddIntersectionToTable(PdfPTable table, IntersectionPoint ip, iTextSharp.text.Font font)
        {
            // ID column
            table.AddCell(new PdfPCell(new Phrase($"P{ip.ID}", font))
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            // Type column
            table.AddCell(new PdfPCell(new Phrase(ip.Type.ToString(), font))
            {
                Padding = 5
            });

            // Coordinates column
            table.AddCell(new PdfPCell(new Phrase($"({ip.Location.X}, {ip.Location.Y})", font))
            {
                Padding = 5
            });

            // Lines & Angles column
            string linesText = $"Lines: {string.Join(", ", ip.LineIDs.Select(id => $"L{id}"))}";

            StringBuilder anglesText = new StringBuilder();
            if (ip.Angles.Count > 0)
            {
                // Group angles
                var angleGroups = ip.Angles
                    .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                    .Select(g => new
                    {
                        Line1 = g.Key.Line1,
                        Line2 = g.Key.Line2,
                        Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                    });

                foreach (var group in angleGroups)
                {
                    if (group.Angles.Count == 2)
                    {
                        anglesText.AppendLine($"L{group.Line1}-L{group.Line2}: {group.Angles[0]:F1}° & {group.Angles[1]:F1}°");
                    }
                    else if (group.Angles.Count == 1)
                    {
                        anglesText.AppendLine($"L{group.Line1}-L{group.Line2}: {group.Angles[0]:F1}°");
                    }
                }
            }

            Phrase cellContent = new Phrase();
            cellContent.Add(new Chunk(linesText + "\n", font));
            if (anglesText.Length > 0)
            {
                cellContent.Add(new Chunk(anglesText.ToString(), font));
            }

            table.AddCell(new PdfPCell(cellContent)
            {
                Padding = 5,
                PaddingTop = 8,
                PaddingBottom = 8
            });
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