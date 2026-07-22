using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Service to capture and save all Kinect images at once
    /// </summary>
    public class ImageCaptureService
    {
        private DepthProcessingService depthService;
        private ColorProcessingService colorService;

        public ImageCaptureService(DepthProcessingService depthService, ColorProcessingService colorService)
        {
            this.depthService = depthService;
            this.colorService = colorService;
        }

        /// <summary>
        /// Get all three images for preview
        /// </summary>
        public (Image depth, Image colorAligned, Image normalColor) CaptureAllImages()
        {
            Image depth = null;
            Image colorAligned = null;
            Image normalColor = null;

            try
            {
                if (depthService != null && depthService.DepthBitmap != null)
                {
                    depth = new Bitmap(depthService.DepthBitmap);
                }

                if (colorService != null)
                {
                    normalColor = colorService.FullColorBitmap;
                }
            }
            catch (Exception)
            {
                // Return what we have
            }

            return (depth, colorAligned, normalColor);
        }

        /// <summary>
        /// Show preview dialog and save if user confirms
        /// </summary>
        public bool ShowPreviewAndSave(Image depthImage, Image colorImage, Image normalImage)
        {
            // Normal image is the color image (both are same in this case)
            // If you want the aligned image, pass it separately

            using (var previewDialog = new MultiImagePreviewDialog())
            {
                previewDialog.SetImages(depthImage, colorImage, normalImage);
                return previewDialog.ShowDialog() == DialogResult.OK;
            }
        }
    }
}