using System;
using System.Windows.Forms;

namespace KinectProject
{
    public class PdfInputForm : Form
    {
        public string PatientName => nameTextBox.Text;
        public string PatientAge => ageTextBox.Text;
        public string PatientSex => sexComboBox.SelectedItem?.ToString();
        public DateTime PatientBirthDate => birthDatePicker.Value;
        public string MedicalRecordNumber => recordNumberTextBox.Text;
        public string MedicalHistory => historyTextBox.Text;

        private TextBox nameTextBox;
        private TextBox ageTextBox;
        private ComboBox sexComboBox;
        private DateTimePicker birthDatePicker;
        private TextBox recordNumberTextBox;
        private TextBox historyTextBox;
        private Button generateButton;
        private Button cancelButton;

        public PdfInputForm()
        {
            this.Text = "Informations du patient";
            this.Size = new System.Drawing.Size(400, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int leftLabel = 20;
            int leftInput = 150;
            int top = 20;
            int spacing = 35;

            Label nameLabel = new Label() { Text = "Nom :", Left = leftLabel, Top = top, Width = 120 };
            nameTextBox = new TextBox() { Left = leftInput, Top = top, Width = 200 };
            top += spacing;

            Label ageLabel = new Label() { Text = "Âge :", Left = leftLabel, Top = top, Width = 120 };
            ageTextBox = new TextBox() { Left = leftInput, Top = top, Width = 200 };
            top += spacing;

            Label sexLabel = new Label() { Text = "Sexe :", Left = leftLabel, Top = top, Width = 120 };
            sexComboBox = new ComboBox() { Left = leftInput, Top = top, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            sexComboBox.Items.AddRange(new string[] { "Homme", "Femme", "Autre" });
            top += spacing;

            Label birthLabel = new Label() { Text = "Date de naissance :", Left = leftLabel, Top = top, Width = 120 };
            birthDatePicker = new DateTimePicker() { Left = leftInput, Top = top, Width = 200, Format = DateTimePickerFormat.Short };
            top += spacing;

            Label recordLabel = new Label() { Text = "N° dossier médical :", Left = leftLabel, Top = top, Width = 120 };
            recordNumberTextBox = new TextBox() { Left = leftInput, Top = top, Width = 200 };
            top += spacing;

            Label historyLabel = new Label() { Text = "Antécédents médicaux :", Left = leftLabel, Top = top, Width = 130 };
            historyTextBox = new TextBox() { Left = leftInput, Top = top, Width = 200, Height = 60, Multiline = true };
            top += 70;

            generateButton = new Button() { Text = "Générer", Left = 100, Top = top, Width = 100 };
            cancelButton = new Button() { Text = "Annuler", Left = 210, Top = top, Width = 100 };

            generateButton.Click += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text) || string.IsNullOrWhiteSpace(ageTextBox.Text))
                {
                    MessageBox.Show("Veuillez remplir les champs obligatoires (Nom, Âge).", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            cancelButton.Click += (sender, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(nameLabel);
            this.Controls.Add(nameTextBox);
            this.Controls.Add(ageLabel);
            this.Controls.Add(ageTextBox);
            this.Controls.Add(sexLabel);
            this.Controls.Add(sexComboBox);
            this.Controls.Add(birthLabel);
            this.Controls.Add(birthDatePicker);
            this.Controls.Add(recordLabel);
            this.Controls.Add(recordNumberTextBox);
            this.Controls.Add(historyLabel);
            this.Controls.Add(historyTextBox);
            this.Controls.Add(generateButton);
            this.Controls.Add(cancelButton);
        }
    }
}
