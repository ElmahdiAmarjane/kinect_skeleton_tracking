using System;
using System.Drawing;
using System.Windows.Forms;

namespace kinectProject
{
    /// <summary>
    /// Dialog to collect patient information for PDF report
    /// </summary>
    public class PdfInputForm : Form
    {
        private TextBox txtName;
        private TextBox txtAge;
        private ComboBox cmbSex;
        private DateTimePicker dtpBirthDate;
        private TextBox txtMedicalRecord;
        private RichTextBox txtMedicalHistory;
        private Button btnGenerate;
        private Button btnCancel;

        public string PatientName => txtName?.Text ?? "";
        public string PatientAge => txtAge?.Text ?? "";
        public string PatientSex => cmbSex?.SelectedItem?.ToString() ?? "";
        public DateTime PatientBirthDate => dtpBirthDate?.Value ?? DateTime.Now;
        public string MedicalRecordNumber => txtMedicalRecord?.Text ?? "";
        public string MedicalHistory => txtMedicalHistory?.Text ?? "";

        public PdfInputForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Informations Patient - Rapport PDF";
            this.Size = new Size(500, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            int yPos = 15;
            int labelWidth = 150;
            int controlWidth = 280;
            int spacing = 35;

            // Title
            Label lblTitle = new Label
            {
                Text = "Informations du Patient",
                Location = new Point(20, yPos),
                Size = new Size(440, 30),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            yPos += 45;

            // Patient Name
            AddLabelAndControl("Nom du patient :", ref yPos, labelWidth, spacing,
                out _, out TextBox txtNameControl);
            txtName = txtNameControl;

            // Age
            AddLabelAndControl("Âge :", ref yPos, labelWidth, spacing,
                out _, out TextBox txtAgeControl);
            txtAge = txtAgeControl;

            // Sex
            Label lblSex = new Label
            {
                Text = "Sexe :",
                Location = new Point(25, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = Color.LightGray
            };

            cmbSex = new ComboBox
            {
                Location = new Point(25 + labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbSex.Items.AddRange(new string[] { "Masculin", "Féminin", "Autre" });
            cmbSex.SelectedIndex = 0;

            this.Controls.Add(lblSex);
            this.Controls.Add(cmbSex);
            yPos += spacing;

            // Birth Date
            Label lblBirth = new Label
            {
                Text = "Date de naissance :",
                Location = new Point(25, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = Color.LightGray
            };

            dtpBirthDate = new DateTimePicker
            {
                Location = new Point(25 + labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                Format = DateTimePickerFormat.Short,
                Value = new DateTime(1990, 1, 1)
            };

            this.Controls.Add(lblBirth);
            this.Controls.Add(dtpBirthDate);
            yPos += spacing;

            // Medical Record Number
            AddLabelAndControl("N° Dossier médical :", ref yPos, labelWidth, spacing,
                out _, out TextBox txtMedControl);
            txtMedicalRecord = txtMedControl;

            // Medical History
            Label lblHistory = new Label
            {
                Text = "Antécédents médicaux :",
                Location = new Point(25, yPos),
                Size = new Size(labelWidth + controlWidth, 25),
                ForeColor = Color.LightGray
            };
            yPos += 25;

            txtMedicalHistory = new RichTextBox
            {
                Location = new Point(25, yPos),
                Size = new Size(labelWidth + controlWidth, 100),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            yPos += 115;

            // Buttons
            btnCancel = new Button
            {
                Text = "Annuler",
                DialogResult = DialogResult.Cancel,
                Location = new Point(180, yPos),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            btnGenerate = new Button
            {
                Text = "📄 Générer PDF",
                Location = new Point(290, yPos),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Veuillez entrer le nom du patient.", "Champ requis",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            // Add controls to form
            this.Controls.Add(lblTitle);
            this.Controls.Add(txtName);
            this.Controls.Add(txtAge);
            this.Controls.Add(txtMedicalRecord);
            this.Controls.Add(lblHistory);
            this.Controls.Add(txtMedicalHistory);
            this.Controls.Add(btnCancel);
            this.Controls.Add(btnGenerate);

            this.AcceptButton = btnGenerate;
            this.CancelButton = btnCancel;
        }

        private void AddLabelAndControl(string labelText, ref int yPos, int labelWidth, int spacing,
            out Label label, out TextBox textBox)
        {
            label = new Label
            {
                Text = labelText,
                Location = new Point(25, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = Color.LightGray
            };

            textBox = new TextBox
            {
                Location = new Point(25 + labelWidth, yPos),
                Size = new Size(280, 25),
                BackColor = Color.FromArgb(62, 62, 64),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            this.Controls.Add(label);
            this.Controls.Add(textBox);
            yPos += spacing;
        }
    }
}