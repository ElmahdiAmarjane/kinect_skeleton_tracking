using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace kinectProject
{
    public class PdfReportService
    {
        public void GeneratePatientReport(PdfInputForm form, Image depthImage, Image colorImage,
            Image splineImage, SpineCurveService spineService, PictureBox depthPictureBox)
        {
            try
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = "Rapport Médical Patient";

                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XFont titleFont = new XFont("Segoe UI", 18, XFontStyle.Bold);
                XFont labelFont = new XFont("Segoe UI", 12, XFontStyle.Bold);
                XFont valueFont = new XFont("Segoe UI", 12, XFontStyle.Regular);

                double margin = 40;
                double yPoint = margin;
                double pageHeight = page.Height;

                // Titre
                gfx.DrawString("Rapport d'analyse posturale", titleFont, XBrushes.DarkBlue,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 40), XStringFormats.TopCenter);
                yPoint += 50;

                // Infos patient
                gfx.DrawString("Informations du patient", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 25;

                string[] patientInfo = new[]
                {
                    $"Nom : {form.PatientName}",
                    $"Âge : {form.PatientAge}",
                    $"Sexe : {form.PatientSex}",
                    $"Date de naissance : {form.PatientBirthDate.ToShortDateString()}",
                    $"N° Dossier médical : {form.MedicalRecordNumber}"
                };

                foreach (var info in patientInfo)
                {
                    gfx.DrawString(info, valueFont, XBrushes.Black, margin, yPoint);
                    yPoint += 20;
                }

                // Antécédents
                yPoint += 10;
                gfx.DrawString("Antécédents médicaux :", labelFont, XBrushes.Black, margin, yPoint);
                yPoint += 20;

                XTextFormatter tf = new XTextFormatter(gfx);
                XRect historyRect = new XRect(margin, yPoint, page.Width - 2 * margin, 80);
                tf.DrawString(form.MedicalHistory, valueFont, XBrushes.Black, historyRect, XStringFormats.TopLeft);
                yPoint += 100;

                // Image couleur
                if (colorImage != null)
                {
                    yPoint = DrawImage(gfx, colorImage, page, margin, yPoint, "Image couleur du patient", labelFont);
                }

                // Image profondeur
                if (depthImage != null)
                {
                    yPoint = DrawImage(gfx, depthImage, page, margin, yPoint, "Image de profondeur (analyse thermique)", labelFont);
                }

                // Courbe sagittale
                if (splineImage != null)
                {
                    yPoint = DrawImage(gfx, splineImage, page, margin, yPoint, "Courbe sagittale du dos", labelFont);

                    if (spineService.MaxZIndex >= 0 && spineService.LastSmoothedSpinePoints != null &&
                        spineService.MaxZIndex < spineService.LastSmoothedSpinePoints.Count)
                    {
                        float deepestZ = spineService.LastSmoothedSpinePoints[spineService.MaxZIndex].X;
                        gfx.DrawString($"Point de courbure maximal : {deepestZ:F0} mm", valueFont, XBrushes.Black, margin, yPoint);
                        yPoint += 20;
                    }
                }

                // Pied de page
                yPoint += 20;
                gfx.DrawString($"Rapport généré le {DateTime.Now:dd/MM/yyyy à HH:mm}",
                    new XFont("Segoe UI", 10, XFontStyle.Italic), XBrushes.Gray,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 30), XStringFormats.Center);

                // Sauvegarde
                string safeFileName = form.PatientName.Replace(" ", "_").Replace("/", "-").Replace("\\", "-");
                string filename = $"rapport_{safeFileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), filename);
                document.Save(fullPath);
                document.Close();

                if (MessageBox.Show($"PDF généré avec succès !\n\nVoulez-vous ouvrir le document ?",
                    "Succès", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la génération du PDF :\n" + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private double DrawImage(XGraphics gfx, Image image, PdfPage page, double margin,
            double yPoint, string title, XFont font)
        {
            gfx.DrawString(title, font, XBrushes.Black, margin, yPoint);
            yPoint += 20;

            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                XImage xImg = XImage.FromStream(ms);

                double maxWidth = page.Width - 2 * margin;
                double maxHeight = page.Height - yPoint - margin;

                double imgRatio = (double)xImg.PixelWidth / xImg.PixelHeight;
                double targetWidth = maxWidth;
                double targetHeight = targetWidth / imgRatio;

                if (targetHeight > maxHeight)
                {
                    targetHeight = maxHeight;
                    targetWidth = targetHeight * imgRatio;
                }

                gfx.DrawImage(xImg, margin, yPoint, targetWidth, targetHeight);
                return yPoint + targetHeight + 20;
            }
        }
    }
}