using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public partial class ForReferenceInputDialog : Form
    {
        public float ReferenceLength { get; private set; } = 200f; // Valeur par défaut 200mm

        public ForReferenceInputDialog()
        {
            InitializeComponent();
            SetupDialog();
        }

        private void SetupDialog()
        {
            this.Text = "Set Reference Length";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Label
            var label = new Label
            {
                Text = "Enter known reference length (mm):",
                Location = new Point(10, 10),
                Width = 200
            };

            // Numeric Input
            var numericInput = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 1000,
                Value = (decimal)ReferenceLength,
                Location = new Point(10, 40),
                Width = 100,
                DecimalPlaces = 1
            };

            // OK Button
            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 40)
            };

            okButton.Click += (s, e) =>
            {
                ReferenceLength = (float)numericInput.Value;
                this.Close();
            };

            this.Controls.Add(label);
            this.Controls.Add(numericInput);
            this.Controls.Add(okButton);

            this.ClientSize = new Size(220, 80);
        }
    }
}