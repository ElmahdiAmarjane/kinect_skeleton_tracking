using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

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

            // Label
            Label label = new Label();
            label.Text = "Enter a name for this measurement:";
            label.Location = new Point(20, 20);
            label.Size = new Size(300, 20);
            label.ForeColor = Color.White;

            // TextBox
            textBox = new TextBox();
            textBox.Text = defaultName;
            textBox.Location = new Point(20, 50);
            textBox.Size = new Size(300, 20);
            textBox.BackColor = Color.FromArgb(37, 37, 38);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;

            // Select all text for easy editing
            textBox.SelectAll();
            textBox.Focus();

            // CheckBox "Ne plus demander"
            dontAskCheckBox = new CheckBox();
            dontAskCheckBox.Text = "Don't ask for rename automatically";
            dontAskCheckBox.Location = new Point(20, 80);
            dontAskCheckBox.Size = new Size(300, 20);
            dontAskCheckBox.ForeColor = Color.White;

            // Boutons
            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(80, 110);
            okButton.Size = new Size(80, 25);
            okButton.BackColor = Color.FromArgb(62, 62, 64);
            okButton.ForeColor = Color.White;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button();
            cancelButton.Text = "Use Default";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(180, 110);
            cancelButton.Size = new Size(80, 25);
            cancelButton.BackColor = Color.FromArgb(62, 62, 64);
            cancelButton.ForeColor = Color.White;
            cancelButton.FlatStyle = FlatStyle.Flat;

            Button skipButton = new Button();
            skipButton.Text = "Skip";
            skipButton.DialogResult = DialogResult.Ignore;
            skipButton.Location = new Point(270, 110);
            skipButton.Size = new Size(50, 25);
            skipButton.BackColor = Color.FromArgb(62, 62, 64);
            skipButton.ForeColor = Color.White;
            skipButton.FlatStyle = FlatStyle.Flat;
            skipButton.Click += SkipButton_Click;

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            this.Controls.Add(dontAskCheckBox);
            this.Controls.Add(okButton);
            this.Controls.Add(cancelButton);
            this.Controls.Add(skipButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            NewName = textBox.Text.Trim();
            DontAskAgain = dontAskCheckBox.Checked;

            if (string.IsNullOrWhiteSpace(NewName))
            {
                MessageBox.Show("Please enter a valid name or click 'Use Default'.",
                              "Invalid Name",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
            }
        }

        private void SkipButton_Click(object sender, EventArgs e)
        {
            NewName = textBox.Text; // Garder le nom par défaut
            DontAskAgain = dontAskCheckBox.Checked;
            this.DialogResult = DialogResult.OK;
        }
    }
}
