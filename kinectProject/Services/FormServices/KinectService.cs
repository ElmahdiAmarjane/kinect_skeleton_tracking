using Microsoft.Kinect;
using System;
using System.Windows.Forms;

namespace kinectProject
{
    public class KinectService
    {
        private KinectSensor kinectSensor;
        private MultiSourceFrameReader multiSourceFrameReader;
        private CoordinateMapper coordinateMapper;

        public KinectSensor Sensor => kinectSensor;
        public CoordinateMapper CoordinateMapper => coordinateMapper;
        public bool IsAvailable => kinectSensor != null && kinectSensor.IsAvailable;

        public event EventHandler<MultiSourceFrameArrivedEventArgs> FrameArrived;
        public event EventHandler<bool> ConnectionStatusChanged; // ✅ New event for status

        private bool isInitializing = true;
        private bool wasAvailable = false;
        private DateTime lastStatusChange = DateTime.MinValue;
        private const int StatusCooldownMs = 2000; // 2 seconds cooldown between status changes

        public bool Initialize()
        {
            kinectSensor = KinectSensor.GetDefault();
            if (kinectSensor == null)
            {
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            isInitializing = true;
            kinectSensor.Open();
            coordinateMapper = kinectSensor.CoordinateMapper;

            multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);

            multiSourceFrameReader.MultiSourceFrameArrived += (s, e) =>
            {
                FrameArrived?.Invoke(s, e);
            };

            kinectSensor.IsAvailableChanged += KinectSensor_IsAvailableChanged;

            wasAvailable = kinectSensor.IsAvailable;
            isInitializing = false;

            ConnectionStatusChanged?.Invoke(this, true);

            return true;
        }

        private void KinectSensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // Ignore events during initialization
            if (isInitializing) return;

            // Cooldown to prevent rapid status changes
            if ((DateTime.Now - lastStatusChange).TotalMilliseconds < StatusCooldownMs)
                return;

            lastStatusChange = DateTime.Now;

            // Only notify if status actually changed
            if (e.IsAvailable != wasAvailable)
            {
                wasAvailable = e.IsAvailable;
                ConnectionStatusChanged?.Invoke(this, e.IsAvailable);
            }
        }

        public MultiSourceFrame AcquireLatestFrame()
        {
            return multiSourceFrameReader?.AcquireLatestFrame();
        }

        public CameraSpacePoint MapDepthToCameraSpace(int depthX, int depthY, ushort depthValue)
        {
            DepthSpacePoint depthPoint = new DepthSpacePoint { X = depthX, Y = depthY };
            return coordinateMapper.MapDepthPointToCameraSpace(depthPoint, depthValue);
        }

        public DepthSpacePoint MapCameraToDepthSpace(CameraSpacePoint cameraPoint)
        {
            return coordinateMapper.MapCameraPointToDepthSpace(cameraPoint);
        }

        public void MapDepthFrameToColorSpace(ushort[] depthData, ColorSpacePoint[] colorPoints)
        {
            coordinateMapper.MapDepthFrameToColorSpace(depthData, colorPoints);
        }

        public void Shutdown()
        {
            if (multiSourceFrameReader != null)
            {
                multiSourceFrameReader.Dispose();
                multiSourceFrameReader = null;
            }

            if (kinectSensor != null)
            {
                kinectSensor.IsAvailableChanged -= KinectSensor_IsAvailableChanged;
                kinectSensor.Close();
                kinectSensor = null;
            }
        }
    }
}