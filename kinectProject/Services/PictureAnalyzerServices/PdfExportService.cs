using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using iTextBaseColor = iTextSharp.text.BaseColor;
using iTextChunk = iTextSharp.text.Chunk;
using iTextDocument = iTextSharp.text.Document;
using iTextFont = iTextSharp.text.Font;
using iTextImage = iTextSharp.text.Image;
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextPdfPCell = iTextSharp.text.pdf.PdfPCell;
using iTextPdfPTable = iTextSharp.text.pdf.PdfPTable;
using iTextPdfWriter = iTextSharp.text.pdf.PdfWriter;
using iTextPhrase = iTextSharp.text.Phrase;
using SDBitmap = System.Drawing.Bitmap;
using SDBrush = System.Drawing.Brush;
using SDColor = System.Drawing.Color;
using SDFont = System.Drawing.Font;
using SDFontStyle = System.Drawing.FontStyle;
using SDGraphics = System.Drawing.Graphics;
// Explicit aliases to resolve ambiguity between System.Drawing and iTextSharp.text
using SDImage = System.Drawing.Image;
using SDPen = System.Drawing.Pen;
using SDPoint = System.Drawing.Point;
using SDRectangle = System.Drawing.Rectangle;
using SDSolidBrush = System.Drawing.SolidBrush;

namespace kinectProject
{
    public class PdfExportService
    {
        private CalculationService calcService;
        private MeasurementService measurementService;

        public PdfExportService()
        {
            calcService = new CalculationService();
            measurementService = new MeasurementService();
        }

        #region Main Export

        /// <summary>
        /// Export measurements to PDF file
        /// </summary>
        public void ExportToPdf(
            SDImage originalImage,
            List<Measurement> measurements,
            List<IntersectionPoint> intersectionPoints,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            if (originalImage == null)
            {
                MessageBox.Show("Please load an image first.", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PDF Files|*.pdf";
                saveDialog.Title = "Export Measurements as PDF";
                saveDialog.FileName = $"Measurement_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CreatePdfReport(
                            saveDialog.FileName,
                            originalImage,
                            measurements,
                            intersectionPoints,
                            isReferenceSet,
                            pixelToRealRatio);

                        MessageBox.Show($"PDF exported successfully to:\n{saveDialog.FileName}",
                            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (MessageBox.Show("Would you like to open the PDF now?", "Open PDF",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            Process.Start(saveDialog.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error creating PDF: {ex.Message}", "Export Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Create the PDF report document
        /// </summary>
        public void CreatePdfReport(
            string filePath,
            SDImage originalImage,
            List<Measurement> measurements,
            List<IntersectionPoint> intersectionPoints,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            // Create document with margins
            iTextDocument document = new iTextDocument(PageSize.A4, 36, 36, 36, 36);
            iTextPdfWriter writer = iTextPdfWriter.GetInstance(document, new FileStream(filePath, FileMode.Create));
            document.Open();

            // ===== Title =====
            iTextFont titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, iTextBaseColor.DARK_GRAY);
            iTextParagraph title = new iTextParagraph("Body Measurement Analysis Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // ===== Date =====
            iTextFont dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, iTextBaseColor.GRAY);
            iTextParagraph date = new iTextParagraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}", dateFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(date);

            // ===== Image with Measurements =====
            AddImageWithMeasurements(document, writer, originalImage, measurements, isReferenceSet, pixelToRealRatio);

            // ===== Measurements Table =====
            AddMeasurementsTable(document, writer, measurements, isReferenceSet, pixelToRealRatio);

            // ===== Intersection Points Analysis =====
            if (intersectionPoints != null && intersectionPoints.Count > 0)
            {
                AddIntersectionPointsTable(document, writer, intersectionPoints);
                AddIntersectionDetails(document, writer, intersectionPoints);
            }

            // ===== Reference Scale =====
            if (isReferenceSet)
            {
                document.Add(new iTextParagraph(
                    $"Reference Scale: 1 cm = {pixelToRealRatio:F2} pixels",
                    FontFactory.GetFont(FontFactory.HELVETICA, 9, iTextBaseColor.GRAY)));
            }

            // ===== Footer =====
            iTextParagraph footer = new iTextParagraph(
                "Generated by Body Measurement Analysis Tool",
                FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, iTextBaseColor.LIGHT_GRAY))
            {
                Alignment = Element.ALIGN_RIGHT,
                SpacingBefore = 20
            };
            document.Add(footer);

            document.Close();
        }

        #endregion

        #region Image Export

        /// <summary>
        /// Add the image with measurement overlays to the PDF
        /// </summary>
        private void AddImageWithMeasurements(
            iTextDocument document,
            iTextPdfWriter writer,
            SDImage originalImage,
            List<Measurement> measurements,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            if (originalImage == null) return;

            try
            {
                using (SDBitmap bmp = new SDBitmap(originalImage.Width, originalImage.Height))
                using (SDGraphics g = SDGraphics.FromImage(bmp))
                {
                    g.Clear(SDColor.White);
                    g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                    // Draw all measurements on the bitmap
                    foreach (var m in measurements)
                        DrawMeasurementOnBitmap(g, m, measurements, isReferenceSet, pixelToRealRatio);

                    string tempImagePath = Path.GetTempFileName() + ".png";
                    bmp.Save(tempImagePath, ImageFormat.Png);

                    iTextImage pdfImage = iTextImage.GetInstance(tempImagePath);
                    pdfImage.Alignment = Element.ALIGN_CENTER;

                    float maxWidth = document.PageSize.Width - 72;
                    float maxHeight = document.PageSize.Height - 200;
                    pdfImage.ScaleToFit(maxWidth, maxHeight);

                    if (writer.GetVerticalPosition(false) - pdfImage.ScaledHeight < document.BottomMargin)
                        document.NewPage();

                    pdfImage.SpacingAfter = 20;
                    document.Add(pdfImage);

                    File.Delete(tempImagePath);
                }
            }
            catch (Exception ex)
            {
                document.Add(new iTextParagraph($"Error adding image: {ex.Message}"));
            }
        }

        /// <summary>
        /// Draw a single measurement on the export bitmap
        /// </summary>
        private void DrawMeasurementOnBitmap(
            SDGraphics g,
            Measurement m,
            List<Measurement> measurements,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            SDColor color = measurementService.GetMeasurementColor(m.Type);
            int lineWidth = 2;
            int pointSize = 6;

            using (SDPen pen = new SDPen(color, lineWidth))
            using (SDBrush brush = new SDSolidBrush(color))
            using (SDFont font = new SDFont("Arial", 10, SDFontStyle.Bold))
            using (SDBrush textBrush = new SDSolidBrush(SDColor.Black))
            {
                switch (m.Type)
                {
                    case MeasurementType.Point:
                        g.FillEllipse(brush,
                            m.Start.X - pointSize / 2,
                            m.Start.Y - pointSize / 2,
                            pointSize, pointSize);
                        g.DrawString(m.ID.ToString(), font, textBrush,
                            m.Start.X + 5, m.Start.Y - 10);
                        break;

                    case MeasurementType.Line:
                        DrawLineMeasurement(g, pen, brush, font, textBrush, m, pointSize);
                        break;

                    case MeasurementType.Distance:
                    case MeasurementType.ReferenceLine:
                        DrawDistanceMeasurement(g, pen, brush, font, textBrush, m,
                            pointSize, isReferenceSet, pixelToRealRatio);
                        break;

                    case MeasurementType.Angle:
                        if (m.Vertex.HasValue)
                        {
                            DrawAngleMeasurement(g, pen, brush, font, textBrush, m,
                                measurements, pointSize);
                        }
                        break;

                    case MeasurementType.AngleWithAxis:
                        DrawAngleWithAxisMeasurement(g, pen, brush, font, textBrush, m, pointSize);
                        break;

                    case MeasurementType.PerpendicularLine:
                        DrawPerpendicularMeasurement(g, pen, brush, font, textBrush, m,
                            pointSize, isReferenceSet, pixelToRealRatio);
                        break;
                }
            }
        }

        /// <summary>
        /// Draw line measurement on bitmap
        /// </summary>
        private void DrawLineMeasurement(
            SDGraphics g, SDPen pen, SDBrush brush, SDFont font, SDBrush textBrush,
            Measurement m, int pointSize)
        {
            g.DrawLine(pen, m.Start, m.End);
            g.FillEllipse(brush,
                m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2,
                pointSize, pointSize);
            g.FillEllipse(brush,
                m.End.X - pointSize / 2, m.End.Y - pointSize / 2,
                pointSize, pointSize);

            SDPoint lineMidPoint = new SDPoint(
                (m.Start.X + m.End.X) / 2,
                (m.Start.Y + m.End.Y) / 2);
            g.DrawString(m.ID.ToString(), font, textBrush,
                lineMidPoint.X, lineMidPoint.Y - 15);
        }

        /// <summary>
        /// Draw distance/reference measurement on bitmap
        /// </summary>
        private void DrawDistanceMeasurement(
            SDGraphics g, SDPen pen, SDBrush brush, SDFont font, SDBrush textBrush,
            Measurement m, int pointSize, bool isReferenceSet, float pixelToRealRatio)
        {
            g.DrawLine(pen, m.Start, m.End);
            g.FillEllipse(brush,
                m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2,
                pointSize, pointSize);
            g.FillEllipse(brush,
                m.End.X - pointSize / 2, m.End.Y - pointSize / 2,
                pointSize, pointSize);

            double distance = calcService.CalculateDistance(m.Start, m.End);
            string distText;

            if (m.Type == MeasurementType.ReferenceLine)
            {
                distText = $"{m.ID}: {distance / pixelToRealRatio:F1} cm";
            }
            else if (isReferenceSet)
            {
                distText = $"{m.ID}: {distance / pixelToRealRatio:F1} cm";
            }
            else
            {
                distText = $"{m.ID}: {distance:F1} px";
            }

            SDPoint midPoint = new SDPoint(
                (m.Start.X + m.End.X) / 2,
                (m.Start.Y + m.End.Y) / 2);
            g.DrawString(distText, font, textBrush, midPoint.X, midPoint.Y - 15);
        }

        /// <summary>
        /// Draw angle measurement on bitmap
        /// </summary>
        private void DrawAngleMeasurement(
            SDGraphics g, SDPen pen, SDBrush brush, SDFont font, SDBrush textBrush,
            Measurement m, List<Measurement> measurements, int pointSize)
        {
            g.DrawLine(pen, m.Vertex.Value, m.End);
            g.FillEllipse(brush,
                m.Vertex.Value.X - pointSize / 2,
                m.Vertex.Value.Y - pointSize / 2,
                pointSize, pointSize);
            g.FillEllipse(brush,
                m.End.X - pointSize / 2,
                m.End.Y - pointSize / 2,
                pointSize, pointSize);

            // Find the other segment
            Measurement otherSegment = measurements.FirstOrDefault(meas =>
                meas.Type == MeasurementType.Angle &&
                meas.Vertex.HasValue &&
                meas.ID == m.ID &&
                meas.End != m.End);

            if (otherSegment.Type == MeasurementType.Angle)
            {
                double angle = calcService.CalculateAngle(m, otherSegment);
                string angleText = $"{m.ID}: {angle:F1}°";
                g.DrawString(angleText, font, textBrush,
                    m.Vertex.Value.X, m.Vertex.Value.Y - 20);
            }
        }

        /// <summary>
        /// Draw angle-with-axis measurement on bitmap
        /// </summary>
        private void DrawAngleWithAxisMeasurement(
            SDGraphics g, SDPen pen, SDBrush brush, SDFont font, SDBrush textBrush,
            Measurement m, int pointSize)
        {
            g.DrawLine(pen, m.Start, m.End);
            g.FillEllipse(brush,
                m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2,
                pointSize, pointSize);
            g.FillEllipse(brush,
                m.End.X - pointSize / 2, m.End.Y - pointSize / 2,
                pointSize, pointSize);

            double axisAngle = calcService.CalculateAngleWithAxis(m);
            string axisAngleText = $"{m.ID}: {axisAngle:F1}° to {m.Axis}";
            SDPoint axisMidPoint = new SDPoint(
                (m.Start.X + m.End.X) / 2,
                (m.Start.Y + m.End.Y) / 2);
            g.DrawString(axisAngleText, font, textBrush,
                axisMidPoint.X, axisMidPoint.Y - 15);
        }

        /// <summary>
        /// Draw perpendicular measurement on bitmap
        /// </summary>
        private void DrawPerpendicularMeasurement(
            SDGraphics g, SDPen pen, SDBrush brush, SDFont font, SDBrush textBrush,
            Measurement m, int pointSize, bool isReferenceSet, float pixelToRealRatio)
        {
            g.DrawLine(pen, m.Start, m.End);
            g.FillEllipse(brush,
                m.Start.X - pointSize / 2, m.Start.Y - pointSize / 2,
                pointSize, pointSize);
            g.FillEllipse(brush,
                m.End.X - pointSize / 2, m.End.Y - pointSize / 2,
                pointSize, pointSize);

            SDPoint perpMidPoint = new SDPoint(
                (m.Start.X + m.End.X) / 2,
                (m.Start.Y + m.End.Y) / 2);
            g.DrawString($"{m.ID}: ", font, textBrush,
                perpMidPoint.X, perpMidPoint.Y - 15);

            // Draw perpendicular symbol
            using (SDPen symbolPen = new SDPen(SDColor.Black, 1))
            {
                g.DrawRectangle(symbolPen,
                    m.Start.X - 2, m.Start.Y - 2, 4, 4);
            }
        }

        #endregion

        #region Measurements Table

        /// <summary>
        /// Add measurements summary table to PDF
        /// </summary>
        private void AddMeasurementsTable(
            iTextDocument document,
            iTextPdfWriter writer,
            List<Measurement> measurements,
            bool isReferenceSet,
            float pixelToRealRatio)
        {
            if (!measurements.Any())
            {
                document.Add(new iTextParagraph(
                    "No measurements recorded.",
                    FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 10, iTextBaseColor.GRAY)));
                return;
            }

            float estimatedHeight = measurements.Count * 20 + 50;
            if (writer.GetVerticalPosition(false) - estimatedHeight < document.BottomMargin + 100)
                document.NewPage();

            iTextParagraph measurementsHeader = new iTextParagraph(
                "Measurement Summary",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, iTextBaseColor.DARK_GRAY))
            {
                SpacingBefore = 10,
                SpacingAfter = 10
            };
            document.Add(measurementsHeader);

            iTextPdfPTable table = new iTextPdfPTable(5)
            {
                WidthPercentage = 100
            };
            table.SetWidths(new float[] { 1, 2, 3, 2, 3 });

            iTextFont headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, iTextBaseColor.WHITE);
            AddTableHeaderCell(table, "ID", headerFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(table, "Type", headerFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(table, "Name", headerFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(table, "Pixel Value", headerFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(table, "Real Value", headerFont, iTextBaseColor.DARK_GRAY);

            var groupedMeasurements = measurements
                .GroupBy(m => m.ID)
                .Select(g => g.First())
                .OrderBy(m => m.ID);

            iTextFont cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var m in groupedMeasurements)
                AddMeasurementToTable(table, m, cellFont, isReferenceSet, pixelToRealRatio, measurements);

            document.Add(table);
        }

        /// <summary>
        /// Add a single measurement row to the table
        /// </summary>
        private void AddMeasurementToTable(
            iTextPdfPTable table,
            Measurement m,
            iTextFont font,
            bool isReferenceSet,
            float pixelToRealRatio,
             List<Measurement> allMeasurements)
        {
            // ID column
            table.AddCell(new iTextPdfPCell(new iTextPhrase(m.ID.ToString(), font))
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            // Type column
            string typeStr = measurementService.GetMeasurementTypeString(m.Type);
            table.AddCell(new iTextPdfPCell(new iTextPhrase(typeStr, font))
            {
                Padding = 5
            });

            // Name column
            table.AddCell(new iTextPdfPCell(new iTextPhrase(m.Name, font))
            {
                Padding = 5
            });

            // Pixel Value column
            string pixelValue = measurementService.GetPixelValueString(m, isReferenceSet, pixelToRealRatio, allMeasurements);
            table.AddCell(new iTextPdfPCell(new iTextPhrase(pixelValue, font))
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_RIGHT
            });

            // Real Value column
            string realValue = measurementService.GetRealValueString(m, isReferenceSet, pixelToRealRatio, allMeasurements);
            table.AddCell(new iTextPdfPCell(new iTextPhrase(realValue, font))
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_RIGHT
            });
        }

        #endregion

        #region Intersection Points Table

        /// <summary>
        /// Add intersection points table to PDF
        /// </summary>
        private void AddIntersectionPointsTable(
            iTextDocument document,
            iTextPdfWriter writer,
            List<IntersectionPoint> intersectionPoints)
        {
            if (writer.GetVerticalPosition(false) < document.BottomMargin + 100)
                document.NewPage();

            iTextParagraph intersectionHeader = new iTextParagraph(
                "Intersection Points Analysis",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, iTextBaseColor.DARK_GRAY))
            {
                SpacingBefore = 20,
                SpacingAfter = 10
            };
            document.Add(intersectionHeader);

            iTextPdfPTable intersectionTable = new iTextPdfPTable(4)
            {
                WidthPercentage = 100
            };
            intersectionTable.SetWidths(new float[] { 1, 2, 3, 4 });

            iTextFont intHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, iTextBaseColor.WHITE);

            AddTableHeaderCell(intersectionTable, "ID", intHeaderFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(intersectionTable, "Type", intHeaderFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(intersectionTable, "Coordinates", intHeaderFont, iTextBaseColor.DARK_GRAY);
            AddTableHeaderCell(intersectionTable, "Lines & Angles", intHeaderFont, iTextBaseColor.DARK_GRAY);

            iTextFont intCellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            foreach (var ip in intersectionPoints.OrderBy(p => p.ID))
                AddIntersectionToTable(intersectionTable, ip, intCellFont);

            document.Add(intersectionTable);
        }

        /// <summary>
        /// Add a single intersection point row to the table
        /// </summary>
        private void AddIntersectionToTable(
            iTextPdfPTable table,
            IntersectionPoint ip,
            iTextFont font)
        {
            // ID column
            table.AddCell(new iTextPdfPCell(new iTextPhrase($"P{ip.ID}", font))
            {
                Padding = 5,
                HorizontalAlignment = Element.ALIGN_CENTER
            });

            // Type column
            table.AddCell(new iTextPdfPCell(new iTextPhrase(ip.Type.ToString(), font))
            {
                Padding = 5
            });

            // Coordinates column
            table.AddCell(new iTextPdfPCell(new iTextPhrase($"({ip.Location.X}, {ip.Location.Y})", font))
            {
                Padding = 5
            });

            // Lines & Angles column
            string linesText = $"Lines: {string.Join(", ", ip.LineIDs.Select(id => $"L{id}"))}";

            StringBuilder anglesText = new StringBuilder();
            if (ip.Angles.Count > 0)
            {
                var angleGroups = ip.Angles
                    .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                    .Select(g => new
                    {
                        Line1 = g.Key.Line1,
                        Line2 = g.Key.Line2,
                        Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                    });

                foreach (var group in angleGroups)
                {
                    if (group.Angles.Count == 2)
                    {
                        anglesText.AppendLine($"L{group.Line1}-L{group.Line2}: {group.Angles[0]:F1}° & {group.Angles[1]:F1}°");
                    }
                    else if (group.Angles.Count == 1)
                    {
                        anglesText.AppendLine($"L{group.Line1}-L{group.Line2}: {group.Angles[0]:F1}°");
                    }
                }
            }

            iTextPhrase cellContent = new iTextPhrase();
            cellContent.Add(new iTextChunk(linesText + "\n", font));
            if (anglesText.Length > 0)
            {
                cellContent.Add(new iTextChunk(anglesText.ToString(), font));
            }

            table.AddCell(new iTextPdfPCell(cellContent)
            {
                Padding = 5,
                PaddingTop = 8,
                PaddingBottom = 8
            });
        }

        /// <summary>
        /// Add detailed intersection analysis
        /// </summary>
        private void AddIntersectionDetails(
            iTextDocument document,
            iTextPdfWriter writer,
            List<IntersectionPoint> intersectionPoints)
        {
            if (writer.GetVerticalPosition(false) < document.BottomMargin + 200)
                document.NewPage();

            iTextParagraph detailHeader = new iTextParagraph(
                "Detailed Angle Analysis",
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, iTextBaseColor.DARK_GRAY))
            {
                SpacingBefore = 15,
                SpacingAfter = 10
            };
            document.Add(detailHeader);

            string intersectionData = GetIntersectionDataForPdf(intersectionPoints);

            iTextParagraph detailContent = new iTextParagraph(
                intersectionData,
                FontFactory.GetFont(FontFactory.HELVETICA, 10))
            {
                SpacingAfter = 15
            };
            document.Add(detailContent);
        }

        /// <summary>
        /// Get intersection data formatted for PDF
        /// </summary>
        private string GetIntersectionDataForPdf(List<IntersectionPoint> intersectionPoints)
        {
            if (intersectionPoints.Count == 0)
                return "No intersection points detected.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INTERSECTION POINTS ANALYSIS");
            sb.AppendLine("=============================");

            foreach (var ip in intersectionPoints.OrderBy(p => p.ID))
            {
                sb.AppendLine();
                sb.AppendLine($"Intersection Point P{ip.ID}");
                sb.AppendLine($"Type: {ip.Type}");
                sb.AppendLine($"Coordinates: ({ip.Location.X}, {ip.Location.Y})");
                sb.AppendLine($"Lines involved: {string.Join(", ", ip.LineIDs.Select(id => $"L{id}"))}");

                if (ip.Angles.Count > 0)
                {
                    sb.AppendLine("Angles between lines:");

                    var angleGroups = ip.Angles
                        .GroupBy(a => new { Line1 = Math.Min(a.Item1, a.Item2), Line2 = Math.Max(a.Item1, a.Item2) })
                        .Select(g => new
                        {
                            Line1 = g.Key.Line1,
                            Line2 = g.Key.Line2,
                            Angles = g.Select(x => x.Item3).Distinct().OrderBy(a => a).ToList()
                        })
                        .OrderBy(g => g.Line1).ThenBy(g => g.Line2);

                    foreach (var group in angleGroups)
                    {
                        if (group.Angles.Count == 2)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}:");
                            sb.AppendLine($"    Acute angle: {group.Angles[0]:F1}°");
                            sb.AppendLine($"    Obtuse angle: {group.Angles[1]:F1}°");
                            sb.AppendLine($"    Sum: {(group.Angles[0] + group.Angles[1]):F1}°");
                        }
                        else if (group.Angles.Count == 1)
                        {
                            sb.AppendLine($"  • Between L{group.Line1} and L{group.Line2}: {group.Angles[0]:F1}°");
                            if (Math.Abs(group.Angles[0] - 90) < 0.1)
                                sb.AppendLine("    → RIGHT ANGLE");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("No angle measurements available");
                }

                sb.AppendLine(new string('-', 50));
            }

            return sb.ToString();
        }

        #endregion

        #region Table Helpers

        /// <summary>
        /// Add a header cell to a PDF table
        /// </summary>
        private void AddTableHeaderCell(iTextPdfPTable table, string text, iTextFont font, iTextBaseColor bgColor)
        {
            iTextPdfPCell cell = new iTextPdfPCell(new iTextPhrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        #endregion
    }
}