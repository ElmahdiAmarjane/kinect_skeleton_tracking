using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectProject
{
  
        [Serializable]
        public class SpineCurveData
        {
            public DateTime CaptureTime { get; set; }
            public List<PointFData> Points { get; set; }
            public int MaxZIndex { get; set; } = -1;
            public float ManualZRef { get; set; } = -1;
            public float FixedDeepestXPixel { get; set; } = -1;
            public double SpineAngle { get; set; }
            public string PatientIdentifier { get; set; } = "Unknown";

        public float OriginalOffsetX { get; set; } = 50f;
        public float OriginalScaleX { get; set; } = 0.1f;

        // Add file path for reference
        [JsonIgnore]
            public string FilePath { get; set; }

            public SpineCurveData()
            {
                Points = new List<PointFData>();
                CaptureTime = DateTime.Now;
            }
        }

        [Serializable]
        public class PointFData
        {
            public float X { get; set; }
            public float Y { get; set; }

            public PointFData() { }

            public PointFData(float x, float y)
            {
                X = x;
                Y = y;
            }

            public static PointFData FromPointF(PointF point)
            {
                return new PointFData(point.X, point.Y);
            }

            public PointF ToPointF()
            {
                return new PointF(X, Y);
            }
        }

    }

