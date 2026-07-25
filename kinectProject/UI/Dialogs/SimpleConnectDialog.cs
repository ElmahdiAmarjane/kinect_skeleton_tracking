using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace kinectProject
{
    public class SimpleConnectDialog : Form
    {
        private List<Measurement> points;

        public bool ConnectAll { get; private set; }
        public bool ConnectSelected { get; private set; }
        public Measurement? SelectedPoint1 { get; private set; }
        public Measurement? SelectedPoint2 { get; private set; }
        public List<Measurement> SelectedPoints { get; private set; }
        public Color LineColor { get; private set; }

        private CheckedListBox chkPoints;
        private ComboBox cmbPoint1;
        private ComboBox cmbPoint2;
        private Button btnConnectTwo;
        private Button btnConnectSelected;
        private Button btnConnectAll;
        private Button btnCancel;
        private Panel colorPreview;
        private ComboBox cmbColor;

        public SimpleConnectDialog(List<Measurement> pointMeasurements)
        {
            points = pointMeasurements;
            ConnectAll = false;
            ConnectSelected = false;
            SelectedPoints = new List<Measurement>();
            LineColor = Color.LimeGreen;
            InitializeComponent();
            PopulateLists();
        }

        private void InitializeComponent()
        {
            this.Text = "Connect Points";
            this.Size = new Size(420, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            int yPos = 12;

            // === CONNECT TWO ===
            Label lblTwo = new Label
            {
                Text = "Connect Two Points:",
                Location = new Point(15, yPos),
                Size = new Size(380, 20),
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            yPos += 22;

            cmbPoint1 = new ComboBox
            {
                Location = new Point(15, yPos),
                Size = new Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            Label lblArrow = new Label
            {
                Text = "→",
                Location = new Point(200, yPos),
                Size = new Size(20, 25),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            cmbPoint2 = new ComboBox
            {
                Location = new Point(220, yPos),
                Size = new Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            yPos += 30;

            btnConnectTwo = new Button
            {
                Text = "Connect These Two",
                Location = new Point(15, yPos),
                Size = new Size(385, 28),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnConnectTwo.FlatAppearance.BorderSize = 0;
            btnConnectTwo.Click += BtnConnectTwo_Click;
            yPos += 35;

            // === CONNECT MULTIPLE ===
            Label lblMulti = new Label
            {
                Text = "Connect Multiple Points (check & order):",
                Location = new Point(15, yPos),
                Size = new Size(380, 20),
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            yPos += 22;

            chkPoints = new CheckedListBox
            {
                Location = new Point(15, yPos),
                Size = new Size(385, 100),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true
            };
            yPos += 108;

            btnConnectSelected = new Button
            {
                Text = "Connect Checked (In List Order)",
                Location = new Point(15, yPos),
                Size = new Size(250, 28),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnConnectSelected.FlatAppearance.BorderSize = 0;
            btnConnectSelected.Click += BtnConnectSelected_Click;

            btnConnectAll = new Button
            {
                Text = "Chain All",
                Location = new Point(275, yPos),
                Size = new Size(125, 28),
                BackColor = Color.FromArgb(255, 140, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnConnectAll.FlatAppearance.BorderSize = 0;
            btnConnectAll.Click += BtnConnectAll_Click;
            yPos += 35;

            // === LINE COLOR ===
            Label lblColor = new Label
            {
                Text = "Line Color:",
                Location = new Point(15, yPos),
                Size = new Size(70, 25),
                ForeColor = Color.White
            };

            cmbColor = new ComboBox
            {
                Location = new Point(85, yPos),
                Size = new Size(140, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbColor.Items.AddRange(new object[] { "Green", "Red", "Blue", "Yellow", "Cyan", "White", "Orange" });
            cmbColor.SelectedIndex = 0;
            cmbColor.SelectedIndexChanged += (s, e) => UpdateColorPreview();

            colorPreview = new Panel
            {
                Location = new Point(235, yPos),
                Size = new Size(30, 25),
                BackColor = Color.LimeGreen,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(290, yPos),
                Size = new Size(110, 28),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                lblTwo, cmbPoint1, lblArrow, cmbPoint2, btnConnectTwo,
                lblMulti, chkPoints, btnConnectSelected, btnConnectAll,
                lblColor, cmbColor, colorPreview, btnCancel
            });

            this.CancelButton = btnCancel;
        }

        private void PopulateLists()
        {
            cmbPoint1.Items.Clear();
            cmbPoint2.Items.Clear();
            chkPoints.Items.Clear();

            foreach (var p in points.OrderBy(p => p.Name))
            {
                string display = $"{p.Name} ({p.Start.X},{p.Start.Y})";
                var item = new PointItem(p, display);

                cmbPoint1.Items.Add(item);
                cmbPoint2.Items.Add(item);
                chkPoints.Items.Add(item); // ✅ Store PointItem directly
            }
        }

        private void UpdateColorPreview()
        {
            string selectedColor = cmbColor.SelectedItem?.ToString() ?? "Green";
            switch (selectedColor)
            {
                case "Red": LineColor = Color.Red; break;
                case "Green": LineColor = Color.LimeGreen; break;
                case "Blue": LineColor = Color.Blue; break;
                case "Yellow": LineColor = Color.Yellow; break;
                case "Cyan": LineColor = Color.Cyan; break;
                case "White": LineColor = Color.White; break;
                case "Orange": LineColor = Color.Orange; break;
                default: LineColor = Color.LimeGreen; break;
            }
            colorPreview.BackColor = LineColor;
        }

        private void BtnConnectTwo_Click(object sender, EventArgs e)
        {
            if (cmbPoint1.SelectedItem == null || cmbPoint2.SelectedItem == null)
            {
                MessageBox.Show("Please select two points.", "Selection Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var p1 = ((PointItem)cmbPoint1.SelectedItem).Point;
            var p2 = ((PointItem)cmbPoint2.SelectedItem).Point;

            if (p1.ID == p2.ID)
            {
                MessageBox.Show("Please select two different points.", "Same Point",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedPoint1 = p1;
            SelectedPoint2 = p2;
            ConnectAll = false;
            ConnectSelected = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnConnectSelected_Click(object sender, EventArgs e)
        {
            if (chkPoints.CheckedItems.Count < 2)
            {
                MessageBox.Show("Check at least 2 points to connect.", "Not Enough",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedPoints.Clear();

            // ✅ Direct cast since we stored PointItem objects in the list
            foreach (PointItem item in chkPoints.CheckedItems)
            {
                SelectedPoints.Add(item.Point);
            }

            ConnectAll = false;
            ConnectSelected = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnConnectAll_Click(object sender, EventArgs e)
        {
            ConnectAll = true;
            ConnectSelected = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    internal class PointItem
    {
        public Measurement Point { get; }
        public string Display { get; }

        public PointItem(Measurement point, string display)
        {
            Point = point;
            Display = display;
        }

        public override string ToString() => Display;
    }
}