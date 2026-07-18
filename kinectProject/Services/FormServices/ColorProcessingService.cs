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

        public Bitmap ColorBitmap => colorBitmap;

        public ColorProcessingService(CoordinateMapper mapper)
        {
            coordinateMapper = mapper;
            colorBitmap = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
            colorPixels = new byte[1920 * 1080 * 4];
        }

        public Bitmap GenerateAlignedColorImage(DepthFrame depthFrame, ColorFrame colorFrame)
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

                    if (depthValue > 0 && depthValue >= 500 && depthValue <= 2000)
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

            return alignedBitmap;
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