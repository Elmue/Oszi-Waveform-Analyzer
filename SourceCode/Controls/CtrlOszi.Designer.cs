namespace OsziWaveformAnalyzer
{
    partial class CtrlOszi
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.trackDigitalHeight = new System.Windows.Forms.TrackBar();
            this.trackAnalogHeight = new System.Windows.Forms.TrackBar();
            this.lblDigitalHeight = new System.Windows.Forms.Label();
            this.lblAnalogHeight = new System.Windows.Forms.Label();
            this.checkSepChannels = new System.Windows.Forms.CheckBox();
            this.checkLegend = new System.Windows.Forms.CheckBox();
            this.groupBox = new System.Windows.Forms.GroupBox();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.lblDispSamples = new System.Windows.Forms.Label();
            this.lblFactorHint = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboFactor = new OsziWaveformAnalyzer.ComboBoxEx();
            ((System.ComponentModel.ISupportInitialize)(this.trackDigitalHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackAnalogHeight)).BeginInit();
            this.groupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // trackDigitalHeight
            // 
            this.trackDigitalHeight.BackColor = System.Drawing.Color.DimGray;
            this.trackDigitalHeight.Location = new System.Drawing.Point(123, 36);
            this.trackDigitalHeight.Name = "trackDigitalHeight";
            this.trackDigitalHeight.Size = new System.Drawing.Size(188, 45);
            this.trackDigitalHeight.TabIndex = 105;
            this.trackDigitalHeight.TickStyle = System.Windows.Forms.TickStyle.None;
            this.trackDigitalHeight.Scroll += new System.EventHandler(this.trackDigitalHeight_Scroll);
            // 
            // trackAnalogHeight
            // 
            this.trackAnalogHeight.BackColor = System.Drawing.Color.DimGray;
            this.trackAnalogHeight.Location = new System.Drawing.Point(123, 10);
            this.trackAnalogHeight.Name = "trackAnalogHeight";
            this.trackAnalogHeight.Size = new System.Drawing.Size(188, 45);
            this.trackAnalogHeight.TabIndex = 104;
            this.trackAnalogHeight.TickStyle = System.Windows.Forms.TickStyle.None;
            this.trackAnalogHeight.Scroll += new System.EventHandler(this.trackAnalogHeight_Scroll);
            // 
            // lblDigitalHeight
            // 
            this.lblDigitalHeight.AutoSize = true;
            this.lblDigitalHeight.ForeColor = System.Drawing.Color.White;
            this.lblDigitalHeight.Location = new System.Drawing.Point(310, 39);
            this.lblDigitalHeight.Name = "lblDigitalHeight";
            this.lblDigitalHeight.Size = new System.Drawing.Size(39, 13);
            this.lblDigitalHeight.TabIndex = 107;
            this.lblDigitalHeight.Text = "Digital:";
            // 
            // lblAnalogHeight
            // 
            this.lblAnalogHeight.AutoSize = true;
            this.lblAnalogHeight.ForeColor = System.Drawing.Color.White;
            this.lblAnalogHeight.Location = new System.Drawing.Point(310, 14);
            this.lblAnalogHeight.Name = "lblAnalogHeight";
            this.lblAnalogHeight.Size = new System.Drawing.Size(43, 13);
            this.lblAnalogHeight.TabIndex = 106;
            this.lblAnalogHeight.Text = "Analog:";
            // 
            // checkSepChannels
            // 
            this.checkSepChannels.AutoSize = true;
            this.checkSepChannels.ForeColor = System.Drawing.Color.White;
            this.checkSepChannels.Location = new System.Drawing.Point(197, 80);
            this.checkSepChannels.Name = "checkSepChannels";
            this.checkSepChannels.Size = new System.Drawing.Size(152, 17);
            this.checkSepChannels.TabIndex = 108;
            this.checkSepChannels.Text = "Separate Analog Channels";
            this.checkSepChannels.UseVisualStyleBackColor = true;
            this.checkSepChannels.CheckedChanged += new System.EventHandler(this.checkSepChannels_CheckedChanged);
            // 
            // checkLegend
            // 
            this.checkLegend.AutoSize = true;
            this.checkLegend.ForeColor = System.Drawing.Color.White;
            this.checkLegend.Location = new System.Drawing.Point(131, 80);
            this.checkLegend.Name = "checkLegend";
            this.checkLegend.Size = new System.Drawing.Size(62, 17);
            this.checkLegend.TabIndex = 109;
            this.checkLegend.Text = "Legend";
            this.checkLegend.UseVisualStyleBackColor = true;
            this.checkLegend.CheckedChanged += new System.EventHandler(this.checkLegend_CheckedChanged);
            // 
            // groupBox
            // 
            this.groupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox.Controls.Add(this.linkHelp);
            this.groupBox.Controls.Add(this.lblDispSamples);
            this.groupBox.Controls.Add(this.lblFactorHint);
            this.groupBox.Controls.Add(this.comboFactor);
            this.groupBox.Controls.Add(this.label2);
            this.groupBox.Controls.Add(this.trackDigitalHeight);
            this.groupBox.Controls.Add(this.checkSepChannels);
            this.groupBox.Controls.Add(this.checkLegend);
            this.groupBox.Controls.Add(this.trackAnalogHeight);
            this.groupBox.Controls.Add(this.lblAnalogHeight);
            this.groupBox.Controls.Add(this.lblDigitalHeight);
            this.groupBox.Location = new System.Drawing.Point(0, 0);
            this.groupBox.Name = "groupBox";
            this.groupBox.Size = new System.Drawing.Size(395, 102);
            this.groupBox.TabIndex = 110;
            this.groupBox.TabStop = false;
            // 
            // linkHelp
            // 
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(310, 59);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 111;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // lblDispSamples
            // 
            this.lblDispSamples.AutoSize = true;
            this.lblDispSamples.ForeColor = System.Drawing.Color.White;
            this.lblDispSamples.Location = new System.Drawing.Point(7, 50);
            this.lblDispSamples.Name = "lblDispSamples";
            this.lblDispSamples.Size = new System.Drawing.Size(50, 13);
            this.lblDispSamples.TabIndex = 113;
            this.lblDispSamples.Text = "Samples:";
            // 
            // lblFactorHint
            // 
            this.lblFactorHint.ForeColor = System.Drawing.Color.PeachPuff;
            this.lblFactorHint.Location = new System.Drawing.Point(7, 68);
            this.lblFactorHint.Name = "lblFactorHint";
            this.lblFactorHint.Size = new System.Drawing.Size(108, 30);
            this.lblFactorHint.TabIndex = 112;
            this.lblFactorHint.Text = "UseArrow up/down\r to change Factor";
            this.lblFactorHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(7, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 13);
            this.label2.TabIndex = 110;
            this.label2.Text = "Display &Factor:";
            // 
            // comboFactor
            // 
            this.comboFactor.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboFactor.DropDownHeight = 500;
            this.comboFactor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboFactor.FormattingEnabled = true;
            this.comboFactor.IntegralHeight = false;
            this.comboFactor.ItemHeight = 15;
            this.comboFactor.Location = new System.Drawing.Point(8, 27);
            this.comboFactor.MaxDropDownItems = 40;
            this.comboFactor.Name = "comboFactor";
            this.comboFactor.Size = new System.Drawing.Size(108, 21);
            this.comboFactor.TabIndex = 111;
            // 
            // CtrlOszi
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.Controls.Add(this.groupBox);
            this.Name = "CtrlOszi";
            this.Size = new System.Drawing.Size(403, 111);
            ((System.ComponentModel.ISupportInitialize)(this.trackDigitalHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackAnalogHeight)).EndInit();
            this.groupBox.ResumeLayout(false);
            this.groupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TrackBar trackDigitalHeight;
        private System.Windows.Forms.TrackBar trackAnalogHeight;
        private System.Windows.Forms.Label lblDigitalHeight;
        private System.Windows.Forms.Label lblAnalogHeight;
        private System.Windows.Forms.CheckBox checkSepChannels;
        private System.Windows.Forms.CheckBox checkLegend;
        private System.Windows.Forms.GroupBox groupBox;
        private System.Windows.Forms.Label lblDispSamples;
        private System.Windows.Forms.Label lblFactorHint;
        private ComboBoxEx comboFactor;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.LinkLabel linkHelp;

    }
}
