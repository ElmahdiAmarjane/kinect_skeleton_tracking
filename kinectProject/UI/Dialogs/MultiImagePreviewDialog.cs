using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Dialog to preview and save multiple images (Depth, Color, Normal)
    /// </summary>
    public partial class MultiImagePreviewDialog : Form
    {
        private PictureBox[] pictureBoxes;
        private Label[] labels;
        private Image[] images;
        private Button btnSaveAll;
        private Button btnCancel;

        public MultiImagePreviewDialog()
        {
            InitializeComponent();
        }

        public void SetImages(Image depthImage, Image colorImage, Image normalImage)
        {
            images = new Image[] { depthImage, colorImage, normalImage };

            for (int i = 0; i < 3; i++)
            {
                if (images[i] != null && pictureBoxes[i] != null)
                {
                    pictureBoxes[i].Image = images[i];
                }
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Preview & Save All Images";
            this.Size = new Size(1100, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            string[] titles = { "Depth Image", "Color Aligned Image", "Normal Color Image" };
            pictureBoxes = new PictureBox[3];
            labels = new Label[3];

            for (int i = 0; i < 3; i++)
            {
                // Label
                labels[i] = new Label
                {
                    Text = titles[i],
                    Location = new Point(15 + i * 350, 10),
                    Size = new Size(330, 25),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                this.Controls.Add(labels[i]);

                // PictureBox
                pictureBoxes[i] = new PictureBox
                {
                    Location = new Point(15 + i * 350, 40),
                    Size = new Size(330, 350),
                    BackColor = Color.Black,
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom
                };
                this.Controls.Add(pictureBoxes[i]);
            }

            // Buttons panel
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            btnSaveAll = new Button
            {
                Text = "💾 Save All Images",
                Size = new Size(150, 35),
                Location = new Point(400, 10),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnSaveAll.FlatAppearance.BorderSize = 0;
            btnSaveAll.Click += BtnSaveAll_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(100, 35),
                Location = new Point(560, 10),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            buttonPanel.Controls.Add(btnSaveAll);
            buttonPanel.Controls.Add(btnCancel);
            this.Controls.Add(buttonPanel);
        }

        private void BtnSaveAll_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select folder to save all images";
                folderDialog.ShowNewFolderButton = true;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = folderDialog.SelectedPath;
                    string sessionFolder = Path.Combine(folderPath,
                        $"Kinect_Session_{DateTime.Now:yyyyMMdd_HHmmss}");
                    Directory.CreateDirectory(sessionFolder);

                    string[] fileNames = { "DepthImage", "ColorAligned", "NormalColor" };
                    int savedCount = 0;

                    for (int i = 0; i < 3; i++)
                    {
                        if (images[i] != null)
                        {
                            string filePath = Path.Combine(sessionFolder,
                                $"{fileNames[i]}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                            images[i].Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            savedCount++;
                        }
                    }

                    MessageBox.Show($"{savedCount} images saved to:\n{sessionFolder}",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Don't dispose images - they're owned by the caller
                for (int i = 0; i < pictureBoxes?.Length; i++)
                {
                    pictureBoxes[i]?.Image?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}