using System;
using System.Windows.Forms;

namespace kinectProject
{
    public class ReferenceInputDialog : Form
    {
        private NumericUpDown numericUpDown;
        private Button btnOk;
        private Button btnCancel;
        private Label lblPrompt;

        public float ReferenceLength { get; private set; }

        public ReferenceInputDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Set Reference Length";
            this.Size = new System.Drawing.Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblPrompt = new Label
            {
                Text = "Enter the reference length (in cm):",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 10)
            };

            numericUpDown = new NumericUpDown
            {
                Minimum = 0.1M,
                Maximum = 1000M,
                DecimalPlaces = 2,
                Value = 1,
                Location = new System.Drawing.Point(10, 40),
                Width = 260
            };

            btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(110, 80),
                Width = 80
            };
            btnOk.Click += (s, e) => { ReferenceLength = (float)numericUpDown.Value; };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(200, 80),
                Width = 80
            };

            this.Controls.Add(lblPrompt);
            this.Controls.Add(numericUpDown);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
        }
    }
}
    