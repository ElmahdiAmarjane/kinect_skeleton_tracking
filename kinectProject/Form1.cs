using System;
using System.Drawing;
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

        // More precise depth range for human body
        private const ushort BODY_DETECTION_MIN_DEPTH = 500;  // 0.5m
        private const ushort BODY_DETECTION_MAX_DEPTH = 2000; // 2m
        private const int DEPTH_WINDOW = 200; // Adjustable depth window in millimeters

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

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body);
                multiSourceFrameReader.MultiSourceFrameArrived += MultiSourceFrameReader_MultiSourceFrameArrived;

                depthBitmap = new Bitmap(512, 424, PixelFormat.Format32bppRgb);
                depthPixels = new byte[512 * 424 * 4];

                // *** STEP 1: Create PictureBox First ***
                PictureBox depthPictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,  // It will be resized automatically
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BackColor = Color.Black // To make sure we see it
                };
                this.Controls.Add(depthPictureBox); // Add first

                // *** STEP 2: Create a Panel for UI Elements ***
                Panel topPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 80, // Fixed height for UI
                    BackColor = Color.LightGray
                };
                this.Controls.Add(topPanel); // Add second, so it stays on top

                // *** STEP 3: Create Dropdowns ***
                jointSelector1.Items.AddRange(Enum.GetNames(typeof(JointType)));
                jointSelector2.Items.AddRange(Enum.GetNames(typeof(JointType)));

                jointSelector1.SelectedIndex = 0;
                jointSelector2.SelectedIndex = 1;

                jointSelector1.Location = new Point(10, 10);
                jointSelector2.Location = new Point(150, 10);

                depthDiffLabel.Location = new Point(10, 50);
                depthDiffLabel.Text = "Depth Difference: - mm";

                // Add UI elements to the panel
                topPanel.Controls.Add(jointSelector1);
                topPanel.Controls.Add(jointSelector2);
                topPanel.Controls.Add(depthDiffLabel);

                MessageBox.Show("Please stand 1-2 meters from the sensor for optimal body mapping.");
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (multiSourceFrameReader != null) multiSourceFrameReader.Dispose();
            if (kinectSensor != null) kinectSensor.Close();
            base.OnFormClosing(e);
        }
    }
}

