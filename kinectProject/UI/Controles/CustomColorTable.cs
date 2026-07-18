using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Custom color table for dark theme toolstrip
    /// </summary>
    public class CustomColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(62, 62, 64);
        public override Color MenuBorder => Color.FromArgb(100, 100, 100);
        public override Color MenuItemSelected => Color.FromArgb(87, 87, 90);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(87, 87, 90);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(87, 87, 90);
        public override Color ImageMarginGradientBegin => Color.FromArgb(55, 55, 58);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(55, 55, 58);
        public override Color ImageMarginGradientEnd => Color.FromArgb(55, 55, 58);
        public override Color ToolStripBorder => Color.FromArgb(100, 100, 100);
        public override Color ToolStripContentPanelGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color ToolStripContentPanelGradientEnd => Color.FromArgb(62, 62, 64);
        public override Color ToolStripGradientBegin => Color.FromArgb(55, 55, 58);
        public override Color ToolStripGradientMiddle => Color.FromArgb(55, 55, 58);
        public override Color ToolStripGradientEnd => Color.FromArgb(55, 55, 58);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(0, 122, 204);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(0, 122, 204);
        public override Color MenuItemBorder => Color.FromArgb(0, 122, 204);
        public override Color SeparatorDark => Color.FromArgb(100, 100, 100);
        public override Color SeparatorLight => Color.FromArgb(45, 45, 48);
    }

}