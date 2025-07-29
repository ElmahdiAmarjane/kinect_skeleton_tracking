namespace kinectProject
{
    partial class BodyPictureAnalyzer
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btnImport;
        private System.Windows.Forms.Panel panelMeasurements;
        private System.Windows.Forms.ListBox lstMeasurements;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnSwitchMode;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.Label lblMode;
        private System.Windows.Forms.Button btnCalculate;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Button btnSetReferenceScale;

        private System.Windows.Forms.Button btnTakePhoto;
        private System.Windows.Forms.Button btnDone;


        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panelMeasurements = new System.Windows.Forms.Panel();
            this.lstMeasurements = new System.Windows.Forms.ListBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.btnImport = new System.Windows.Forms.Button();
            this.btnSwitchMode = new System.Windows.Forms.Button();
            this.btnCalculate = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.lblMode = new System.Windows.Forms.Label();
            this.btnSetReferenceScale = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.panelMeasurements.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 60);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(800, 480);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBox1_Paint);
            this.pictureBox1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseClick);
            // 
            // panelMeasurements
            // 
            this.panelMeasurements.Controls.Add(this.lstMeasurements);
            this.panelMeasurements.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelMeasurements.Location = new System.Drawing.Point(0, 540);
            this.panelMeasurements.Name = "panelMeasurements";
            this.panelMeasurements.Size = new System.Drawing.Size(800, 100);
            this.panelMeasurements.TabIndex = 1;
            // 
            // lstMeasurements
            // 
            this.lstMeasurements.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.lstMeasurements.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lstMeasurements.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstMeasurements.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lstMeasurements.ForeColor = System.Drawing.Color.White;
            this.lstMeasurements.FormattingEnabled = true;
            this.lstMeasurements.Location = new System.Drawing.Point(0, 0);
            this.lstMeasurements.Name = "lstMeasurements";
            this.lstMeasurements.ScrollAlwaysVisible = true;
            this.lstMeasurements.Size = new System.Drawing.Size(800, 100);
            this.lstMeasurements.TabIndex = 0;
            // 
            // statusStrip1
            // 
            this.statusStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 640);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(800, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.ForeColor = System.Drawing.Color.White;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(133, 17);
            this.toolStripStatusLabel1.Text = "Ready for measurement";
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.btnImport);
            this.panelTop.Controls.Add(this.btnSwitchMode);
            this.panelTop.Controls.Add(this.btnCalculate);
            this.panelTop.Controls.Add(this.btnClear);
            this.panelTop.Controls.Add(this.lblMode);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(800, 60);
            this.panelTop.TabIndex = 3;
            this.panelTop.Paint += new System.Windows.Forms.PaintEventHandler(this.panelTop_Paint);
            // 
            // btnImport
            // 
            this.btnImport.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(136)))));
            this.btnImport.Dock = System.Windows.Forms.DockStyle.Top;
            this.btnImport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnImport.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnImport.ForeColor = System.Drawing.Color.White;
            this.btnImport.Location = new System.Drawing.Point(0, 0);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new System.Drawing.Size(800, 30);
            this.btnImport.TabIndex = 0;
            this.btnImport.Text = "IMPORT BODY PICTURE";
            this.btnImport.UseVisualStyleBackColor = false;
            this.btnImport.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // btnSwitchMode
            // 
            this.btnSwitchMode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(106)))), ((int)(((byte)(27)))), ((int)(((byte)(154)))));
            this.btnSwitchMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSwitchMode.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnSwitchMode.ForeColor = System.Drawing.Color.White;
            this.btnSwitchMode.Location = new System.Drawing.Point(10, 35);
            this.btnSwitchMode.Name = "btnSwitchMode";
            this.btnSwitchMode.Size = new System.Drawing.Size(120, 25);
            this.btnSwitchMode.TabIndex = 1;
            this.btnSwitchMode.Text = "SWITCH MODE";
            this.btnSwitchMode.UseVisualStyleBackColor = false;
            this.btnSwitchMode.Click += new System.EventHandler(this.btnSwitchMode_Click);
            // 
            // btnCalculate
            // 
            this.btnCalculate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnCalculate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCalculate.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnCalculate.ForeColor = System.Drawing.Color.White;
            this.btnCalculate.Location = new System.Drawing.Point(140, 35);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(120, 25);
            this.btnCalculate.TabIndex = 2;
            this.btnCalculate.Text = "CALCULATE";
            this.btnCalculate.UseVisualStyleBackColor = false;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // btnClear
            // 
            this.btnClear.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.btnClear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClear.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnClear.ForeColor = System.Drawing.Color.White;
            this.btnClear.Location = new System.Drawing.Point(270, 35);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(120, 25);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "CLEAR ALL";
            this.btnClear.UseVisualStyleBackColor = false;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // lblMode
            // 
            this.lblMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMode.AutoSize = true;
            this.lblMode.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold);
            this.lblMode.ForeColor = System.Drawing.Color.White;
            this.lblMode.Location = new System.Drawing.Point(600, 40);
            this.lblMode.Name = "lblMode";
            this.lblMode.Size = new System.Drawing.Size(88, 13);
            this.lblMode.TabIndex = 4;
            this.lblMode.Text = "Mode: Distance";
            // 
            // btnSetReferenceScale
            // 
            this.btnSetReferenceScale.BackColor = System.Drawing.Color.FromArgb(64, 64, 64); // Dark gray
            this.btnSetReferenceScale.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSetReferenceScale.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnSetReferenceScale.ForeColor = System.Drawing.Color.White;
            this.btnSetReferenceScale.Location = new System.Drawing.Point(400, 35); // Adjust to avoid overlap
            this.btnSetReferenceScale.Name = "btnSetReferenceScale";
            this.btnSetReferenceScale.Size = new System.Drawing.Size(160, 25);
            this.btnSetReferenceScale.TabIndex = 4;
            this.btnSetReferenceScale.Text = "SET REFERENCE SCALE";
            this.btnSetReferenceScale.UseVisualStyleBackColor = false;
            this.btnSetReferenceScale.Click += new System.EventHandler(this.btnSetReferenceScale_Click);
            //
            // btnTakePhoto
            this.btnTakePhoto = new System.Windows.Forms.Button();
            this.btnTakePhoto.BackColor = System.Drawing.Color.FromArgb(0, 123, 255);
            this.btnTakePhoto.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTakePhoto.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnTakePhoto.ForeColor = System.Drawing.Color.White;
            this.btnTakePhoto.Location = new System.Drawing.Point(570, 35);
            this.btnTakePhoto.Name = "btnTakePhoto";
            this.btnTakePhoto.Size = new System.Drawing.Size(100, 25);
            this.btnTakePhoto.TabIndex = 5;
            this.btnTakePhoto.Text = "TAKE PHOTO";
            this.btnTakePhoto.UseVisualStyleBackColor = false;
            this.btnTakePhoto.Click += new System.EventHandler(this.btnTakePhoto_Click);
            this.panelTop.Controls.Add(this.btnTakePhoto);

            // btnDone (hidden initially)
            this.btnDone = new System.Windows.Forms.Button();
            this.btnDone.BackColor = System.Drawing.Color.SeaGreen;
            this.btnDone.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDone.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.btnDone.ForeColor = System.Drawing.Color.White;
            this.btnDone.Location = new System.Drawing.Point(680, 35);
            this.btnDone.Name = "btnDone";
            this.btnDone.Size = new System.Drawing.Size(100, 25);
            this.btnDone.TabIndex = 6;
            this.btnDone.Text = "DONE";
            this.btnDone.Visible = false;
            this.btnDone.Click += new System.EventHandler(this.btnDone_Click);
            this.panelTop.Controls.Add(this.btnDone);


            // 
            // BodyPictureAnalyzer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.ClientSize = new System.Drawing.Size(800, 662);
            this.Controls.Add(this.btnSetReferenceScale);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.panelMeasurements);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.statusStrip1);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.ForeColor = System.Drawing.Color.White;
            this.MinimumSize = new System.Drawing.Size(600, 500);
            this.Name = "BodyPictureAnalyzer";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Professional Body Measurement Analyzer";
            this.Load += new System.EventHandler(this.BodyPictureAnalyzer_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.panelMeasurements.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }
}