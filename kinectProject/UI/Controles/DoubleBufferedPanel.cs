using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Panel with double buffering enabled for smooth rendering
    /// </summary>
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }
}