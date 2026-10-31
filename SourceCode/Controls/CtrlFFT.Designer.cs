namespace OsziWaveformAnalyzer
{
    partial class CtrlFFT
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
            this.checkLog = new System.Windows.Forms.CheckBox();
            this.checkDC = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnPeaks = new System.Windows.Forms.Button();
            this.groupBox = new System.Windows.Forms.GroupBox();
            this.checkNoise = new System.Windows.Forms.CheckBox();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.comboWindow = new OsziWaveformAnalyzer.ComboBoxEx();
            this.comboScale = new OsziWaveformAnalyzer.ComboBoxEx();
            this.groupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // checkLog
            // 
            this.checkLog.AutoSize = true;
            this.checkLog.ForeColor = System.Drawing.Color.White;
            this.checkLog.Location = new System.Drawing.Point(172, 19);
            this.checkLog.Name = "checkLog";
            this.checkLog.Size = new System.Drawing.Size(90, 17);
            this.checkLog.TabIndex = 2;
            this.checkLog.Text = "Logarithmic X";
            this.checkLog.UseVisualStyleBackColor = true;
            this.checkLog.CheckedChanged += new System.EventHandler(this.checkLog_CheckedChanged);
            // 
            // checkDC
            // 
            this.checkDC.AutoSize = true;
            this.checkDC.ForeColor = System.Drawing.Color.White;
            this.checkDC.Location = new System.Drawing.Point(172, 38);
            this.checkDC.Name = "checkDC";
            this.checkDC.Size = new System.Drawing.Size(84, 17);
            this.checkDC.TabIndex = 3;
            this.checkDC.Text = "Remove DC";
            this.checkDC.UseVisualStyleBackColor = true;
            this.checkDC.CheckedChanged += new System.EventHandler(this.checkDC_CheckedChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(10, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(49, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Window:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(9, 52);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Scale Y:";
            // 
            // btnPeaks
            // 
            this.btnPeaks.ForeColor = System.Drawing.Color.Black;
            this.btnPeaks.Location = new System.Drawing.Point(98, 67);
            this.btnPeaks.Name = "btnPeaks";
            this.btnPeaks.Size = new System.Drawing.Size(61, 23);
            this.btnPeaks.TabIndex = 6;
            this.btnPeaks.Text = "Peaks";
            this.btnPeaks.UseVisualStyleBackColor = true;
            this.btnPeaks.Click += new System.EventHandler(this.btnPeaks_Click);
            // 
            // groupBox
            // 
            this.groupBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox.Controls.Add(this.checkNoise);
            this.groupBox.Controls.Add(this.linkHelp);
            this.groupBox.Controls.Add(this.comboWindow);
            this.groupBox.Controls.Add(this.btnPeaks);
            this.groupBox.Controls.Add(this.comboScale);
            this.groupBox.Controls.Add(this.label2);
            this.groupBox.Controls.Add(this.checkLog);
            this.groupBox.Controls.Add(this.label1);
            this.groupBox.Controls.Add(this.checkDC);
            this.groupBox.Location = new System.Drawing.Point(0, 0);
            this.groupBox.Name = "groupBox";
            this.groupBox.Size = new System.Drawing.Size(276, 102);
            this.groupBox.TabIndex = 7;
            this.groupBox.TabStop = false;
            // 
            // checkNoise
            // 
            this.checkNoise.AutoSize = true;
            this.checkNoise.ForeColor = System.Drawing.Color.White;
            this.checkNoise.Location = new System.Drawing.Point(172, 57);
            this.checkNoise.Name = "checkNoise";
            this.checkNoise.Size = new System.Drawing.Size(96, 17);
            this.checkNoise.TabIndex = 106;
            this.checkNoise.Text = "Remove Noise";
            this.checkNoise.UseVisualStyleBackColor = true;
            this.checkNoise.CheckedChanged += new System.EventHandler(this.checkNoise_CheckedChanged);
            // 
            // linkHelp
            // 
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(207, 77);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 105;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // comboWindow
            // 
            this.comboWindow.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboWindow.DropDownHeight = 500;
            this.comboWindow.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboWindow.FormattingEnabled = true;
            this.comboWindow.IntegralHeight = false;
            this.comboWindow.Location = new System.Drawing.Point(9, 27);
            this.comboWindow.Name = "comboWindow";
            this.comboWindow.Size = new System.Drawing.Size(122, 21);
            this.comboWindow.TabIndex = 0;
            this.comboWindow.SelectedIndexChanged += new System.EventHandler(this.comboWindow_SelectedIndexChanged);
            // 
            // comboScale
            // 
            this.comboScale.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboScale.DropDownHeight = 500;
            this.comboScale.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboScale.FormattingEnabled = true;
            this.comboScale.IntegralHeight = false;
            this.comboScale.Location = new System.Drawing.Point(9, 68);
            this.comboScale.Name = "comboScale";
            this.comboScale.Size = new System.Drawing.Size(82, 21);
            this.comboScale.TabIndex = 1;
            this.comboScale.SelectedIndexChanged += new System.EventHandler(this.comboScale_SelectedIndexChanged);
            // 
            // CtrlFFT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.Controls.Add(this.groupBox);
            this.Name = "CtrlFFT";
            this.Size = new System.Drawing.Size(286, 110);
            this.groupBox.ResumeLayout(false);
            this.groupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private OsziWaveformAnalyzer.ComboBoxEx comboWindow;
        private OsziWaveformAnalyzer.ComboBoxEx comboScale;
        private System.Windows.Forms.CheckBox checkLog;
        private System.Windows.Forms.CheckBox checkDC;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnPeaks;
        private System.Windows.Forms.GroupBox groupBox;
        private System.Windows.Forms.LinkLabel linkHelp;
        private System.Windows.Forms.CheckBox checkNoise;


    }
}
