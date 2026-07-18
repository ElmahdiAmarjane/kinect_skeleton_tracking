using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class AutoRenameDialog : Form
    {
        private TextBox textBox;
        private CheckBox dontAskCheckBox;

        public string NewName { get; private set; }
        public bool DontAskAgain { get; private set; }

        public AutoRenameDialog(string defaultName)
        {
            InitializeComponent(defaultName);
        }

        private void InitializeComponent(string defaultName)
        {
            this.Text = "Rename Measurement";
            this.Size = new Size(350, 180);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            Label label = new Label
            {
                Text = "Enter name for measurement:",
                Location = new Point(20, 20),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };

            textBox = new TextBox
            {
                Text = defaultName,
                Location = new Point(20, 50),
                Size = new Size(300, 20),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            dontAskCheckBox = new CheckBox
            {
                Text = "Don't ask again (use auto-rename)",
                Location = new Point(20, 80),
                Size = new Size(200, 20),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(80, 110),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(180, 110),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            this.Controls.AddRange(new Control[] { label, textBox, dontAskCheckBox, okButton, cancelButton });
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            NewName = textBox.Text.Trim();
            DontAskAgain = dontAskCheckBox.Checked;

            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("Please enter a valid name.");
                this.DialogResult = DialogResult.None;
            }
        }
    }
}