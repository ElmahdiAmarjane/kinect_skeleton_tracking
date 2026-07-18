using System;
using System.Collections.Generic;
using System.Drawing;

namespace kinectProject
{
    public class ConnectedComponent
    {
        public List<Point> Pixels = new List<Point>();
        public int MinX = int.MaxValue, MinY = int.MaxValue;
        public int MaxX = int.MinValue, MaxY = int.MinValue;

        public int PixelCount => Pixels.Count;
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;

        public PointF GeometricCenter =>
            new PointF((MinX + MaxX) / 2f, (MinY + MaxY) / 2f);

        public void Add(int x, int y)
        {
            Pixels.Add(new Point(x, y));
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
        }
    }
}