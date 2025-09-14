using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KinectProject
{
    public partial class MultiCurveViewer : Form
    {
        private List<SpineCurveData> allCurves = new List<SpineCurveData>();
        private int curveSpacing = 200;
        private int curveHeight = 120; // Estimated height for each curve
        private int verticalOffset = 0;
        private int maxVerticalOffset = 0;
        private const int ScrollStep = 50;
   

   
      

        // Cache fonts and brushes
        private Font infoFont;
        private Font labelFont;
        private Brush[] curveBrushes;
        private Pen[] curvePens;
        //
        private int? activeCurveIndex = null;
        private float? referenceLineX = null;
        private PointF? selectedPoint = null;
        private bool isDraggingReference = false;

        public MultiCurveViewer()
        {
            InitializeComponent();
            InitializeComponents();
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 60);
            this.ForeColor = Color.White;
            this.KeyPreview = true; // Enable keyboard events

            // Add resize protection
            this.ResizeBegin += (s, e) => { this.SuspendLayout(); };
            this.ResizeEnd += (s, e) => { this.ResumeLayout(true); this.Invalidate(); };

            // Enable interactive features
            EnableInteractiveFeatures();
        }

        private void InitializeComponents()
        {
            // Create fonts
            infoFont = new Font("Segoe UI", 9, FontStyle.Regular);
            labelFont = new Font("Segoe UI", 8, FontStyle.Regular);

            // Create distinct colors for different curves
            curveBrushes = new Brush[]
            {
                Brushes.Cyan,
                Brushes.LightGreen,
                Brushes.Orange,
                Brushes.Violet,
                Brushes.Yellow,
                Brushes.Pink,
                Brushes.LightBlue
            };

           

            // Set up form properties
            this.ClientSize = new Size(800, 600);
            this.Text = "Multi-Curve Viewer - Comparative Analysis";
            this.Padding = new Padding(10);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Add scroll event
            this.MouseWheel += MultiCurveViewer_MouseWheel;
            this.KeyDown += MultiCurveViewer_KeyDown;
        }

        public void LoadCurves(List<SpineCurveData> curves)
        {
            allCurves = curves;

            // ADD DEBUG INFO
            foreach (var curve in curves)
            {
                if (curve.Points != null)
                {
                    Console.WriteLine($"Curve with {curve.Points.Count} points");
                    if (curve.Points.Count > 0)
                    {
                        var firstPoint = curve.Points[0];
                        Console.WriteLine($"First point: X={firstPoint.X}, Y={firstPoint.Y}");
                    }
                }
            }

            CalculateLayout();
            Invalidate();
        }

        public void AddCurve(SpineCurveData curve)
        {
            allCurves.Add(curve);
            CalculateLayout();
            Invalidate();
        }

        private void CalculateLayout()
        {
            // Calculate total height needed based on actual curve count and spacing
            int totalHeight = allCurves.Count * curveSpacing + 100;
            maxVerticalOffset = Math.Max(0, totalHeight - ClientSize.Height);
            verticalOffset = Math.Min(verticalOffset, maxVerticalOffset);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (allCurves.Count == 0)
            {
                e.Graphics.DrawString("No curves loaded. Import files using the main application.",
                    infoFont, Brushes.White, 20, 20);
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 20, 30));

            // Draw title
            g.DrawString($"Multi-Curve Analysis - {allCurves.Count} Curves Loaded",
                new Font("Segoe UI", 12, FontStyle.Bold), Brushes.White, 20, 10 - verticalOffset);

            // Draw each curve with visibility check
            for (int curveIndex = 0; curveIndex < allCurves.Count; curveIndex++)
            {
                var curveData = allCurves[curveIndex];
                if (curveData.Points == null || curveData.Points.Count == 0)
                    continue;

                int yBase = 50 + curveIndex * curveSpacing - verticalOffset;

                // Only draw if the curve is at least partially visible
                if (yBase + curveHeight >= 0 && yBase - curveHeight <= ClientSize.Height)
                {
                    DrawCurve(g, curveData, curveIndex, yBase);
                }
            }

            // Draw scroll indicator if needed
            if (maxVerticalOffset > 0)
            {
                DrawScrollIndicator(g);
            }
        }

        private void DrawCurve(Graphics g, SpineCurveData curveData, int curveIndex, int yBase)
        {
            var points = curveData.Points.ConvertAll(p => p.ToPointF());
            if (points.Count < 2) return;

            // Use consistent scaling
            float offsetX = 50f;
            float scaleX = 0.1f;
            float scaleY = 1.5f;

            // Draw curve info - move to top of curve area
            string info = $"[{curveIndex + 1}] {curveData.CaptureTime:yyyy-MM-dd HH:mm:ss} - " +
                         $"{points.Count} points - Angle: {curveData.SpineAngle:F1}°";

            g.DrawString(info, infoFont, curveBrushes[curveIndex % curveBrushes.Length], 20, yBase);

            // Draw horizontal separator at bottom of info area
            using (var grayPen = new Pen(Color.Gray))
            {
                g.DrawLine(grayPen, 20, yBase + 20, ClientSize.Width - 40, yBase + 20);
            }

            // Calculate curve drawing area (below the info)
            int curveAreaTop = yBase + 30;
            int curveAreaHeight = curveHeight - 40;

            // Create pen on demand
            Color[] curveColors = new Color[]
            {
        Color.Cyan, Color.LightGreen, Color.Orange, Color.Violet,
        Color.Yellow, Color.Pink, Color.LightBlue
            };

            using (var curvePen = new Pen(curveColors[curveIndex % curveColors.Length], 2))
            {
                // Find min and max Y values for this curve to center it vertically
                float minY = points.Min(p => p.Y);
                float maxY = points.Max(p => p.Y);
                float yRange = maxY - minY;

                if (yRange == 0) yRange = 1; // Avoid division by zero

                for (int i = 1; i < points.Count; i++)
                {
                    if (float.IsNaN(points[i - 1].X) || float.IsNaN(points[i - 1].Y) ||
                        float.IsNaN(points[i].X) || float.IsNaN(points[i].Y))
                        continue;

                    // Calculate X position
                    float x1 = offsetX + points[i - 1].X * scaleX;
                    float x2 = offsetX + points[i].X * scaleX;

                    // Calculate Y position - normalize and center within curve area
                    float normalizedY1 = (points[i - 1].Y - minY) / yRange;
                    float normalizedY2 = (points[i].Y - minY) / yRange;

                    float y1 = curveAreaTop + curveAreaHeight - (normalizedY1 * curveAreaHeight);
                    float y2 = curveAreaTop + curveAreaHeight - (normalizedY2 * curveAreaHeight);

                    if (IsValidCoordinate(x1, y1) && IsValidCoordinate(x2, y2) &&
                        IsWithinVisibleArea(x1, y1) && IsWithinVisibleArea(x2, y2))
                    {
                        try
                        {
                            g.DrawLine(curvePen, x1, y1, x2, y2);
                        }
                        catch (ArgumentException)
                        {
                            continue;
                        }
                    }
                }
            }

            // Draw max point and interactive reference line
            if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < points.Count)
            {
                var maxPoint = points[curveData.MaxZIndex];
                if (!float.IsNaN(maxPoint.X) && !float.IsNaN(maxPoint.Y))
                {
                    // Find min and max Y values for normalization
                    float minY = points.Min(p => p.Y);
                    float maxY = points.Max(p => p.Y);
                    float yRange = maxY - minY;
                    if (yRange == 0) yRange = 1;

                    float x = offsetX + maxPoint.X * scaleX;
                    float normalizedY = (maxPoint.Y - minY) / yRange;
                    float y = curveAreaTop + curveAreaHeight - (normalizedY * curveAreaHeight);

                    if (IsValidCoordinate(x, y) && IsWithinVisibleArea(x, y))
                    {
                        try
                        {
                            // Draw max point
                            g.FillEllipse(Brushes.Red, x - 3, y - 3, 6, 6);

                            // Draw reference line (use dragged position if active)
                            float refX = (isDraggingReference && activeCurveIndex == curveIndex && referenceLineX.HasValue)
                                ? referenceLineX.Value : x;

                            using (var refLinePen = new Pen(Color.Red, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                            {
                                g.DrawLine(refLinePen, refX, curveAreaTop, refX, curveAreaTop + curveAreaHeight);
                            }

                            // Label the reference line
                            string refLabel = (isDraggingReference && activeCurveIndex == curveIndex)
                                ? $"Ref: {((refX - offsetX) / scaleX):F1}mm"
                                : $"Max: {maxPoint.X:F1}mm";

                            g.DrawString(refLabel, labelFont, Brushes.Red, refX + 5, curveAreaTop - 15);

                            // Draw distance measurement if point is selected on this curve
                            if (activeCurveIndex == curveIndex && selectedPoint.HasValue)
                            {
                                float selectedX = offsetX + selectedPoint.Value.X * scaleX;
                                float normalizedSelectedY = (selectedPoint.Value.Y - minY) / yRange;
                                float selectedY = curveAreaTop + curveAreaHeight - (normalizedSelectedY * curveAreaHeight);

                                // Draw selected point
                                g.FillEllipse(Brushes.Yellow, selectedX - 4, selectedY - 4, 8, 8);

                                // Draw line to reference and show distance
                                using (var measurePen = new Pen(Color.Yellow, 1))
                                {
                                    g.DrawLine(measurePen, selectedX, selectedY, refX, selectedY);
                                }

                                float distance = Math.Abs(selectedPoint.Value.X - ((refX - offsetX) / scaleX));
                                g.DrawString($"{distance:F1}mm", labelFont, Brushes.Yellow,
                                            (selectedX + refX) / 2, selectedY - 15);
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Ignore
                        }
                    }
                }
            }
        }

        // Enhanced validation methods
        private bool IsValidCoordinate(float x, float y)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x) &&
                   !float.IsNaN(y) && !float.IsInfinity(y);
        }

        private bool IsWithinVisibleArea(float x, float y)
        {
            // Check if coordinates are within the visible area with some margin
            float margin = 100f;
            return x >= -margin && x <= ClientSize.Width + margin &&
                   y >= -margin && y <= ClientSize.Height + margin;
        }

        private void DrawScrollIndicator(Graphics g)
        {
            // Draw scroll bar on right side
            int scrollWidth = 10;
            int scrollHeight = ClientSize.Height - 40;
            int scrollX = ClientSize.Width - scrollWidth - 5;
            int scrollY = 20;

            // Background
            g.FillRectangle(Brushes.DarkGray, scrollX, scrollY, scrollWidth, scrollHeight);

            // Thumb position
            float thumbPosition = (float)verticalOffset / maxVerticalOffset;
            int thumbHeight = Math.Max(30, (int)(scrollHeight * (ClientSize.Height / (float)(maxVerticalOffset + ClientSize.Height))));
            int thumbY = scrollY + (int)(thumbPosition * (scrollHeight - thumbHeight));

            g.FillRectangle(Brushes.LightGray, scrollX, thumbY, scrollWidth, thumbHeight);
        }

        private void MultiCurveViewer_MouseWheel(object sender, MouseEventArgs e)
        {
            verticalOffset -= e.Delta / 5;
            verticalOffset = Math.Max(0, Math.Min(verticalOffset, maxVerticalOffset));
            Invalidate();
        }

        private void MultiCurveViewer_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    verticalOffset -= ScrollStep;
                    break;
                case Keys.Down:
                    verticalOffset += ScrollStep;
                    break;
                case Keys.Home:
                    verticalOffset = 0;
                    break;
                case Keys.End:
                    verticalOffset = maxVerticalOffset;
                    break;
                case Keys.Escape:
                    this.Close();
                    return;
            }

            verticalOffset = Math.Max(0, Math.Min(verticalOffset, maxVerticalOffset));
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                // Check if click is on scroll bar
                int scrollWidth = 10;
                int scrollX = ClientSize.Width - scrollWidth - 5;

                if (e.X >= scrollX && e.X <= scrollX + scrollWidth)
                {
                    ScrollToPosition(e.Y);
                }
            }
        }

      
        private void ScrollToPosition(int mouseY)
        {
            int scrollHeight = ClientSize.Height - 40;
            int scrollY = 20;

            if (mouseY >= scrollY && mouseY <= scrollY + scrollHeight)
            {
                float relativePosition = (mouseY - scrollY) / (float)scrollHeight;
                verticalOffset = (int)(relativePosition * maxVerticalOffset);
                Invalidate();
            }
        }





        ////

        // Add these methods for interactive features
        private void EnableInteractiveFeatures()
        {
            this.MouseDown += MultiCurveViewer_MouseDown;
            this.MouseMove += MultiCurveViewer_MouseMove;
            this.MouseUp += MultiCurveViewer_MouseUp;
        }

        private void MultiCurveViewer_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Check if click is near any reference line
                for (int i = 0; i < allCurves.Count; i++)
                {
                    var curveData = allCurves[i];
                    if (curveData.Points == null || curveData.Points.Count == 0) continue;

                    int yBase = 50 + i * curveSpacing - verticalOffset;
                    int curveAreaTop = yBase + 30;

                    var points = curveData.Points.ConvertAll(p => p.ToPointF());

                    if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < points.Count)
                    {
                        var maxPoint = points[curveData.MaxZIndex];
                        float x = 50f + maxPoint.X * 0.1f;

                        // Check if click is near the reference line (within 10 pixels)
                        if (Math.Abs(e.X - x) < 10 && e.Y >= curveAreaTop && e.Y <= curveAreaTop + curveHeight - 40)
                        {
                            isDraggingReference = true;
                            activeCurveIndex = i;
                            referenceLineX = x;
                            this.Cursor = Cursors.SizeWE;
                            return;
                        }
                    }
                }

                // If not clicking on reference line, select a point on curve
                SelectPointOnCurve(e.Location);
            }
        }

        private void SelectPointOnCurve(Point mouseLocation)
        {
            selectedPoint = null;
            activeCurveIndex = null;

            for (int i = 0; i < allCurves.Count; i++)
            {
                var curveData = allCurves[i];
                if (curveData.Points == null || curveData.Points.Count == 0) continue;

                int yBase = 50 + i * curveSpacing - verticalOffset;
                int curveAreaTop = yBase + 30;
                var points = curveData.Points.ConvertAll(p => p.ToPointF());

                // Find min and max Y values for normalization
                float minY = points.Min(p => p.Y);
                float maxY = points.Max(p => p.Y);
                float yRange = maxY - minY;
                if (yRange == 0) yRange = 1;

                // Find closest point on this curve
                float minDistance = 15f; // pixels
                PointF closestPoint = PointF.Empty;

                foreach (var point in points)
                {
                    float x = 50f + point.X * 0.1f;
                    float normalizedY = (point.Y - minY) / yRange;
                    float y = curveAreaTop + (curveHeight - 40) - (normalizedY * (curveHeight - 40));

                    float distance = (float)Math.Sqrt(Math.Pow(mouseLocation.X - x, 2) + Math.Pow(mouseLocation.Y - y, 2));

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPoint = new PointF(point.X, point.Y);
                        activeCurveIndex = i;
                    }
                }

                if (activeCurveIndex.HasValue)
                {
                    selectedPoint = closestPoint;
                    break;
                }
            }

            Invalidate();
        }
        private void MultiCurveViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingReference && activeCurveIndex.HasValue && referenceLineX.HasValue)
            {
                // Move reference line
                referenceLineX = e.X;
                Invalidate();
            }
            else
            {
                // Update cursor when near reference lines
                bool nearReference = false;
                for (int i = 0; i < allCurves.Count; i++)
                {
                    var curveData = allCurves[i];
                    if (curveData.Points == null || curveData.Points.Count == 0) continue;

                    int yBase = 50 + i * curveSpacing - verticalOffset;
                    var points = curveData.Points.ConvertAll(p => p.ToPointF());

                    if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < points.Count)
                    {
                        var maxPoint = points[curveData.MaxZIndex];
                        float x = 50f + maxPoint.X * 0.1f;

                        if (Math.Abs(e.X - x) < 10 && e.Y >= yBase - 30 && e.Y <= yBase + 120)
                        {
                            this.Cursor = Cursors.SizeWE;
                            nearReference = true;
                            break;
                        }
                    }
                }

                if (!nearReference)
                {
                    this.Cursor = Cursors.Default;
                }
            }
        }

        private void MultiCurveViewer_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isDraggingReference)
            {
                isDraggingReference = false;
                this.Cursor = Cursors.Default;
            }
        }

  











        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                infoFont?.Dispose();
                labelFont?.Dispose();
              
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(800, 600);
            this.Name = "MultiCurveViewer";
            this.Text = "Multi-Curve Viewer";
            this.ResumeLayout(false);
        }
    }
}