using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
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
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            Label label = new Label
            {
                Text = "Select reference axis for angle measurement:",
                Location = new Point(10, 10),
                Size = new Size(220, 30),
                ForeColor = Color.White
            };

            Button xAxisBtn = new Button
            {
                Text = "X-Axis",
                Location = new Point(20, 50),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            xAxisBtn.Click += (s, e) =>
            {
                SelectedAxis = AxisType.X;
                this.DialogResult = DialogResult.OK;
            };

            Button yAxisBtn = new Button
            {
                Text = "Y-Axis",
                Location = new Point(120, 50),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            yAxisBtn.Click += (s, e) =>
            {
                SelectedAxis = AxisType.Y;
                this.DialogResult = DialogResult.OK;
            };

            this.Controls.Add(label);
            this.Controls.Add(xAxisBtn);
            this.Controls.Add(yAxisBtn);
        }
    }
}