using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Kinect;

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

        // Depth range for human body
        private const ushort BODY_DETECTION_MIN_DEPTH = 400;  // 0.5m
        private const ushort BODY_DETECTION_MAX_DEPTH = 2000; // 2m
        private const int DEPTH_WINDOW = 200; // Adjustable depth window in millimeters

        // Variables for point selection
        private DepthFrameReader depthReader;
        private CoordinateMapper coordinateMapper;
        private Point clickPoint1 = Point.Empty;
        private Point clickPoint2 = Point.Empty;
        private CameraSpacePoint? selectedPoint1 = null;
        private CameraSpacePoint? selectedPoint2 = null;
        private PictureBox depthPictureBox;

        // Zoom functionality
        private float zoomFactor = 1.0f;
        private Point zoomCenter = new Point(256, 212); // Center of 512x424 image
        private const float ZOOM_INCREMENT = 0.25f;
        private const float MAX_ZOOM = 4.0f;
        private const float MIN_ZOOM = 0.5f;
        private bool isPanning = false;
        private Point panStart;
        private Label zoomLabel;

        // UI Controls
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

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body);
                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameReader_MultiSourceFrameArrived;

                depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
                depthPixels = new byte[512 * 424 * 4];

                // Create PictureBox
                depthPictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Black
                };
                this.Controls.Add(depthPictureBox);
                depthPictureBox.MouseClick += DepthPictureBox_MouseClick;
                depthPictureBox.MouseDown += DepthPictureBox_MouseDown;
                depthPictureBox.MouseMove += DepthPictureBox_MouseMove;
                depthPictureBox.MouseUp += DepthPictureBox_MouseUp;
                depthPictureBox.MouseWheel += DepthPictureBox_MouseWheel;

                // Initialize zoom controls
                InitializeZoomControls();

                // Initialize joint selectors
                jointSelector1.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector2.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector1.SelectedIndex = 0;
                jointSelector2.SelectedIndex = 1;

                jointSelector1.Location = new Point(10, 50);
                jointSelector2.Location = new Point(150, 50);
                depthDiffLabel.Location = new Point(10, 80);
                depthDiffLabel.AutoSize = true;
                depthDiffLabel.Text = "Depth Difference: - mm";

                this.Controls.Add(jointSelector1);
                this.Controls.Add(jointSelector2);
                this.Controls.Add(depthDiffLabel);

                MessageBox.Show("Please stand 1-2 meters from the sensor for optimal body mapping.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void InitializeZoomControls()
        {
            // Zoom In Button
            Button zoomInButton = new Button
            {
                Text = "+",
                Location = new Point(10, 10),
                Size = new Size(30, 30)
            };
            zoomInButton.Click += ZoomInButton_Click;
            this.Controls.Add(zoomInButton);

            // Zoom Out Button
            Button zoomOutButton = new Button
            {
                Text = "-",
                Location = new Point(50, 10),
                Size = new Size(30, 30)
            };
            zoomOutButton.Click += ZoomOutButton_Click;
            this.Controls.Add(zoomOutButton);

            // Reset Zoom Button
            Button resetZoomButton = new Button
            {
                Text = "Reset",
                Location = new Point(90, 10),
                Size = new Size(60, 30)
            };
            resetZoomButton.Click += ResetZoomButton_Click;
            this.Controls.Add(resetZoomButton);

            // Zoom level label
            zoomLabel = new Label
            {
                Text = $"Zoom: {zoomFactor}x",
                Location = new Point(160, 15),
                AutoSize = true
            };
            this.Controls.Add(zoomLabel);

            // Bring controls to front
            zoomInButton.BringToFront();
            zoomOutButton.BringToFront();
            resetZoomButton.BringToFront();
            zoomLabel.BringToFront();
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

                // Get selected joints from ComboBox
                string jointName1 = jointSelector1.SelectedItem?.ToString() ?? "Head";
                string jointName2 = jointSelector2.SelectedItem?.ToString() ?? "Neck";

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
                this.Invoke((MethodInvoker)delegate {
                    depthDiffLabel.Text = $"{jointName1}-{jointName2} Depth Diff: {depthDifference} mm";
                });

                // Get spine base position for reference depth
                CameraSpacePoint spineBase = trackedBody.Joints[JointType.SpineMid].Position;
                ushort referenceDepth = (ushort)(spineBase.Z * 1000); // Convert to millimeters

                // Calculate adaptive depth window
                ushort minDepth = (ushort)Math.Max(referenceDepth - DEPTH_WINDOW, BODY_DETECTION_MIN_DEPTH);
                ushort maxDepth = (ushort)Math.Min(referenceDepth + DEPTH_WINDOW, BODY_DETECTION_MAX_DEPTH);

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
                UpdateZoomedImage();
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
        }

        private void UpdateZoomedImage()
        {
            if (depthBitmap == null) return;

            // Calculate the source rectangle based on zoom
            int zoomWidth = (int)(512 / zoomFactor);
            int zoomHeight = (int)(424 / zoomFactor);

            Rectangle srcRect = new Rectangle(
                Math.Max(0, Math.Min(zoomCenter.X - zoomWidth / 2, 512 - zoomWidth)),
                Math.Max(0, Math.Min(zoomCenter.Y - zoomHeight / 2, 424 - zoomHeight)),
                zoomWidth,
                zoomHeight);

            // Create a temporary bitmap for the zoomed view
            Bitmap zoomedBitmap = new Bitmap(depthPictureBox.Width, depthPictureBox.Height);

            using (Graphics g = Graphics.FromImage(zoomedBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(depthBitmap,
                            new Rectangle(0, 0, depthPictureBox.Width, depthPictureBox.Height),
                            srcRect,
                            GraphicsUnit.Pixel);
            }

            depthPictureBox.Image = zoomedBitmap;
            zoomLabel.Text = $"Zoom: {zoomFactor:F1}x";
        }

        private void DepthPictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (depthBitmap == null || coordinateMapper == null || depthReader == null)
            {
                MessageBox.Show("Initialization error: Missing depthBitmap or coordinateMapper.");
                return;
            }

            // Calculate the actual coordinates in the depth image accounting for zoom
            int x, y;

            if (zoomFactor == 1.0f)
            {
                x = e.X * 512 / depthPictureBox.Width;
                y = e.Y * 424 / depthPictureBox.Height;
            }
            else
            {
                // Calculate the source rectangle (same as in UpdateZoomedImage)
                int zoomWidth = (int)(512 / zoomFactor);
                int zoomHeight = (int)(424 / zoomFactor);

                Rectangle srcRect = new Rectangle(
                    Math.Max(0, Math.Min(zoomCenter.X - zoomWidth / 2, 512 - zoomWidth)),
                    Math.Max(0, Math.Min(zoomCenter.Y - zoomHeight / 2, 424 - zoomHeight)),
                    zoomWidth,
                    zoomHeight);

                // Map click position to source image
                x = srcRect.X + (int)(e.X * srcRect.Width / (float)depthPictureBox.Width);
                y = srcRect.Y + (int)(e.Y * srcRect.Height / (float)depthPictureBox.Height);
            }

            // Update zoom center for panning
            zoomCenter = new Point(x, y);

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
                    MessageBox.Show($"First point selected at ({x},{y})");
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

            // Update the zoomed image to center on the clicked point
            UpdateZoomedImage();
        }

        private void DepthPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && zoomFactor > 1.0f)
            {
                isPanning = true;
                panStart = e.Location;
            }
        }

        private void DepthPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                // Calculate movement in source image coordinates
                int dx = (int)((panStart.X - e.X) * (512 / zoomFactor) / depthPictureBox.Width);
                int dy = (int)((panStart.Y - e.Y) * (424 / zoomFactor) / depthPictureBox.Height);

                zoomCenter.X = Math.Max(0, Math.Min(512, zoomCenter.X + dx));
                zoomCenter.Y = Math.Max(0, Math.Min(424, zoomCenter.Y + dy));

                panStart = e.Location;
                UpdateZoomedImage();
            }
        }

        private void DepthPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            isPanning = false;
        }

        private void DepthPictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                zoomFactor = Math.Min(zoomFactor + ZOOM_INCREMENT, MAX_ZOOM);
            }
            else
            {
                zoomFactor = Math.Max(zoomFactor - ZOOM_INCREMENT, MIN_ZOOM);
            }
            UpdateZoomedImage();
        }

        private void ZoomInButton_Click(object sender, EventArgs e)
        {
            zoomFactor = Math.Min(zoomFactor + ZOOM_INCREMENT, MAX_ZOOM);
            UpdateZoomedImage();
        }

        private void ZoomOutButton_Click(object sender, EventArgs e)
        {
            zoomFactor = Math.Max(zoomFactor - ZOOM_INCREMENT, MIN_ZOOM);
            UpdateZoomedImage();
        }

        private void ResetZoomButton_Click(object sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            zoomCenter = new Point(256, 212);
            UpdateZoomedImage();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (multiSourceFrameReader != null) multiSourceFrameReader.Dispose();
            if (kinectSensor != null) kinectSensor.Close();
            base.OnFormClosing(e);
        }
    }
}