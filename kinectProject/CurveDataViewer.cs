using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KinectProject;

namespace KinectProject
{
    public partial class CurveDataViewer : Form
    {
        private SpineCurveData curveData;
        private List<System.Drawing.PointF> points;

        // Cache fonts as class fields
        private Font infoFont;
        private Font labelFont;

        public CurveDataViewer()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(45, 45, 60);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f);
            this.Padding = new Padding(10);

            infoFont = new Font("Segoe UI", 10, FontStyle.Regular);
            labelFont = new Font("Segoe UI", 8, FontStyle.Regular);
        }

        public void LoadCurveData(SpineCurveData data)
        {
            curveData = data;
            points = data.Points.ConvertAll(p => p.ToPointF());

            // Update UI
            Text = $"Données Courbe - {data.CaptureTime:yyyy-MM-dd HH:mm:ss}";
            Invalidate(); // Redraw
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (curveData == null || points == null || points.Count == 0)
                return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 20, 30));

            // Draw info - use cached fonts
            string info = $"Points: {points.Count} | Angle: {curveData.SpineAngle:F1}° | " +
                         $"Capture: {curveData.CaptureTime:yyyy-MM-dd HH:mm:ss}";
            g.DrawString(info, infoFont, Brushes.LightGray, 10, 10);

            if (points.Count < 2)
                return;

            // USE THE SAME SCALING AS YOUR MAIN FORM
            float offsetX = 50f;  // Same as main form
            float scaleX = 0.1f;  // Same as main form

            // Draw curve with ORIGINAL scaling factors
            using (Pen curvePen = new Pen(Color.Cyan, 2))
            {
                for (int i = 1; i < points.Count; i++)
                {
                    // REPLICATE THE EXACT SAME TRANSFORMATION AS MAIN FORM
                    float x1 = offsetX + points[i - 1].X * scaleX;
                    float y1 = points[i - 1].Y;
                    float x2 = offsetX + points[i].X * scaleX;
                    float y2 = points[i].Y;

                    g.DrawLine(curvePen, x1, y1, x2, y2);
                }
            }

            // Draw max point with ORIGINAL scaling
            if (curveData.MaxZIndex >= 0 && curveData.MaxZIndex < points.Count)
            {
                var maxPoint = points[curveData.MaxZIndex];
                float x = offsetX + maxPoint.X * scaleX;
                float y = maxPoint.Y;

                g.FillEllipse(Brushes.Red, x - 4, y - 4, 8, 8);
                g.DrawString("Point Max", labelFont, Brushes.Red, x + 10, y - 10);
            }

            // Draw reference line with ORIGINAL scaling
            if (curveData.ManualZRef > 0)
            {
                float xRef = offsetX + curveData.ManualZRef * scaleX;
                using (Pen refPen = new Pen(Color.Red, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(refPen, xRef, 0, xRef, ClientSize.Height);
                }
            }

            // Draw fixed deepest pixel line if available
            if (curveData.FixedDeepestXPixel > 0)
            {
                using (Pen refPen = new Pen(Color.Orange, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    g.DrawLine(refPen, curveData.FixedDeepestXPixel, 0, curveData.FixedDeepestXPixel, ClientSize.Height);
                }
                g.DrawString($"Ref Line: {curveData.FixedDeepestXPixel:F0}", labelFont, Brushes.Orange, curveData.FixedDeepestXPixel + 5, 30);
            }
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate(); // Redraw when resized
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // CurveDataViewer
            // 
            this.ClientSize = new System.Drawing.Size(600, 400);
            this.Name = "CurveDataViewer";
            this.Text = "Visualisateur Données Courbe";
            this.Load += new System.EventHandler(this.CurveDataViewer_Load);
            this.ResumeLayout(false);

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose fonts when the form is disposed
                infoFont?.Dispose();
                labelFont?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void CurveDataViewer_Load(object sender, EventArgs e)
        {

        }
    }



}