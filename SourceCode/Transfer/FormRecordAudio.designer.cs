namespace Transfer
{
    partial class FormRecordAudio
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormRecordAudio));
            this.comboDevice = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.comboSamplerate = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblLimit = new System.Windows.Forms.Label();
            this.btnStartStop = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBits = new System.Windows.Forms.ComboBox();
            this.panelVU = new OsziWaveformAnalyzer.VUMeter();
            this.label5 = new System.Windows.Forms.Label();
            this.comboChannels = new System.Windows.Forms.ComboBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboDevice
            // 
            this.comboDevice.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDevice.FormattingEnabled = true;
            this.comboDevice.Location = new System.Drawing.Point(12, 26);
            this.comboDevice.Name = "comboDevice";
            this.comboDevice.Size = new System.Drawing.Size(260, 21);
            this.comboDevice.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Audio Device:";
            // 
            // comboSamplerate
            // 
            this.comboSamplerate.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSamplerate.FormattingEnabled = true;
            this.comboSamplerate.Items.AddRange(new object[] {
            "16.000",
            "32.000",
            "44.100",
            "48.000",
            "96.000",
            "128.000",
            "192.000",
            "256.000",
            "384.000",
            "512.000",
            "768.000"});
            this.comboSamplerate.Location = new System.Drawing.Point(12, 73);
            this.comboSamplerate.Name = "comboSamplerate";
            this.comboSamplerate.Size = new System.Drawing.Size(74, 21);
            this.comboSamplerate.TabIndex = 2;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 56);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Samplerate:";
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 235);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(313, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 7;
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = false;
            this.statusLabel.BackColor = System.Drawing.SystemColors.Control;
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.statusLabel.ForeColor = System.Drawing.Color.Black;
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(520, 17);
            this.statusLabel.Text = "Status";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLimit
            // 
            this.lblLimit.AutoSize = true;
            this.lblLimit.ForeColor = System.Drawing.Color.Yellow;
            this.lblLimit.Location = new System.Drawing.Point(13, 213);
            this.lblLimit.Name = "lblLimit";
            this.lblLimit.Size = new System.Drawing.Size(151, 13);
            this.lblLimit.TabIndex = 8;
            this.lblLimit.Text = "The recording is limited to XXX";
            // 
            // btnStartStop
            // 
            this.btnStartStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStartStop.ForeColor = System.Drawing.Color.Black;
            this.btnStartStop.Location = new System.Drawing.Point(226, 72);
            this.btnStartStop.Name = "btnStartStop";
            this.btnStartStop.Size = new System.Drawing.Size(75, 23);
            this.btnStartStop.TabIndex = 5;
            this.btnStartStop.Text = "Start";
            this.btnStartStop.UseVisualStyleBackColor = true;
            this.btnStartStop.Click += new System.EventHandler(this.btnStartStop_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ForeColor = System.Drawing.Color.Yellow;
            this.label3.Location = new System.Drawing.Point(12, 102);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(276, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "If an audio format is not supported, it may be interpolated.";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(92, 56);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(27, 13);
            this.label4.TabIndex = 13;
            this.label4.Text = "Bits:";
            // 
            // comboBits
            // 
            this.comboBits.AutoCompleteCustomSource.AddRange(new string[] {
            "8 bit",
            "16 bit",
            "24 bit"});
            this.comboBits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBits.FormattingEnabled = true;
            this.comboBits.Items.AddRange(new object[] {
            "8 bit",
            "16 bit",
            "24 bit"});
            this.comboBits.Location = new System.Drawing.Point(92, 73);
            this.comboBits.Name = "comboBits";
            this.comboBits.Size = new System.Drawing.Size(50, 21);
            this.comboBits.TabIndex = 3;
            // 
            // panelVU
            // 
            this.panelVU.AutoSize = true;
            this.panelVU.BackColor = System.Drawing.Color.DimGray;
            this.panelVU.Location = new System.Drawing.Point(13, 124);
            this.panelVU.Name = "panelVU";
            this.panelVU.Size = new System.Drawing.Size(288, 85);
            this.panelVU.TabIndex = 10;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(148, 56);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(54, 13);
            this.label5.TabIndex = 15;
            this.label5.Text = "Channels:";
            // 
            // comboChannels
            // 
            this.comboChannels.AutoCompleteCustomSource.AddRange(new string[] {
            "8 bit",
            "16 bit",
            "24 bit"});
            this.comboChannels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboChannels.FormattingEnabled = true;
            this.comboChannels.Items.AddRange(new object[] {
            "1 (Mono)",
            "2 (Stereo)",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8"});
            this.comboChannels.Location = new System.Drawing.Point(148, 73);
            this.comboChannels.Name = "comboChannels";
            this.comboChannels.Size = new System.Drawing.Size(72, 21);
            this.comboChannels.TabIndex = 14;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.ForeColor = System.Drawing.Color.Black;
            this.btnRefresh.Image = ((System.Drawing.Image)(resources.GetObject("btnRefresh.Image")));
            this.btnRefresh.Location = new System.Drawing.Point(277, 25);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(24, 24);
            this.btnRefresh.TabIndex = 16;
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // FormRecordAudio
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(313, 257);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.comboChannels);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.comboBits);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.panelVU);
            this.Controls.Add(this.btnStartStop);
            this.Controls.Add(this.lblLimit);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.comboSamplerate);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.comboDevice);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormRecordAudio";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Audio Recording";
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboDevice;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboSamplerate;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Label lblLimit;
        private System.Windows.Forms.Button btnStartStop;
        private OsziWaveformAnalyzer.VUMeter panelVU;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBits;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboChannels;
        private System.Windows.Forms.Button btnRefresh;

    }
}