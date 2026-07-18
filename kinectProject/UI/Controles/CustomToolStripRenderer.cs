using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Custom toolstrip renderer for dark theme
    /// </summary>
    public class CustomToolStripRenderer : ToolStripProfessionalRenderer
    {
        public CustomToolStripRenderer() : base(new CustomColorTable()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                e.TextColor = Color.White;
            }
            else if (!e.Item.Enabled)
            {
                e.TextColor = Color.Gray;
            }
            else
            {
                e.TextColor = Color.White;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            e.Graphics.FillRectangle(
                new SolidBrush(Color.FromArgb(62, 62, 64)),
                e.Item.ContentRectangle);

            using (Pen pen = new Pen(Color.FromArgb(100, 100, 100), 1))
            {
                int y = e.Item.ContentRectangle.Height / 2;
                e.Graphics.DrawLine(pen,
                    e.Item.ContentRectangle.Left + 5, y,
                    e.Item.ContentRectangle.Right - 5, y);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                base.OnRenderMenuItemBackground(e);
            }
            else
            {
                Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
                using (Brush brush = new SolidBrush(Color.FromArgb(87, 87, 90)))
                {
                    e.Graphics.FillRectangle(brush, rc);
                }
                using (Pen pen = new Pen(Color.FromArgb(0, 122, 204)))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, rc.Width - 1, rc.Height - 1);
                }
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(100, 100, 100), 1))
            {
                e.Graphics.DrawRectangle(pen,
                    0, 0,
                    e.ToolStrip.Width - 1,
                    e.ToolStrip.Height - 1);
            }
        }
    }
}