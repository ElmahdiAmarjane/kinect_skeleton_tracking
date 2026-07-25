using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class DetectionSettingsDialog : Form
    {
        private TrackBar toleranceTrackBar;
        private NumericUpDown minSizeNumeric;
        private NumericUpDown maxSizeNumeric;
        private Label toleranceValueLabel;
        private RadioButton rbAutoDetect;
        private RadioButton rbSampleColor;
        private RadioButton rbManualAdd;
        private RadioButton rbPresetColor;
        public int Tolerance { get; private set; }
        public int MinSize { get; private set; }
        public int MaxSize { get; private set; }
        public string DetectionMethod { get; private set; } // "sample", "preset", "manual"

        public DetectionSettingsDialog(int defaultTolerance, int defaultMinSize, int defaultMaxSize)
        {
            Tolerance = defaultTolerance;
            MinSize = defaultMinSize;
            MaxSize = defaultMaxSize;
            DetectionMethod = "sample";

            InitializeComponent();
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
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            int yPos = 15;

            // Detection method
            Label methodLabel = new Label
            {
                Text = "Detection Method:",
                Location = new Point(20, yPos),
                Size = new Size(350, 22),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            yPos += 28;

            rbSampleColor = new RadioButton
            {
                Text = "🖱️ Sample a point color (Recommended)",
                Location = new Point(30, yPos),
                Size = new Size(340, 22),
                ForeColor = Color.LightGreen,
                BackColor = Color.Transparent,
                Checked = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            yPos += 26;

            rbPresetColor = new RadioButton
            {
                Text = "🎨 Use preset color (Red/Green/Blue)",
                Location = new Point(30, yPos),
                Size = new Size(340, 22),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            yPos += 26;

            rbManualAdd = new RadioButton
            {
                Text = "✏️ Add points manually one by one",
                Location = new Point(30, yPos),
                Size = new Size(340, 22),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Regular)
            };
            yPos += 35;

            // Separator
            Label sep = new Label
            {
                Text = "Detection Parameters",
                Location = new Point(20, yPos),
                Size = new Size(350, 22),
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            yPos += 28;

            // Tolerance
            Label toleranceLabel = new Label
            {
                Text = "Color Tolerance:",
                Location = new Point(20, yPos),
                Size = new Size(110, 20),
                ForeColor = Color.White
            };

            toleranceValueLabel = new Label
            {
                Location = new Point(340, yPos),
                Size = new Size(40, 20),
                ForeColor = Color.Yellow,
                TextAlign = ContentAlignment.MiddleRight
            };

            toleranceTrackBar = new TrackBar
            {
                Location = new Point(130, yPos - 2),
                Size = new Size(200, 45),
                Minimum = 5,
                Maximum = 80,
                TickFrequency = 5,
                Value = 30,
                BackColor = Color.FromArgb(45, 45, 48)
            };
            toleranceTrackBar.ValueChanged += (s, e) => toleranceValueLabel.Text = toleranceTrackBar.Value.ToString();
            yPos += 45;

            // Min size
            Label minSizeLabel = new Label
            {
                Text = "Min Point Size (px):",
                Location = new Point(20, yPos),
                Size = new Size(130, 25),
                ForeColor = Color.White
            };

            minSizeNumeric = new NumericUpDown
            {
                Location = new Point(155, yPos),
                Size = new Size(70, 25),
                Minimum = 2,
                Maximum = 100,
                Value = 5,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            yPos += 30;

            // Max size
            Label maxSizeLabel = new Label
            {
                Text = "Max Point Size (px):",
                Location = new Point(20, yPos),
                Size = new Size(130, 25),
                ForeColor = Color.White
            };

            maxSizeNumeric = new NumericUpDown
            {
                Location = new Point(155, yPos),
                Size = new Size(70, 25),
                Minimum = 5,
                Maximum = 200,
                Value = 30,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            yPos += 40;

            // Buttons
            Button detectButton = new Button
            {
                Text = "Start Detection",
                DialogResult = DialogResult.OK,
                Location = new Point(100, yPos),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            detectButton.FlatAppearance.BorderSize = 0;

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(230, yPos),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                methodLabel, rbSampleColor, rbPresetColor, rbManualAdd, sep,
                toleranceLabel, toleranceValueLabel, toleranceTrackBar,
                minSizeLabel, minSizeNumeric,
                maxSizeLabel, maxSizeNumeric,
                detectButton, cancelButton
            });

            this.AcceptButton = detectButton;
            this.CancelButton = cancelButton;

            // Update detection method on radio change
            rbSampleColor.CheckedChanged += (s, e) => { if (rbSampleColor.Checked) DetectionMethod = "sample"; };
            rbPresetColor.CheckedChanged += (s, e) => { if (rbPresetColor.Checked) DetectionMethod = "preset"; };
            rbManualAdd.CheckedChanged += (s, e) => { if (rbManualAdd.Checked) DetectionMethod = "manual"; };
        }

        private void LoadSettings()
        {
            toleranceTrackBar.Value = Tolerance;
            toleranceValueLabel.Text = Tolerance.ToString();
            minSizeNumeric.Value = MinSize;
            maxSizeNumeric.Value = MaxSize;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.OK)
            {
                Tolerance = toleranceTrackBar.Value;
                MinSize = (int)minSizeNumeric.Value;
                MaxSize = (int)maxSizeNumeric.Value;
            }
            base.OnFormClosing(e);
        }
    }
}