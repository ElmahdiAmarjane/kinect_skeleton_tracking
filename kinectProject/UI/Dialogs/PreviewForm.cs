using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Dialog to preview an image before saving
    /// </summary>
    public class PreviewForm : Form
    {
        private PictureBox pictureBox;
        private Button btnSave;
        private Button btnCancel;

        public Image PreviewImage
        {
            get => pictureBox?.Image;
            set
            {
                if (pictureBox != null)
                    pictureBox.Image = value;
            }
        }

        public PreviewForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Image Preview";
            this.Size = new Size(800, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            // PictureBox
            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None
            };

            // Bottom panel for buttons
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(100, 32),
                Location = new Point(12, 9),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnSave = new Button
            {
                Text = "💾 Save Image",
                Size = new Size(120, 32),
                Location = new Point(660, 9),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            bottomPanel.Controls.Add(btnCancel);
            bottomPanel.Controls.Add(btnSave);

            // Add controls
            this.Controls.Add(pictureBox);
            this.Controls.Add(bottomPanel);

            // Handle resize for button positioning
            this.Resize += (s, e) =>
            {
                btnSave.Location = new Point(this.ClientSize.Width - 132, 9);
            };
        }
    }
}