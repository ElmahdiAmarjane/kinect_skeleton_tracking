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
        private int curveHeight = 300; // Estimated height for each curve box
        private int curveWidth = 400;  // Width for each curve box

        private int horizontalSpacing = 20;
        private int verticalSpacing = 20;

        private int horizontalOffset = 0;
        private int verticalOffset = 0;
        private int maxHorizontalOffset = 0;
        private int maxVerticalOffset = 0;
        private const int ScrollStep = 50;

        // Interaction state
        private int? activeCurveIndex = null;           // curve index being interacted with (dragging or selected)
        private bool isDraggingRefLine = false;         // dragging vertical red reference line
        private PointF? selectedPoint = null;           // selected/nearest point on currently hovered curve
        private int? selectedPointIndex = null;

        // Per-curve manual reference and pixel positions
        private Dictionary<int, float> manualZRefs = new Dictionary<int, float>(); // Z value in mm
        private Dictionary<int, int> fixedXPixel = new Dictionary<int, int>();     // pixel X position of ref line

        // Fonts / pens
        private Font infoFont;
        private Font labelFont;
        private Pen axisPen = new Pen(Color.Gray, 1);

        // Scaling constants borrowed from working CurveDataViewer behaviour
        // Important: We use a fixed horizontal scale and DO NOT scale Y (to avoid distortion)
        private const float OffsetXInsideBox = 50f; // same idea as CurveDataViewer offset
        private const float ScaleX = 0.1f;          // same horizontal scale as working viewer (px per mm)

        public MultiCurveViewer()
        {
            InitializeComponent();
            InitializeComponents();
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 60);
            this.ForeColor = Color.White;
            this.KeyPreview = true;

            // Resize handling
            this.ResizeBegin += (s, e) => { this.SuspendLayout(); };
            this.ResizeEnd += (s, e) => { this.ResumeLayout(true); this.Invalidate(); };

            // Enable interactions
            EnableInteractiveFeatures();
        }

        private void InitializeComponents()
        {
            infoFont = new Font("Segoe UI", 9, FontStyle.Regular);
            labelFont = new Font("Segoe UI", 8, FontStyle.Regular);

            this.ClientSize = new Size(900, 700);
            this.Text = "Multi-Curve Viewer - Corrected Scaling + Interactive Ref Line";
            this.Padding = new Padding(10);

            this.MouseWheel += MultiCurveViewer_MouseWheel;
            this.KeyDown += MultiCurveViewer_KeyDown;
        }

        public void LoadCurves(List<SpineCurveData> curves)
        {
            allCurves = curves ?? new List<SpineCurveData>();
            // Reset per-curve refs
            manualZRefs.Clear();
            fixedXPixel.Clear();
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

                // Only draw if visible
                if (xBase + curveWidth < 0 || xBase > ClientSize.Width ||
                    yBase + curveHeight < 0 || yBase > ClientSize.Height) continue;

                DrawCurveBox(g, curveData, i, xBase, yBase);
            }

            // Draw scroll indicators if necessary
            if (maxHorizontalOffset > 0 || maxVerticalOffset > 0)
            {
                DrawScrollIndicators(g);
            }
        }

        private void DrawCurveBox(Graphics g, SpineCurveData curveData, int curveIndex, int xBase, int yBase)
        {
            // Drawing rectangle for this curve
            Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

            // Background and border
            g.FillRectangle(Brushes.Black, curveArea);
            g.DrawRectangle(Pens.Gray, curveArea);

            // Info title
            string info = $"[{curveIndex + 1}] {curveData.CaptureTime:yyyy-MM-dd HH:mm:ss}  Angle: {curveData.SpineAngle:F1}°";
            g.DrawString(info, infoFont, Brushes.LightGray, xBase + 10, yBase + 5);

            // Prepare points (convert)
            var pts = curveData.Points.ConvertAll(p => p.ToPointF());
            if (pts.Count < 2) return;

            // We'll use a fixed horizontal scaling and keep Y as-is (to match CurveDataViewer)
            float offsetX = curveArea.Left + OffsetXInsideBox; // provide 50px margin inside the box
            float scaleX = ScaleX;

            // Draw vertical/horizontal axis lines (center-ish)
            float axisX = offsetX - 10; // small axis line to the left of curve
            g.DrawLine(axisPen, axisX, curveArea.Top, axisX, curveArea.Bottom);
            g.DrawLine(axisPen, curveArea.Left, curveArea.Bottom - 1, curveArea.Right, curveArea.Bottom - 1);

            // Draw curve lines using original (good) scaling approach
            Color[] palette = new Color[] { Color.Cyan, Color.LightGreen, Color.Orange, Color.Violet, Color.Yellow, Color.Pink, Color.LightBlue };
            using (Pen curvePen = new Pen(palette[curveIndex % palette.Length], 2))
            {
                for (int k = 1; k < pts.Count; k++)
                {
                    if (float.IsNaN(pts[k - 1].X) || float.IsNaN(pts[k - 1].Y)
                        || float.IsNaN(pts[k].X) || float.IsNaN(pts[k].Y)) continue;

                    float x1 = offsetX + pts[k - 1].X * scaleX;
                    float y1 = curveArea.Top + pts[k - 1].Y; // keep Y as originally (like CurveDataViewer)
                    float x2 = offsetX + pts[k].X * scaleX;
                    float y2 = curveArea.Top + pts[k].Y;

                    // Clip drawing to curveArea
                    g.SetClip(curveArea);
                    try
                    {
                        g.DrawLine(curvePen, x1, y1, x2, y2);
                    }
                    catch (ArgumentException) { /* ignore invalid lines */ }
                    g.ResetClip();
                }
            }

            // Determine reference Z (either manual or automatic - max Z)
            float autoZRef = pts.Any() ? pts[curveData.MaxZIndex].X : 0f;
            float zRef = manualZRefs.ContainsKey(curveIndex) && manualZRefs[curveIndex] > 0 ? manualZRefs[curveIndex] : autoZRef;

            // Determine pixel X for reference line (store if not present)
            if (!fixedXPixel.ContainsKey(curveIndex))
            {
                int pixelX = (int)(offsetX + zRef * scaleX);
                fixedXPixel[curveIndex] = pixelX;
            }

            // If a manualZRef exists and is >0, use it to compute pixel
            if (manualZRefs.ContainsKey(curveIndex) && manualZRefs[curveIndex] > 0)
            {
                fixedXPixel[curveIndex] = (int)(offsetX + manualZRefs[curveIndex] * scaleX);
            }
            else
            {
                // otherwise use auto (max)
                fixedXPixel[curveIndex] = (int)(offsetX + zRef * scaleX);
            }

            // Draw dashed red reference line inside the box
            int refX = fixedXPixel[curveIndex];
            using (Pen refPen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                // Clip and draw only inside curve area
                g.SetClip(curveArea);
                g.DrawLine(refPen, refX, curveArea.Top, refX, curveArea.Bottom);
                g.ResetClip();
            }
            g.DrawString($"Ref Z: {zRef:F1} mm", labelFont, Brushes.Red, Math.Min(refX + 5, curveArea.Right - 60), curveArea.Top + 5);

            // Draw max point marker (from curveData.MaxZIndex)
            if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < pts.Count)
            {
                var maxPt = pts[curveData.MaxZIndex];
                float maxXpix = offsetX + maxPt.X * scaleX;
                float maxYpix = curveArea.Top + maxPt.Y;
                g.FillEllipse(Brushes.Red, maxXpix - 4, maxYpix - 4, 8, 8);
            }

            // If there's a selected point belonging to this curve, draw it and the lateral distance
            if (selectedPoint.HasValue && activeCurveIndex.HasValue && activeCurveIndex.Value == curveIndex)
            {
                var sp = selectedPoint.Value;
                float spXpix = offsetX + sp.X * scaleX;
                float spYpix = curveArea.Top + sp.Y;

                // draw marker
                g.FillEllipse(Brushes.Yellow, spXpix - 4, spYpix - 4, 8, 8);

                // compute lateral distance in mm between the selected point and zRef
                float lateralDistance = Math.Abs(sp.X - zRef);

                string label = $"Z: {sp.X:F1} mm  Δ: {lateralDistance:F1} mm";
                // draw label near the point, keep inside box
                float labelX = Math.Min(spXpix + 8, curveArea.Right - 90);
                float labelY = Math.Max(spYpix - 30, curveArea.Top + 5);
                g.DrawString(label, labelFont, Brushes.Yellow, labelX, labelY);
            }

            // Draw box border again to ensure markers are visible
            g.DrawRectangle(Pens.Gray, curveArea);
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

            // Check if click is within any curve area, and if near its ref line then start dragging
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
                    // click inside this curve area
                    activeCurveIndex = i;

                    // compute offsetX for this box to compare with fixedXPixel
                    float offsetX = curveArea.Left + OffsetXInsideBox;
                    int refPixelX = fixedXPixel.ContainsKey(i) ? fixedXPixel[i] : (int)(offsetX + allCurves[i].Points[allCurves[i].MaxZIndex].X * ScaleX);

                    // If click is close to the ref line (within 8 px), start dragging
                    if (Math.Abs(e.X - refPixelX) <= 8)
                    {
                        isDraggingRefLine = true;
                        this.Cursor = Cursors.SizeWE;
                    }
                    else
                    {
                        // Not clicking the ref line: select nearest point to the click
                        SelectNearestPointInCurve(i, new Point(e.X, e.Y));
                    }

                    return; // handled
                }
            }

            // click outside any curve
            activeCurveIndex = null;
            selectedPoint = null;
            selectedPointIndex = null;
            Invalidate();
        }

        private void MultiCurveViewer_MouseMove(object sender, MouseEventArgs e)
        {
            // If dragging the reference line for an active curve
            if (isDraggingRefLine && activeCurveIndex.HasValue)
            {
                int i = activeCurveIndex.Value;

                // compute the curve area for this index
                int curvesPerRow = Math.Max(1, (ClientSize.Width - 40) / (curveWidth + horizontalSpacing));
                int row = i / curvesPerRow;
                int col = i % curvesPerRow;
                int xBase = 20 + col * (curveWidth + horizontalSpacing) - horizontalOffset;
                int yBase = 20 + row * (curveHeight + verticalSpacing) - verticalOffset;
                Rectangle curveArea = new Rectangle(xBase + 5, yBase + 25, curveWidth - 10, curveHeight - 35);

                float offsetX = curveArea.Left + OffsetXInsideBox;

                // Clamp the mouse X to be inside the curve area
                int clampedX = Math.Max(curveArea.Left + 1, Math.Min(curveArea.Right - 1, e.X));
                fixedXPixel[i] = clampedX;

                // Convert back to Z value (mm)
                float newZ = (clampedX - offsetX) / ScaleX;
                manualZRefs[i] = newZ;

                // update selection distance if any
                if (selectedPoint.HasValue && activeCurveIndex == i)
                {
                    // nothing special: Draw will compute lateral distance
                }

                Invalidate();
                return;
            }

            // If not dragging, update hovered/closest point for visual feedback (only within curve area)
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
            // Left-click handled in MouseDown for selection and drag start.
            // Middle or right-click: clear selection
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
            // Find which curve area mouse is over and compute nearest point (within threshold)
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

                // Mouse is inside this curve box: search nearest point
                var pts = allCurves[i].Points.ConvertAll(p => p.ToPointF());
                float offsetX = curveArea.Left + OffsetXInsideBox;

                float minDistance = 15f; // pixel threshold
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
                // Clear selection feedback if not hovering close to any point
                // (But we keep manual ref line state)
                // Do not clear selectedPoint if user is dragging refline
                if (!isDraggingRefLine)
                {
                    selectedPoint = null;
                    selectedPointIndex = null;
                    // activeCurveIndex = null; // keep activeCurveIndex to preserve which curve ref being manipulated
                    Invalidate();
                }
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
            // Horizontal
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

            // Vertical
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
            switch (e.KeyCode)
            {
                case Keys.Up:
                    verticalOffset = Math.Max(0, verticalOffset - ScrollStep);
                    break;
                case Keys.Down:
                    verticalOffset = Math.Min(maxVerticalOffset, verticalOffset + ScrollStep);
                    break;
                case Keys.Left:
                    horizontalOffset = Math.Max(0, horizontalOffset - ScrollStep);
                    break;
                case Keys.Right:
                    horizontalOffset = Math.Min(maxHorizontalOffset, horizontalOffset + ScrollStep);
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

            // Click on scrollbars handling (optional)
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

    // Dummy SpineCurveData for compilation example:
    // In your project you already have this class; remove this one if duplicate.
    
    // SimplePoint used as placeholder for your actual point class.
    public class SimplePoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public PointF ToPointF() => new PointF(X, Y);
    }
}
