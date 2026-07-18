using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    public class RenameDialog : Form
    {
        private TextBox textBox;
        public string NewName { get; private set; }

        public RenameDialog(string currentName)
        {
            InitializeComponent(currentName);
        }

        private void InitializeComponent(string currentName)
        {
            this.SuspendLayout();
            // 
            // RenameDialog
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "RenameDialog";
            this.Load += new System.EventHandler(this.RenameDialog_Load);
            this.ResumeLayout(false);

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

        private void RenameDialog_Load(object sender, EventArgs e)
        {

        }
    }
}