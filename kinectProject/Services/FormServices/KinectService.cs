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

        public bool Initialize()
        {
            kinectSensor = KinectSensor.GetDefault();
            if (kinectSensor == null)
            {
                MessageBox.Show("Aucun capteur Kinect détecté.", "Erreur Kinect",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            kinectSensor.Open();
            coordinateMapper = kinectSensor.CoordinateMapper;

            multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);

            multiSourceFrameReader.MultiSourceFrameArrived += (s, e) =>
            {
                FrameArrived?.Invoke(s, e);
            };

            kinectSensor.IsAvailableChanged += KinectSensor_IsAvailableChanged;

            return true;
        }

        private void KinectSensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            if (!e.IsAvailable)
            {
                MessageBox.Show("Connexion perdue avec le capteur Kinect.", "Alerte",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                kinectSensor.Close();
                kinectSensor = null;
            }
        }
    }
}