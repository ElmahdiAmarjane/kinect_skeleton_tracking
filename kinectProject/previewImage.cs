using System.Drawing;
using System.Windows.Forms;

public class PreviewForm : Form
{
    private PictureBox pictureBox;
    private Button btnSaveButton;
    private Button btnCancelButton;

    public Bitmap PreviewImage
    {
        set
        {
            if (pictureBox != null)
            {
                pictureBox.Image?.Dispose();
                pictureBox.Image = value;
            }
        }
    }

    public PreviewForm()
    {
        this.Text = "Image Preview";
        this.Size = new Size(800, 600);
        this.MinimumSize = new Size(400, 300);
        this.Icon = System.Drawing.SystemIcons.Application;

        pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };
        pictureBox.MouseDown += PictureBox_MouseDown;
        this.Controls.Add(pictureBox);

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(240, 240, 240)
        };
        this.Controls.Add(buttonPanel);

        btnSaveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(4)
        };
        buttonPanel.Controls.Add(btnSaveButton);

        btnCancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(4)
        };
        buttonPanel.Controls.Add(btnCancelButton);
    }

    private void PictureBox_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Zoom In", null, (s, args) => pictureBox.SizeMode = PictureBoxSizeMode.Zoom);
            menu.Items.Add("Fit to Window", null, (s, args) => pictureBox.SizeMode = PictureBoxSizeMode.StretchImage);
            menu.Show(pictureBox, e.Location);
        }
    }
}