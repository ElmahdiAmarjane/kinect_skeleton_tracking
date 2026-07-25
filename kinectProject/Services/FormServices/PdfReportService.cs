using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace kinectProject
{
    public class PdfReportService
    {
        public void GeneratePatientReport(PdfInputForm form, Bitmap depthImage, Bitmap colorImage,
            Bitmap splineImage, double spineAngle, float deepestZ)
        {
            try
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = "Rapport Médical Patient";
                document.Info.Author = "Kinect Body Analysis Pro";
                document.Info.Subject = "Analyse Posturale";

                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);

                XFont titleFont = new XFont("Segoe UI", 16, XFontStyle.Bold);
                XFont sectionFont = new XFont("Segoe UI", 12, XFontStyle.Bold);
                XFont labelFont = new XFont("Segoe UI", 10, XFontStyle.Bold);
                XFont valueFont = new XFont("Segoe UI", 10, XFontStyle.Regular);
                XFont smallFont = new XFont("Segoe UI", 8, XFontStyle.Italic);

                double margin = 30;
                double yPoint = 30;

                // ===== HEADER =====
                gfx.DrawString("RAPPORT D'ANALYSE POSTURALE", titleFont, XBrushes.DarkBlue,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 30), XStringFormats.TopCenter);
                yPoint += 35;

                // Date
                gfx.DrawString($"Date : {DateTime.Now:dd/MM/yyyy HH:mm}", smallFont, XBrushes.Gray,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 15), XStringFormats.TopRight);
                yPoint += 25;

                // ===== PATIENT INFO =====
                gfx.DrawString("INFORMATIONS DU PATIENT", sectionFont, XBrushes.DarkBlue, margin, yPoint);
                yPoint += 5;
                gfx.DrawLine(XPens.DarkBlue, margin, yPoint, page.Width - margin, yPoint);
                yPoint += 15;

                DrawPatientInfoRow(gfx, "Nom :", form.PatientName, labelFont, valueFont, margin, ref yPoint);
                DrawPatientInfoRow(gfx, "Âge :", form.PatientAge, labelFont, valueFont, margin, ref yPoint);
                DrawPatientInfoRow(gfx, "Sexe :", form.PatientSex, labelFont, valueFont, margin, ref yPoint);
                DrawPatientInfoRow(gfx, "Date de naissance :", form.PatientBirthDate.ToShortDateString(), labelFont, valueFont, margin, ref yPoint);
                DrawPatientInfoRow(gfx, "N° Dossier :", form.MedicalRecordNumber, labelFont, valueFont, margin, ref yPoint);

                yPoint += 10;

                // ===== MEDICAL HISTORY =====
                if (!string.IsNullOrWhiteSpace(form.MedicalHistory))
                {
                    gfx.DrawString("ANTÉCÉDENTS MÉDICAUX", sectionFont, XBrushes.DarkBlue, margin, yPoint);
                    yPoint += 5;
                    gfx.DrawLine(XPens.DarkBlue, margin, yPoint, page.Width - margin, yPoint);
                    yPoint += 15;

                    XTextFormatter tf = new XTextFormatter(gfx);
                    XRect historyRect = new XRect(margin, yPoint, page.Width - 2 * margin, 60);
                    tf.DrawString(form.MedicalHistory, valueFont, XBrushes.Black, historyRect, XStringFormats.TopLeft);
                    yPoint += 70;
                }

                // ===== IMAGES SECTION =====
                gfx.DrawString("IMAGES D'ANALYSE", sectionFont, XBrushes.DarkBlue, margin, yPoint);
                yPoint += 5;
                gfx.DrawLine(XPens.DarkBlue, margin, yPoint, page.Width - margin, yPoint);
                yPoint += 15;

                // Layout: 2 images per row
                double imgWidth = (page.Width - 2 * margin - 20) / 2;
                double imgHeight = imgWidth * 0.75; // 4:3 ratio

                int imagesAdded = 0;
                double rowYStart = yPoint;

                // Depth Image
                if (depthImage != null)
                {
                    if (imagesAdded == 2) { yPoint = rowYStart + imgHeight + 40; rowYStart = yPoint; imagesAdded = 0; }
                    double xPos = margin + (imagesAdded % 2) * (imgWidth + 20);
                    DrawImageWithBorder(gfx, depthImage, xPos, yPoint, imgWidth, imgHeight, "Image de profondeur", labelFont);
                    imagesAdded++;
                }

                // Color Image
                if (colorImage != null)
                {
                    if (imagesAdded == 2) { yPoint = rowYStart + imgHeight + 40; rowYStart = yPoint; imagesAdded = 0; }
                    double xPos = margin + (imagesAdded % 2) * (imgWidth + 20);
                    DrawImageWithBorder(gfx, colorImage, xPos, yPoint, imgWidth, imgHeight, "Image couleur alignée", labelFont);
                    imagesAdded++;
                }

                // Normal Color Image (1920x1080) - get from FullColorBitmap
                // Will be passed separately if available

                yPoint = rowYStart + imgHeight + 30;

                // ===== SPINE CURVE =====
                if (splineImage != null)
                {
                    // Check if we need a new page
                    if (yPoint + 250 > page.Height - margin)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPoint = margin;
                    }

                    gfx.DrawString("COURBE SAGITTALE", sectionFont, XBrushes.DarkBlue, margin, yPoint);
                    yPoint += 5;
                    gfx.DrawLine(XPens.DarkBlue, margin, yPoint, page.Width - margin, yPoint);
                    yPoint += 15;

                    double curveWidth = page.Width - 2 * margin;
                    double curveHeight = 200;
                    DrawImageWithBorder(gfx, splineImage, margin, yPoint, curveWidth, curveHeight, "Courbe sagittale du dos", labelFont);
                    yPoint += curveHeight + 15;

                    // Spine metrics
                    if (deepestZ > 0)
                    {
                        gfx.DrawString($"• Point de courbure maximal : {deepestZ:F0} mm", valueFont, XBrushes.Black, margin + 10, yPoint);
                        yPoint += 18;
                    }
                    if (!double.IsNaN(spineAngle))
                    {
                        gfx.DrawString($"• Angle sagittal du tronc : {spineAngle:F1}°", valueFont, XBrushes.Black, margin + 10, yPoint);
                        yPoint += 18;
                    }
                }

                // ===== MEASUREMENTS TABLE =====
                //yPoint += 20;
                //gfx.DrawString("MESURES", sectionFont, XBrushes.DarkBlue, margin, yPoint);
                //yPoint += 5;
                //gfx.DrawLine(XPens.DarkBlue, margin, yPoint, page.Width - margin, yPoint);
                //yPoint += 15;

                //// Table
                //double[] colWidths = { 180, 180, 180 };
                //string[] headers = { "Paramètre", "Valeur", "Unité" };
                //string[][] rows = {
                //    new[] { "Profondeur maximale", $"{deepestZ:F1}", "mm" },
                //    new[] { "Angle sagittal", $"{spineAngle:F1}", "°" },
                //    new[] { "Distance capteur", "-", "mm" },
                //};

                //DrawTable(gfx, margin, yPoint, colWidths, headers, rows, labelFont, valueFont);

                // ===== FOOTER =====
                yPoint = page.Height - 30;
                gfx.DrawString($"Rapport généré le {DateTime.Now:dd/MM/yyyy à HH:mm} - Kinect Body Analysis Pro",
                    smallFont, XBrushes.Gray,
                    new XRect(margin, yPoint, page.Width - 2 * margin, 15), XStringFormats.Center);

                // ===== SAVE =====
                string safeFileName = (form.PatientName ?? "Patient")
                    .Replace(" ", "_").Replace("/", "-").Replace("\\", "-");
                string filename = $"Rapport_{safeFileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fullPath = Path.Combine(folder, filename);

                document.Save(fullPath);
                document.Close();

                if (MessageBox.Show($"PDF généré avec succès !\n\n{fullPath}\n\nOuvrir le document ?",
                    "Succès", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start(fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la génération du PDF :\n" + ex.Message,
                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DrawPatientInfoRow(XGraphics gfx, string label, string value,
            XFont labelFont, XFont valueFont, double margin, ref double yPoint)
        {
            gfx.DrawString(label, labelFont, XBrushes.Black, margin, yPoint);
            gfx.DrawString(value ?? "-", valueFont, XBrushes.Black, margin + 130, yPoint);
            yPoint += 18;
        }

        private void DrawImageWithBorder(XGraphics gfx, Bitmap image, double x, double y,
            double maxWidth, double maxHeight, string title, XFont titleFont)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    XImage xImg = XImage.FromStream(ms);

                    double imgRatio = (double)xImg.PixelWidth / xImg.PixelHeight;
                    double drawWidth = maxWidth;
                    double drawHeight = drawWidth / imgRatio;

                    if (drawHeight > maxHeight - 20)
                    {
                        drawHeight = maxHeight - 20;
                        drawWidth = drawHeight * imgRatio;
                    }

                    // Center image in its area
                    double xOffset = x + (maxWidth - drawWidth) / 2;

                    // Draw border
                    gfx.DrawRectangle(XPens.Gray, x, y, maxWidth, maxHeight - 20);

                    // Draw image
                    gfx.DrawImage(xImg, xOffset, y + 2, drawWidth, drawHeight);

                    // Draw title below
                    XRect titleRect = new XRect(x, y + maxHeight - 18, maxWidth, 15);
                    gfx.DrawString(title, titleFont, XBrushes.Black, titleRect, XStringFormats.TopCenter);
                }
            }
            catch (Exception ex)
            {
                gfx.DrawString($"Erreur image: {ex.Message}", titleFont, XBrushes.Red, x, y);
            }
        }

        private void DrawTable(XGraphics gfx, double x, double y, double[] colWidths,
            string[] headers, string[][] rows, XFont headerFont, XFont cellFont)
        {
            double rowHeight = 22;
            double currentX = x;

            // Draw headers
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawRectangle(XPens.DarkBlue, currentX, y, colWidths[i], rowHeight);
                gfx.DrawString(headers[i], headerFont, XBrushes.White,
                    new XRect(currentX + 3, y + 3, colWidths[i] - 6, rowHeight - 6),
                    XStringFormats.CenterLeft);

                // Header background
                gfx.DrawRectangle(XBrushes.DarkBlue, currentX, y, colWidths[i], rowHeight);
                gfx.DrawString(headers[i], headerFont, XBrushes.White,
                    new XRect(currentX + 3, y + 3, colWidths[i] - 6, rowHeight - 6),
                    XStringFormats.CenterLeft);

                currentX += colWidths[i];
            }

            y += rowHeight;
            bool alternate = false;

            // Draw rows
            foreach (var row in rows)
            {
                currentX = x;
                for (int i = 0; i < row.Length; i++)
                {
                    if (alternate)
                        gfx.DrawRectangle(XBrushes.LightGray, currentX, y, colWidths[i], rowHeight);
                    else
                        gfx.DrawRectangle(XPens.Gray, currentX, y, colWidths[i], rowHeight);

                    gfx.DrawString(row[i], cellFont, XBrushes.Black,
                        new XRect(currentX + 3, y + 3, colWidths[i] - 6, rowHeight - 6),
                        XStringFormats.CenterLeft);

                    currentX += colWidths[i];
                }
                y += rowHeight;
                alternate = !alternate;
            }
        }
    }
}