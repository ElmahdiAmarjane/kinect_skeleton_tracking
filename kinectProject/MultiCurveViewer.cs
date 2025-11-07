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
        private int curveHeight = 300;
        private int curveWidth = 400;

        private int horizontalSpacing = 20;
        private int verticalSpacing = 20;

        private int horizontalOffset = 0;
        private int verticalOffset = 0;
        private int maxHorizontalOffset = 0;
        private int maxVerticalOffset = 0;
        private const int ScrollStep = 50;

        // Interaction state
        private int? activeCurveIndex = null;
        private bool isDraggingRefLine = false;
        private PointF? selectedPoint = null;
        private int? selectedPointIndex = null;

        // Per-curve manual reference Z values (in mm - this is the source of truth)
        private Dictionary<int, float> manualZRefs = new Dictionary<int, float>();

        // Fonts / pens
        private Font infoFont;
        private Font labelFont;
        private Pen axisPen = new Pen(Color.Gray, 1);

        // Scaling constants
        private const float OffsetXInsideBox = 50f;
        private const float ScaleX = 0.1f; // pixels per mm

        // Fine control step for keyboard (0.5mm as requested)
        private const float FineControlStep = 0.5f; // mm

        public MultiCurveViewer()
        {
            InitializeComponent();
            InitializeComponents();
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 60);
            this.ForeColor = Color.White;
            this.KeyPreview = true;

            this.ResizeBegin += (s, e) => { this.SuspendLayout(); };
            this.ResizeEnd += (s, e) => { this.ResumeLayout(true); this.Invalidate(); };

            EnableInteractiveFeatures();
        }

        private void InitializeComponents()
        {
            infoFont = new Font("Segoe UI", 9, FontStyle.Regular);
            labelFont = new Font("Segoe UI", 8, FontStyle.Regular);

            this.ClientSize = new Size(900, 700);
            this.Text = "Multi-Curve Viewer - High Precision Control (0.5mm)";
            this.Padding = new Padding(10);

            this.MouseWheel += MultiCurveViewer_MouseWheel;
            this.KeyDown += MultiCurveViewer_KeyDown;
        }

        public void LoadCurves(List<SpineCurveData> curves)
        {
            allCurves = curves ?? new List<SpineCurveData>();
            manualZRefs.Clear();
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
            if (allCurves == null) return;

            int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));
            int rows = (int)Math.Ceiling((double)allCurves.Count / curvesPerRow);

            int totalWidth = Math.Min(allCurves.Count, curvesPerRow) * (curveWidth + horizontalSpacing) + 40;
            int totalHeight = rows * (curveHeight + verticalSpacing) + 40;

            maxHorizontalOffset = Math.Max(0, totalWidth - ClientSize.Width);
            maxVerticalOffset = Math.Max(0, totalHeight - ClientSize.Height);

            horizontalOffset = Math.Max(0, Math.Min(horizontalOffset, maxHorizontalOffset));
            verticalOffset = Math.Max(0, Math.Min(verticalOffset, maxVerticalOffset));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 20, 30));

            if (allCurves == null || allCurves.Count == 0)
            {
                g.DrawString("No curves loaded. Import files using the main application.", infoFont, Brushes.White, 20, 20);
                return;
            }

            int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));

            for (int i = 0; i < allCurves.Count; i++)
            {
                var curveData = allCurves[i];
                if (curveData?.Points == null || curveData.Points.Count == 0) continue;

                int row = i / curvesPerRow;
                int col = i % curvesPerRow;

                int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
                int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;

                if (xBase + curveWidth < 0 || xBase > ClientSize.Width ||
                    yBase + curveHeight < 0 || yBase > ClientSize.Height) continue;

                DrawCurveBox(g, curveData, i, xBase, yBase);
            }

            if (maxHorizontalOffset > 0 || maxVerticalOffset > 0)
            {
                DrawScrollIndicators(g);
            }

            // Draw control hints
            DrawControlHints(g);
        }

        private void DrawCurveBox(Graphics g, SpineCurveData curveData, int curveIndex, int xBase, int yBase)
        {
            Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

            g.FillRectangle(Brushes.Black, curveArea);
            g.DrawRectangle(Pens.Gray, curveArea);

            string info = $"[{curveIndex + 1}] {curveData.CaptureTime:yyyy-MM-dd HH:mm:ss}  Angle: {curveData.SpineAngle:F1}°";
            g.DrawString(info, infoFont, Brushes.LightGray, xBase + 10, yBase + 5);

            var pts = curveData.Points.ConvertAll(p => p.ToPointF());
            if (pts.Count < 2) return;

            float offsetX = curveArea.Left + OffsetXInsideBox;
            float scaleX = ScaleX;

            // Draw axes
            float axisX = offsetX - 10;
            g.DrawLine(axisPen, axisX, curveArea.Top, axisX, curveArea.Bottom);
            g.DrawLine(axisPen, curveArea.Left, curveArea.Bottom - 1, curveArea.Right, curveArea.Bottom - 1);

            // Draw curve
            Color[] palette = new Color[] { Color.Cyan, Color.LightGreen, Color.Orange, Color.Violet, Color.Yellow, Color.Pink, Color.LightBlue };
            using (Pen curvePen = new Pen(palette[curveIndex % palette.Length], 2))
            {
                for (int k = 1; k < pts.Count; k++)
                {
                    if (float.IsNaN(pts[k - 1].X) || float.IsNaN(pts[k - 1].Y) ||
                        float.IsNaN(pts[k].X) || float.IsNaN(pts[k].Y)) continue;

                    float x1 = offsetX + pts[k - 1].X * scaleX;
                    float y1 = curveArea.Top + pts[k - 1].Y;
                    float x2 = offsetX + pts[k].X * scaleX;
                    float y2 = curveArea.Top + pts[k].Y;

                    g.SetClip(curveArea);
                    try { g.DrawLine(curvePen, x1, y1, x2, y2); }
                    catch (ArgumentException) { }
                    g.ResetClip();
                }
            }

            // Get reference Z value (stored in mm)
            float autoZRef = pts.Any() ? pts[curveData.MaxZIndex].X : 0f;
            float zRef = manualZRefs.ContainsKey(curveIndex) ? manualZRefs[curveIndex] : autoZRef;

            // Convert Z value to pixel position for drawing
            float refXFloat = offsetX + zRef * scaleX;
            int refX = (int)Math.Round(refXFloat);

            // Draw reference line
            using (Pen refPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                g.SetClip(curveArea);
                g.DrawLine(refPen, refXFloat, curveArea.Top, refXFloat, curveArea.Bottom);
                g.ResetClip();
            }

            // Reference label
            string refLabel = manualZRefs.ContainsKey(curveIndex)
                ? $"Ref Z: {zRef:F2} mm (manual)"
                : $"Ref Z: {zRef:F2} mm (auto)";
            g.DrawString(refLabel, labelFont, Brushes.Red, Math.Min(refXFloat + 5, curveArea.Right - 100), curveArea.Top + 5);

            // Draw max point marker
            if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < pts.Count)
            {
                var maxPt = pts[curveData.MaxZIndex];
                float maxXpix = offsetX + maxPt.X * scaleX;
                float maxYpix = curveArea.Top + maxPt.Y;
                g.FillEllipse(Brushes.Red, maxXpix - 4, maxYpix - 4, 8, 8);
            }

            // Draw selected point and distance
            if (selectedPoint.HasValue && activeCurveIndex.HasValue && activeCurveIndex.Value == curveIndex)
            {
                var sp = selectedPoint.Value;
                float spXpix = offsetX + sp.X * scaleX;
                float spYpix = curveArea.Top + sp.Y;

                g.FillEllipse(Brushes.Yellow, spXpix - 4, spYpix - 4, 8, 8);

                float lateralDistance = Math.Abs(sp.X - zRef);
                string label = $"Z: {sp.X:F2} mm  Δ: {lateralDistance:F2} mm";

                float labelX = Math.Min(spXpix + 8, curveArea.Right - 120);
                float labelY = Math.Max(spYpix - 30, curveArea.Top + 5);
                g.DrawString(label, labelFont, Brushes.Yellow, labelX, labelY);
            }

            g.DrawRectangle(Pens.Gray, curveArea);
        }

        private void DrawControlHints(Graphics g)
        {
            string hint = activeCurveIndex.HasValue
                ? "Selected curve: Use ← → keys (0.5mm) or Shift+← → (5mm) to adjust ref line | R: Reset | Right-click: Deselect"
                : "Click curve to select | Drag red line to adjust reference";

            using (Brush hintBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.DrawString(hint, labelFont, hintBrush, 10, ClientSize.Height - 35);
            }
        }

        private void EnableInteractiveFeatures()
        {
            this.MouseDown += MultiCurveViewer_MouseDown;
            this.MouseMove += MultiCurveViewer_MouseMove;
            this.MouseUp += MultiCurveViewer_MouseUp;
            this.MouseClick += MultiCurveViewer_MouseClick;
        }

        private void MultiCurveViewer_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));

            for (int i = 0; i < allCurves.Count; i++)
            {
                int row = i / curvesPerRow;
                int col = i % curvesPerRow;

                int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
                int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;
                Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

                if (e.X >= curveArea.Left && e.X <= curveArea.Right &&
                    e.Y >= curveArea.Top && e.Y <= curveArea.Bottom)
                {
                    activeCurveIndex = i;

                    // Get current Z ref value
                    var pts = allCurves[i].Points.ConvertAll(p => p.ToPointF());
                    float autoZRef = pts.Any() ? pts[allCurves[i].MaxZIndex].X : 0f;
                    float currentZRef = manualZRefs.ContainsKey(i) ? manualZRefs[i] : autoZRef;

                    // Convert to pixel position
                    float offsetX = curveArea.Left + OffsetXInsideBox;
                    float refPixelX = offsetX + currentZRef * ScaleX;

                    // Check if clicking near reference line
                    if (Math.Abs(e.X - refPixelX) <= 8)
                    {
                        isDraggingRefLine = true;
                        this.Cursor = Cursors.SizeWE;
                    }
                    else
                    {
                        SelectNearestPointInCurve(i, new Point(e.X, e.Y));
                    }

                    Invalidate();
                    return;
                }
            }

            activeCurveIndex = null;
            selectedPoint = null;
            selectedPointIndex = null;
            Invalidate();
        }

        private void MultiCurveViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingRefLine && activeCurveIndex.HasValue)
            {
                int i = activeCurveIndex.Value;

                int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));
                int row = i / curvesPerRow;
                int col = i % curvesPerRow;
                int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
                int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;
                Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

                float offsetX = curveArea.Left + OffsetXInsideBox;

                // Clamp mouse X to curve area
                int clampedX = Math.Max(curveArea.Left + 1, Math.Min(curveArea.Right - 1, e.X));

                // Convert pixel position to Z value (mm) - this maintains precision
                float newZ = (clampedX - offsetX) / ScaleX;

                // Store the Z value directly (not the pixel position)
                manualZRefs[i] = newZ;

                Invalidate();
                return;
            }

            UpdateHoveredPoint(new Point(e.X, e.Y));
        }

        private void MultiCurveViewer_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingRefLine)
            {
                isDraggingRefLine = false;
                this.Cursor = Cursors.Default;
                Invalidate();
            }
        }

        private void MultiCurveViewer_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                selectedPoint = null;
                selectedPointIndex = null;
                activeCurveIndex = null;
                Invalidate();
            }
        }

        private void UpdateHoveredPoint(Point mouseLocation)
        {
            if (isDraggingRefLine) return;

            int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));
            bool foundAny = false;

            for (int i = 0; i < allCurves.Count; i++)
            {
                int row = i / curvesPerRow;
                int col = i % curvesPerRow;
                int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
                int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;
                Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

                if (mouseLocation.X < curveArea.Left || mouseLocation.X > curveArea.Right ||
                    mouseLocation.Y < curveArea.Top || mouseLocation.Y > curveArea.Bottom)
                {
                    continue;
                }

                var pts = allCurves[i].Points.ConvertAll(p => p.ToPointF());
                float offsetX = curveArea.Left + OffsetXInsideBox;

                float minDistance = 15f;
                PointF? bestPt = null;
                int bestIdx = -1;

                for (int k = 0; k < pts.Count; k++)
                {
                    var pt = pts[k];
                    float xpix = offsetX + pt.X * ScaleX;
                    float ypix = curveArea.Top + pt.Y;
                    float dx = mouseLocation.X - xpix;
                    float dy = mouseLocation.Y - ypix;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestPt = pt;
                        bestIdx = k;
                    }
                }

                if (bestPt.HasValue)
                {
                    selectedPoint = bestPt;
                    selectedPointIndex = bestIdx;
                    activeCurveIndex = i;
                    foundAny = true;
                    Invalidate();
                    break;
                }
            }

            if (!foundAny)
            {
                selectedPoint = null;
                selectedPointIndex = null;
                Invalidate();
            }
        }

        private void SelectNearestPointInCurve(int curveIndex, Point mouseLocation)
        {
            if (curveIndex < 0 || curveIndex >= allCurves.Count) return;
            var pts = allCurves[curveIndex].Points.ConvertAll(p => p.ToPointF());

            int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));
            int row = curveIndex / curvesPerRow;
            int col = curveIndex % curvesPerRow;
            int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
            int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;
            Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);
            float offsetX = curveArea.Left + OffsetXInsideBox;

            float minDistance = 20f;
            PointF? best = null;
            int bestIdx = -1;
            for (int k = 0; k < pts.Count; k++)
            {
                var pt = pts[k];
                float xpix = offsetX + pt.X * ScaleX;
                float ypix = curveArea.Top + pt.Y;
                float dx = mouseLocation.X - xpix;
                float dy = mouseLocation.Y - ypix;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    best = pt;
                    bestIdx = k;
                }
            }

            if (best.HasValue)
            {
                selectedPoint = best;
                selectedPointIndex = bestIdx;
                activeCurveIndex = curveIndex;
            }
            else
            {
                selectedPoint = null;
                selectedPointIndex = null;
            }

            Invalidate();
        }

        private void DrawScrollIndicators(Graphics g)
        {
            if (maxHorizontalOffset > 0)
            {
                int scrollWidth = ClientSize.Width - 80;
                int scrollX = 40;
                int scrollY = ClientSize.Height - 20;

                g.FillRectangle(Brushes.DarkGray, scrollX, scrollY, scrollWidth, 10);
                float thumbPos = horizontalOffset / (float)Math.Max(1, maxHorizontalOffset);
                int thumbWidth = Math.Max(30, (int)(scrollWidth * (ClientSize.Width / (float)(maxHorizontalOffset + ClientSize.Width))));
                int thumbX = scrollX + (int)(thumbPos * (scrollWidth - thumbWidth));
                g.FillRectangle(Brushes.LightGray, thumbX, scrollY, thumbWidth, 10);
            }

            if (maxVerticalOffset > 0)
            {
                int scrollHeight = ClientSize.Height - 80;
                int scrollX = ClientSize.Width - 20;
                int scrollY = 40;

                g.FillRectangle(Brushes.DarkGray, scrollX, scrollY, 10, scrollHeight);
                float thumbPos = verticalOffset / (float)Math.Max(1, maxVerticalOffset);
                int thumbHeight = Math.Max(30, (int)(scrollHeight * (ClientSize.Height / (float)(maxVerticalOffset + ClientSize.Height))));
                int thumbY = scrollY + (int)(thumbPos * (scrollHeight - thumbHeight));
                g.FillRectangle(Brushes.LightGray, scrollX, thumbY, 10, thumbHeight);
            }
        }

        private void MultiCurveViewer_MouseWheel(object sender, MouseEventArgs e)
        {
            verticalOffset -= e.Delta / 5;
            verticalOffset = Math.Max(0, Math.Min(verticalOffset, maxVerticalOffset));
            Invalidate();
        }

        private void MultiCurveViewer_KeyDown(object sender, KeyEventArgs e)
        {
            // Reference line adjustment for active curve
            if (activeCurveIndex.HasValue && activeCurveIndex.Value >= 0 && activeCurveIndex.Value < allCurves.Count)
            {
                var pts = allCurves[activeCurveIndex.Value].Points.ConvertAll(p => p.ToPointF());
                float autoZRef = pts.Any() ? pts[allCurves[activeCurveIndex.Value].MaxZIndex].X : 0f;
                float currentZRef = manualZRefs.ContainsKey(activeCurveIndex.Value)
                    ? manualZRefs[activeCurveIndex.Value]
                    : autoZRef;

                float step = e.Shift ? FineControlStep * 10 : FineControlStep; // Shift = 5mm, normal = 0.5mm

                switch (e.KeyCode)
                {
                    case Keys.Left:
                        manualZRefs[activeCurveIndex.Value] = currentZRef - step;
                        Invalidate();
                        e.Handled = true;
                        return;

                    case Keys.Right:
                        manualZRefs[activeCurveIndex.Value] = currentZRef + step;
                        Invalidate();
                        e.Handled = true;
                        return;

                    case Keys.R:
                        // Reset to automatic reference
                        if (manualZRefs.ContainsKey(activeCurveIndex.Value))
                        {
                            manualZRefs.Remove(activeCurveIndex.Value);
                            Invalidate();
                        }
                        e.Handled = true;
                        return;
                }
            }

            // Scrolling
            switch (e.KeyCode)
            {
                case Keys.Up:
                    verticalOffset = Math.Max(0, verticalOffset - ScrollStep);
                    break;
                case Keys.Down:
                    verticalOffset = Math.Min(maxVerticalOffset, verticalOffset + ScrollStep);
                    break;
                case Keys.PageUp:
                    verticalOffset = Math.Max(0, verticalOffset - ClientSize.Height / 2);
                    break;
                case Keys.PageDown:
                    verticalOffset = Math.Min(maxVerticalOffset, verticalOffset + ClientSize.Height / 2);
                    break;
                case Keys.Home:
                    verticalOffset = 0;
                    horizontalOffset = 0;
                    break;
                case Keys.End:
                    verticalOffset = maxVerticalOffset;
                    horizontalOffset = maxHorizontalOffset;
                    break;
                case Keys.Escape:
                    this.Close();
                    return;
            }

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            int scrollWidth = 10;
            int scrollX = ClientSize.Width - scrollWidth - 5;
            if (e.X >= scrollX && e.X <= scrollX + scrollWidth)
            {
                ScrollToPosition(e.Y);
            }
        }

        private void ScrollToPosition(int mouseY)
        {
            int scrollHeight = ClientSize.Height - 40;
            int scrollY = 20;

            if (mouseY >= scrollY && mouseY <= scrollY + scrollHeight)
            {
                float relative = (mouseY - scrollY) / (float)scrollHeight;
                verticalOffset = (int)(relative * maxVerticalOffset);
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                infoFont?.Dispose();
                labelFont?.Dispose();
                axisPen?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(900, 700);
            this.Name = "MultiCurveViewer";
            this.Text = "Multi-Curve Viewer";
            this.ResumeLayout(false);
        }
    }

    public class SimplePoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public PointF ToPointF() => new PointF(X, Y);
    }
}