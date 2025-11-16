namespace Operations
{
    partial class ConvertAD
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConvertAD));
            this.label1 = new System.Windows.Forms.Label();
            this.btnConvert = new System.Windows.Forms.Button();
            this.trackBarThreshLow = new System.Windows.Forms.TrackBar();
            this.lblThresholdHigh = new System.Windows.Forms.Label();
            this.lblThresholdLow = new System.Windows.Forms.Label();
            this.trackBarThresHigh = new System.Windows.Forms.TrackBar();
            this.comboMethod = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textNewName = new System.Windows.Forms.TextBox();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnConvertClose = new System.Windows.Forms.Button();
            this.radioOtherChannel = new System.Windows.Forms.RadioButton();
            this.radioSameChannel = new System.Windows.Forms.RadioButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.radioAllChannels = new System.Windows.Forms.RadioButton();
            this.radioOneChannel = new System.Windows.Forms.RadioButton();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarThreshLow)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarThresHigh)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(34, 103);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(0, 13);
            this.label1.TabIndex = 0;
            // 
            // btnConvert
            // 
            this.btnConvert.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnConvert.ForeColor = System.Drawing.Color.Black;
            this.btnConvert.Location = new System.Drawing.Point(211, 298);
            this.btnConvert.Name = "btnConvert";
            this.btnConvert.Size = new System.Drawing.Size(75, 23);
            this.btnConvert.TabIndex = 5;
            this.btnConvert.Text = "Convert";
            this.btnConvert.UseVisualStyleBackColor = true;
            this.btnConvert.Click += new System.EventHandler(this.btnConvert_Click);
            // 
            // trackBarThreshLow
            // 
            this.trackBarThreshLow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.trackBarThreshLow.Location = new System.Drawing.Point(12, 186);
            this.trackBarThreshLow.Maximum = 100;
            this.trackBarThreshLow.Name = "trackBarThreshLow";
            this.trackBarThreshLow.Size = new System.Drawing.Size(584, 45);
            this.trackBarThreshLow.TabIndex = 10;
            this.trackBarThreshLow.TickFrequency = 10;
            this.trackBarThreshLow.Value = 48;
            this.trackBarThreshLow.Scroll += new System.EventHandler(this.trackLow_Scroll);
            // 
            // lblThresholdHigh
            // 
            this.lblThresholdHigh.AutoSize = true;
            this.lblThresholdHigh.Location = new System.Drawing.Point(17, 116);
            this.lblThresholdHigh.Name = "lblThresholdHigh";
            this.lblThresholdHigh.Size = new System.Drawing.Size(82, 13);
            this.lblThresholdHigh.TabIndex = 11;
            this.lblThresholdHigh.Text = "Threshold High:";
            // 
            // lblThresholdLow
            // 
            this.lblThresholdLow.AutoSize = true;
            this.lblThresholdLow.Location = new System.Drawing.Point(17, 170);
            this.lblThresholdLow.Name = "lblThresholdLow";
            this.lblThresholdLow.Size = new System.Drawing.Size(80, 13);
            this.lblThresholdLow.TabIndex = 13;
            this.lblThresholdLow.Text = "Threshold Low:";
            // 
            // trackBarThresHigh
            // 
            this.trackBarThresHigh.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.trackBarThresHigh.Location = new System.Drawing.Point(12, 132);
            this.trackBarThresHigh.Maximum = 100;
            this.trackBarThresHigh.Name = "trackBarThresHigh";
            this.trackBarThresHigh.Size = new System.Drawing.Size(584, 45);
            this.trackBarThresHigh.TabIndex = 12;
            this.trackBarThresHigh.TickFrequency = 10;
            this.trackBarThresHigh.Value = 52;
            this.trackBarThresHigh.Scroll += new System.EventHandler(this.trackHigh_Scroll);
            // 
            // comboMethod
            // 
            this.comboMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMethod.FormattingEnabled = true;
            this.comboMethod.Location = new System.Drawing.Point(126, 60);
            this.comboMethod.Name = "comboMethod";
            this.comboMethod.Size = new System.Drawing.Size(263, 21);
            this.comboMethod.TabIndex = 15;
            this.comboMethod.SelectedIndexChanged += new System.EventHandler(this.comboMethod_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 63);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(102, 13);
            this.label3.TabIndex = 14;
            this.label3.Text = "Conversion Method:";
            // 
            // textNewName
            // 
            this.textNewName.Enabled = false;
            this.textNewName.Location = new System.Drawing.Point(213, 263);
            this.textNewName.Name = "textNewName";
            this.textNewName.Size = new System.Drawing.Size(176, 20);
            this.textNewName.TabIndex = 18;
            // 
            // lblHint
            // 
            this.lblHint.AutoSize = true;
            this.lblHint.ForeColor = System.Drawing.Color.Lime;
            this.lblHint.Location = new System.Drawing.Point(18, 88);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(33, 13);
            this.lblHint.TabIndex = 111;
            this.lblHint.Text = "HINT";
            // 
            // btnConvertClose
            // 
            this.btnConvertClose.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnConvertClose.ForeColor = System.Drawing.Color.Black;
            this.btnConvertClose.Location = new System.Drawing.Point(295, 298);
            this.btnConvertClose.Name = "btnConvertClose";
            this.btnConvertClose.Size = new System.Drawing.Size(115, 23);
            this.btnConvertClose.TabIndex = 112;
            this.btnConvertClose.Text = "Convert and Close";
            this.btnConvertClose.UseVisualStyleBackColor = true;
            this.btnConvertClose.Click += new System.EventHandler(this.btnConvertClose_Click);
            // 
            // radioOtherChannel
            // 
            this.radioOtherChannel.AutoSize = true;
            this.radioOtherChannel.Location = new System.Drawing.Point(21, 264);
            this.radioOtherChannel.Name = "radioOtherChannel";
            this.radioOtherChannel.Size = new System.Drawing.Size(190, 17);
            this.radioOtherChannel.TabIndex = 113;
            this.radioOtherChannel.Text = "Save digital data  to other channel:";
            this.radioOtherChannel.UseVisualStyleBackColor = true;
            this.radioOtherChannel.CheckedChanged += new System.EventHandler(this.radioOtherChannel_CheckedChanged);
            // 
            // radioSameChannel
            // 
            this.radioSameChannel.AutoSize = true;
            this.radioSameChannel.Checked = true;
            this.radioSameChannel.Location = new System.Drawing.Point(21, 237);
            this.radioSameChannel.Name = "radioSameChannel";
            this.radioSameChannel.Size = new System.Drawing.Size(185, 17);
            this.radioSameChannel.TabIndex = 114;
            this.radioSameChannel.TabStop = true;
            this.radioSameChannel.Text = "Save digital data to same channel";
            this.radioSameChannel.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.radioAllChannels);
            this.panel1.Controls.Add(this.radioOneChannel);
            this.panel1.Location = new System.Drawing.Point(12, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(579, 50);
            this.panel1.TabIndex = 115;
            // 
            // radioAllChannels
            // 
            this.radioAllChannels.AutoSize = true;
            this.radioAllChannels.Location = new System.Drawing.Point(9, 26);
            this.radioAllChannels.Name = "radioAllChannels";
            this.radioAllChannels.Size = new System.Drawing.Size(156, 17);
            this.radioAllChannels.TabIndex = 1;
            this.radioAllChannels.Text = "Convert all analog channels";
            this.radioAllChannels.UseVisualStyleBackColor = true;
            this.radioAllChannels.CheckedChanged += new System.EventHandler(this.radioAllChannels_CheckedChanged);
            // 
            // radioOneChannel
            // 
            this.radioOneChannel.AutoSize = true;
            this.radioOneChannel.Checked = true;
            this.radioOneChannel.Location = new System.Drawing.Point(9, 7);
            this.radioOneChannel.Name = "radioOneChannel";
            this.radioOneChannel.Size = new System.Drawing.Size(131, 17);
            this.radioOneChannel.TabIndex = 0;
            this.radioOneChannel.TabStop = true;
            this.radioOneChannel.Text = "Convert Channel  XYZ";
            this.radioOneChannel.UseVisualStyleBackColor = true;
            // 
            // linkHelp
            // 
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(398, 63);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 116;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // ConvertAD
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(603, 333);
            this.Controls.Add(this.linkHelp);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.radioSameChannel);
            this.Controls.Add(this.radioOtherChannel);
            this.Controls.Add(this.btnConvertClose);
            this.Controls.Add(this.lblHint);
            this.Controls.Add(this.textNewName);
            this.Controls.Add(this.comboMethod);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblThresholdLow);
            this.Controls.Add(this.trackBarThresHigh);
            this.Controls.Add(this.lblThresholdHigh);
            this.Controls.Add(this.trackBarThreshLow);
            this.Controls.Add(this.btnConvert);
            this.Controls.Add(this.label1);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConvertAD";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Convert Analog / Digital - One Channel";
            ((System.ComponentModel.ISupportInitialize)(this.trackBarThreshLow)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarThresHigh)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnConvert;
        private System.Windows.Forms.TrackBar trackBarThreshLow;
        private System.Windows.Forms.Label lblThresholdHigh;
        private System.Windows.Forms.Label lblThresholdLow;
        private System.Windows.Forms.TrackBar trackBarThresHigh;
        private System.Windows.Forms.ComboBox comboMethod;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textNewName;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnConvertClose;
        private System.Windows.Forms.RadioButton radioOtherChannel;
        private System.Windows.Forms.RadioButton radioSameChannel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton radioAllChannels;
        private System.Windows.Forms.RadioButton radioOneChannel;
        private System.Windows.Forms.LinkLabel linkHelp;
    }
}