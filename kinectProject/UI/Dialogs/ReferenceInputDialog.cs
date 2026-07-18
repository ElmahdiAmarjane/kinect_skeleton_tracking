using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class ReferenceInputDialog : Form
    {
        private TextBox textBox;
        public float ReferenceLength { get; private set; }

        public ReferenceInputDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Set Reference Length";
            this.Size = new Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            Label label = new Label
            {
                Text = "Enter known length in centimeters:",
                Location = new Point(20, 20),
                Size = new Size(250, 20),
                ForeColor = Color.White
            };

            textBox = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(250, 20),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(60, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(150, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (float.TryParse(textBox.Text, out float result) && result > 0)
            {
                ReferenceLength = result;
            }
            else
            {
                MessageBox.Show("Please enter a valid positive number.");
                this.DialogResult = DialogResult.None;
            }
        }
    }
}