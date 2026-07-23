using Microsoft.Kinect;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace kinectProject
{
    public class ColorProcessingService
    {
        private Bitmap colorBitmap;
        private byte[] colorPixels;
        private CoordinateMapper coordinateMapper;

        private Bitmap fullColorBitmap;
        private readonly object colorLock = new object();

        public Bitmap ColorBitmap => colorBitmap;

        public ColorProcessingService(CoordinateMapper mapper)
        {
            coordinateMapper = mapper;
            colorBitmap = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
            colorPixels = new byte[1920 * 1080 * 4];
            fullColorBitmap = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
        }

        public Bitmap FullColorBitmap
        {
            get
            {
                lock (colorLock)
                {
                    if (fullColorBitmap == null) return null;
                    return new Bitmap(fullColorBitmap);
                }
            }
        }

        public Bitmap GenerateAlignedColorImage(DepthFrame depthFrame, ColorFrame colorFrame, ushort referenceDepth = 1500)
        {
            if (depthFrame == null || colorFrame == null) return null;

            int depthWidth = depthFrame.FrameDescription.Width;
            int depthHeight = depthFrame.FrameDescription.Height;
            int colorWidth = colorFrame.FrameDescription.Width;
            int colorHeight = colorFrame.FrameDescription.Height;

            ushort[] depthData = new ushort[depthWidth * depthHeight];
            depthFrame.CopyFrameDataToArray(depthData);

            byte[] colorData = new byte[colorWidth * colorHeight * 4];
            colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Bgra);

            lock (colorLock)
            {
                BitmapData fullBmpData = fullColorBitmap.LockBits(
                    new Rectangle(0, 0, colorWidth, colorHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);
                Marshal.Copy(colorData, 0, fullBmpData.Scan0, colorData.Length);
                fullColorBitmap.UnlockBits(fullBmpData);
            }

            ushort minDepth = (ushort)Math.Max(referenceDepth - 150, 500);
            ushort maxDepth = (ushort)Math.Min(referenceDepth + 150, 3000);

            Bitmap alignedBitmap = new Bitmap(depthWidth, depthHeight, PixelFormat.Format32bppArgb);
            BitmapData bmpData = alignedBitmap.LockBits(
                new Rectangle(0, 0, depthWidth, depthHeight),
                ImageLockMode.WriteOnly, alignedBitmap.PixelFormat);

            byte[] alignedPixels = new byte[depthWidth * depthHeight * 4];

            ColorSpacePoint[] colorPoints = new ColorSpacePoint[depthWidth * depthHeight];
            coordinateMapper.MapDepthFrameToColorSpace(depthData, colorPoints);

            Parallel.For(0, depthHeight, depthY =>
            {
                for (int depthX = 0; depthX < depthWidth; depthX++)
                {
                    int depthIndex = depthY * depthWidth + depthX;
                    ushort depthValue = depthData[depthIndex];
                    ColorSpacePoint colorPoint = colorPoints[depthIndex];
                    int outputIndex = depthIndex * 4;

                    byte b = 128, g = 128, r = 128, a = 255;

                    if (depthValue > 0 && depthValue >= minDepth && depthValue <= maxDepth)
                    {
                        int colorX = (int)(colorPoint.X + 0.5);
                        int colorY = (int)(colorPoint.Y + 0.5);

                        if (colorX >= 0 && colorX < colorWidth && colorY >= 0 && colorY < colorHeight)
                        {
                            int colorIndex = (colorY * colorWidth + colorX) * 4;
                            b = colorData[colorIndex];
                            g = colorData[colorIndex + 1];
                            r = colorData[colorIndex + 2];
                            a = 255;
                        }
                    }

                    alignedPixels[outputIndex] = b;
                    alignedPixels[outputIndex + 1] = g;
                    alignedPixels[outputIndex + 2] = r;
                    alignedPixels[outputIndex + 3] = a;
                }
            });

            Marshal.Copy(alignedPixels, 0, bmpData.Scan0, alignedPixels.Length);
            alignedBitmap.UnlockBits(bmpData);

            Bitmap result = new Bitmap(alignedBitmap);
            alignedBitmap.Dispose();
            return result;
        }

        public Bitmap CropCenter(Bitmap source, int targetWidth, int targetHeight)
        {
            if (source == null) return null;
            int x = Math.Max(0, (source.Width - targetWidth) / 2);
            int y = Math.Max(0, (source.Height - targetHeight) / 2);
            targetWidth = Math.Min(targetWidth, source.Width);
            targetHeight = Math.Min(targetHeight, source.Height);
            Rectangle cropArea = new Rectangle(x, y, targetWidth, targetHeight);
            return source.Clone(cropArea, source.PixelFormat);
        }
    }
}