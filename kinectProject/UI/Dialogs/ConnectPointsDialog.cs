using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace kinectProject
{
    public class ConnectPointsDialog : Form
    {
        private List<Measurement> points;
        private CheckedListBox pointListBox;
        private ListBox connectionsListBox;
        private Button btnAddConnection;
        private Button btnRemoveConnection;
        private Button btnConnectAll;
        private Button btnOK;
        private Button btnCancel;

        private List<(Measurement p1, Measurement p2)> connections = new List<(Measurement, Measurement)>();
        public List<(Measurement p1, Measurement p2)> SelectedConnections => connections;

        public ConnectPointsDialog(List<Measurement> pointMeasurements)
        {
            points = pointMeasurements;
            InitializeComponent();
            PopulatePointList();
        }

        private void InitializeComponent()
        {
            this.Text = "Connect Points - Create Lines";
            this.Size = new Size(550, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            Label lblPoints = new Label
            {
                Text = "Select two points to connect:",
                Location = new Point(15, 10),
                Size = new Size(250, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            pointListBox = new CheckedListBox
            {
                Location = new Point(15, 35),
                Size = new Size(250, 250),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true
            };
            pointListBox.ItemCheck += PointListBox_ItemCheck;

            btnAddConnection = new Button
            {
                Text = "→ Connect Selected →",
                Location = new Point(275, 100),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            btnAddConnection.FlatAppearance.BorderSize = 0;
            btnAddConnection.Click += BtnAddConnection_Click;

            Label lblConnections = new Label
            {
                Text = "Connections:",
                Location = new Point(15, 295),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            connectionsListBox = new ListBox
            {
                Location = new Point(15, 318),
                Size = new Size(380, 60),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.LightGreen,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnRemoveConnection = new Button
            {
                Text = "Remove",
                Location = new Point(405, 318),
                Size = new Size(80, 28),
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRemoveConnection.FlatAppearance.BorderSize = 0;
            btnRemoveConnection.Click += BtnRemoveConnection_Click;

            btnConnectAll = new Button
            {
                Text = "Connect All (Chain)",
                Location = new Point(275, 160),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnConnectAll.FlatAppearance.BorderSize = 0;
            btnConnectAll.Click += BtnConnectAll_Click;

            btnOK = new Button
            {
                Text = "OK - Create Lines",
                DialogResult = DialogResult.OK,
                Location = new Point(250, 370),
                Size = new Size(130, 35),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(390, 370),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.AddRange(new Control[]
            {
                lblPoints, pointListBox,
                btnAddConnection, btnConnectAll,
                lblConnections, connectionsListBox, btnRemoveConnection,
                btnOK, btnCancel
            });
        }

        private void PopulatePointList()
        {
            pointListBox.Items.Clear();
            foreach (var p in points)
            {
                pointListBox.Items.Add($"{p.Name} (ID:{p.ID}) @ ({p.Start.X},{p.Start.Y})");
            }
        }

        private void PointListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Enable Add button only when exactly 2 items are checked
            this.BeginInvoke((Action)(() =>
            {
                int checkedCount = pointListBox.CheckedIndices.Count;
                if (e.NewValue == CheckState.Checked) checkedCount++;
                else checkedCount--;
                btnAddConnection.Enabled = (checkedCount == 2);
            }));
        }

        private void BtnAddConnection_Click(object sender, EventArgs e)
        {
            var checkedIndices = pointListBox.CheckedIndices.Cast<int>().ToList();
            if (checkedIndices.Count != 2) return;

            var p1 = points[checkedIndices[0]];
            var p2 = points[checkedIndices[1]];

            connections.Add((p1, p2));
            connectionsListBox.Items.Add($"{p1.Name} ↔ {p2.Name}");

            // Uncheck
            for (int i = 0; i < pointListBox.Items.Count; i++)
                pointListBox.SetItemChecked(i, false);
        }

        private void BtnRemoveConnection_Click(object sender, EventArgs e)
        {
            if (connectionsListBox.SelectedIndex >= 0)
            {
                connections.RemoveAt(connectionsListBox.SelectedIndex);
                connectionsListBox.Items.RemoveAt(connectionsListBox.SelectedIndex);
            }
        }

        private void BtnConnectAll_Click(object sender, EventArgs e)
        {
            connections.Clear();
            connectionsListBox.Items.Clear();

            // Chain: P1→P2, P2→P3, P3→P4...
            for (int i = 0; i < points.Count - 1; i++)
            {
                connections.Add((points[i], points[i + 1]));
                connectionsListBox.Items.Add($"{points[i].Name} ↔ {points[i + 1].Name}");
            }
        }
    }
}