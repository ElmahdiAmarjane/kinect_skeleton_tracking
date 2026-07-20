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
        public bool IsAvailable => kinectSensor != null && kinectSensor.IsOpen;

        public event EventHandler<MultiSourceFrameArrivedEventArgs> FrameArrived;
        public event EventHandler<bool> ConnectionStatusChanged;

        private bool isInitializing = true;
        private bool wasAvailable = false;
        private bool connectionNotified = false; // ✅ Track if we already notified disconnect
        private DateTime lastFrameReceived = DateTime.MinValue;
        private DateTime lastRestartAttempt = DateTime.MinValue; // ✅ Prevent rapid restarts
        private const int FrameTimeoutMs = 5000; // ✅ Shorter timeout
        private const int RestartCooldownMs = 10000; // ✅ 10s between restart attempts
        private const int MaxRestartAttempts = 3; // ✅ Limit restart attempts

        private System.Windows.Forms.Timer watchdogTimer;
        private const int WatchdogIntervalMs = 2000;
        private bool firstFrameReceived = false;
        private int restartAttempts = 0; // ✅ Count restart attempts

        public bool Initialize()
        {
            kinectSensor = KinectSensor.GetDefault();
            if (kinectSensor == null)
            {
                return false;
            }

            isInitializing = true;

            try
            {
                kinectSensor.Open();

                if (!kinectSensor.IsOpen)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            coordinateMapper = kinectSensor.CoordinateMapper;

            multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);

            multiSourceFrameReader.MultiSourceFrameArrived += (s, e) =>
            {
                lastFrameReceived = DateTime.Now;
                restartAttempts = 0; // ✅ Reset attempts when frames arrive

                if (!firstFrameReceived)
                {
                    firstFrameReceived = true;
                    isInitializing = false;
                    NotifyConnected();
                }

                FrameArrived?.Invoke(s, e);
            };

            watchdogTimer = new System.Windows.Forms.Timer();
            watchdogTimer.Interval = WatchdogIntervalMs;
            watchdogTimer.Tick += WatchdogTimer_Tick;
            watchdogTimer.Start();

            lastFrameReceived = DateTime.Now;

            return true;
        }

        private void NotifyConnected()
        {
            if (!wasAvailable || !connectionNotified)
            {
                wasAvailable = true;
                connectionNotified = true;
                ConnectionStatusChanged?.Invoke(this, true);
            }
        }

        private void NotifyDisconnected()
        {
            if (wasAvailable || connectionNotified)
            {
                wasAvailable = false;
                connectionNotified = true;
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        private void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            if (isInitializing) return;

            double timeSinceLastFrame = (DateTime.Now - lastFrameReceived).TotalMilliseconds;
            double timeSinceLastRestart = (DateTime.Now - lastRestartAttempt).TotalMilliseconds;

            // ✅ Check if sensor is still physically connected
            bool sensorPhysicallyPresent = kinectSensor != null && kinectSensor.IsOpen;

            if (!sensorPhysicallyPresent)
            {
                // Sensor is gone - notify and stop trying
                NotifyDisconnected();
                return;
            }

            if (timeSinceLastFrame > FrameTimeoutMs)
            {
                NotifyDisconnected();

                // ✅ Only restart if we haven't exceeded max attempts and cooldown passed
                if (restartAttempts < MaxRestartAttempts && timeSinceLastRestart > RestartCooldownMs)
                {
                    lastRestartAttempt = DateTime.Now;
                    restartAttempts++;
                    RestartFrameReader();
                }
                // If max attempts reached, just keep showing disconnected
            }
            else if (firstFrameReceived)
            {
                NotifyConnected();
            }
        }

        private void RestartFrameReader()
        {
            try
            {
                // ✅ First check if sensor is still available
                if (kinectSensor == null || !kinectSensor.IsOpen || !kinectSensor.IsAvailable)
                {
                    NotifyDisconnected();
                    return;
                }

                if (multiSourceFrameReader != null)
                {
                    multiSourceFrameReader.Dispose();
                    multiSourceFrameReader = null;
                }

                multiSourceFrameReader = kinectSensor.OpenMultiSourceFrameReader(
                    FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.Color);

                multiSourceFrameReader.MultiSourceFrameArrived += (s, e) =>
                {
                    lastFrameReceived = DateTime.Now;
                    restartAttempts = 0;

                    if (!firstFrameReceived)
                    {
                        firstFrameReceived = true;
                        isInitializing = false;
                        NotifyConnected();
                    }

                    FrameArrived?.Invoke(s, e);
                };

                lastFrameReceived = DateTime.Now;
            }
            catch (Exception)
            {
                // Sensor not available - don't retry
                NotifyDisconnected();
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
            if (watchdogTimer != null)
            {
                watchdogTimer.Stop();
                watchdogTimer.Dispose();
                watchdogTimer = null;
            }

            if (multiSourceFrameReader != null)
            {
                multiSourceFrameReader.Dispose();
                multiSourceFrameReader = null;
            }

            if (kinectSensor != null)
            {
                if (kinectSensor.IsOpen)
                    kinectSensor.Close();
                kinectSensor = null;
            }
        }
    }
}