using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class CustomRenameDialog : Form
    {
        private TextBox textBox;
        public string NewName { get; private set; }

        public CustomRenameDialog(string currentName, string prompt = "Enter new name for measurement:")
        {
            InitializeComponent(currentName, prompt);
        }

        private void InitializeComponent(string currentName, string prompt)
        {
            this.Text = "Rename Measurement";
            this.Size = new Size(350, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            Label label = new Label
            {
                Text = prompt,
                Location = new Point(20, 20),
                Size = new Size(300, 30),
                AutoSize = true,
                ForeColor = Color.White
            };

            textBox = new TextBox
            {
                Text = currentName,
                Location = new Point(20, 60),
                Size = new Size(300, 20),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(80, 90),
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
                Location = new Point(170, 90),
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
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                NewName = textBox.Text.Trim();
            }
            else
            {
                MessageBox.Show("Please enter a valid name.");
                this.DialogResult = DialogResult.None;
            }
        }
    }
}