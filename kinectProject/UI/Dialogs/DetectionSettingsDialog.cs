using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class DetectionSettingsDialog : Form
    {
        private ComboBox colorComboBox;
        private Button colorPickerButton;
        private TrackBar toleranceTrackBar;
        private NumericUpDown minSizeNumeric;
        private NumericUpDown maxSizeNumeric;
        private ColorDialog colorDialog;
        private Label toleranceValueLabel;

        public PointColor SelectedColor { get; private set; }
        public Color CustomColor { get; private set; }
        public int Tolerance { get; private set; }
        public int MinSize { get; private set; }
        public int MaxSize { get; private set; }

        public DetectionSettingsDialog(PointColor defaultColor, Color customColor,
                                      int defaultTolerance, int defaultMinSize, int defaultMaxSize)
        {
            SelectedColor = defaultColor;
            CustomColor = customColor;
            Tolerance = defaultTolerance;
            MinSize = defaultMinSize;
            MaxSize = defaultMaxSize;

            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "Point Detection Settings";
            this.Size = new Size(400, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            // Color selection
            Label colorLabel = new Label
            {
                Text = "Sticker Color:",
                Location = new Point(20, 20),
                Size = new Size(100, 20),
                ForeColor = Color.White
            };

            colorComboBox = new ComboBox
            {
                Location = new Point(130, 20),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            colorComboBox.Items.AddRange(new string[] { "Red", "Green", "Blue", "Yellow", "White", "Custom" });
            colorComboBox.SelectedIndexChanged += ColorComboBox_SelectedIndexChanged;

            colorPickerButton = new Button
            {
                Text = "Pick Color",
                Location = new Point(290, 20),
                Size = new Size(80, 25),
                Enabled = false,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            colorPickerButton.Click += ColorPickerButton_Click;

            // Color tolerance
            Label toleranceLabel = new Label
            {
                Text = "Color Tolerance:",
                Location = new Point(20, 60),
                Size = new Size(100, 20),
                ForeColor = Color.White
            };

            toleranceValueLabel = new Label
            {
                Location = new Point(330, 60),
                Size = new Size(40, 20),
                ForeColor = Color.Yellow
            };

            toleranceTrackBar = new TrackBar
            {
                Location = new Point(130, 60),
                Size = new Size(200, 45),
                Minimum = 10,
                Maximum = 100,
                TickFrequency = 10,
                Value = 30,
                BackColor = Color.FromArgb(45, 45, 48)
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
                Size = new Size(100, 20),
                ForeColor = Color.White
            };

            minSizeNumeric = new NumericUpDown
            {
                Location = new Point(130, 110),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 50,
                Value = 5,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Maximum point size
            Label maxSizeLabel = new Label
            {
                Text = "Max Point Size:",
                Location = new Point(20, 150),
                Size = new Size(100, 20),
                ForeColor = Color.White
            };

            maxSizeNumeric = new NumericUpDown
            {
                Location = new Point(130, 150),
                Size = new Size(100, 25),
                Minimum = 5,
                Maximum = 100,
                Value = 30,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Buttons
            Button detectButton = new Button
            {
                Text = "Detect Points",
                DialogResult = DialogResult.OK,
                Location = new Point(100, 200),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(220, 200),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            // Tips
            Label tipsLabel = new Label
            {
                Text = "Tips: Use bright, solid-colored stickers.\nEnsure good lighting and contrast.\nAvoid colors similar to background.",
                Location = new Point(20, 240),
                Size = new Size(350, 40),
                ForeColor = Color.LightGray,
                Font = new Font("Arial", 8, FontStyle.Italic)
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
            toleranceValueLabel.Text = Tolerance.ToString();
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
}