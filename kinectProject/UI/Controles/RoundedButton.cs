using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace kinectProject
{
    public class RoundedButtonHelper
    {
        public static Button CreateStyledButton(string text, Color backColor, EventHandler clickHandler, int minWidth = 90)
        {
            Button button = new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Height = 32,
                MinimumSize = new Size(minWidth, 32),
                Margin = new Padding(4, 0, 4, 0),
                Padding = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.2f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.2f);

            ApplyRoundedStyle(button, 8);
            button.Click += clickHandler;

            return button;
        }

        public static Button ApplyRoundedStyle(Button btn, int radius = 8)
        {
            btn.Paint += (s, e) =>
            {
                Rectangle rect = btn.ClientRectangle;
                using (GraphicsPath path = new GraphicsPath())
                {
                    int r = radius;
                    path.AddArc(rect.X, rect.Y, r, r, 180, 90);
                    path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
                    path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
                    path.CloseAllFigures();
                    btn.Region = new Region(path);
                }
            };

            return btn;
        }
    }
}