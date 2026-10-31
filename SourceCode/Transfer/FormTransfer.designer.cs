namespace Transfer
{
    partial class FormTransfer
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormTransfer));
            this.comboDevices = new System.Windows.Forms.ComboBox();
            this.lblDevice1 = new System.Windows.Forms.Label();
            this.btnInstallDriver = new System.Windows.Forms.Button();
            this.btnSearch = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.label5 = new System.Windows.Forms.Label();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.radioRS232 = new System.Windows.Forms.RadioButton();
            this.comboRS232 = new System.Windows.Forms.ComboBox();
            this.groupCommand = new System.Windows.Forms.GroupBox();
            this.label9 = new System.Windows.Forms.Label();
            this.textResponse = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.textCommand = new System.Windows.Forms.TextBox();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.radioUSB = new System.Windows.Forms.RadioButton();
            this.radioTCP = new System.Windows.Forms.RadioButton();
            this.radioVXI = new System.Windows.Forms.RadioButton();
            this.textVxiLink = new System.Windows.Forms.TextBox();
            this.lblDevice3 = new System.Windows.Forms.Label();
            this.lblDevice2 = new System.Windows.Forms.Label();
            this.comboBaud = new System.Windows.Forms.ComboBox();
            this.groupDcCapture = new System.Windows.Forms.GroupBox();
            this.comboTotalTime = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnDcCapture = new System.Windows.Forms.Button();
            this.comboInterval = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.statusStrip.SuspendLayout();
            this.groupCommand.SuspendLayout();
            this.groupDcCapture.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboDevices
            // 
            this.comboDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDevices.FormattingEnabled = true;
            this.comboDevices.Location = new System.Drawing.Point(62, 25);
            this.comboDevices.Name = "comboDevices";
            this.comboDevices.Size = new System.Drawing.Size(128, 21);
            this.comboDevices.TabIndex = 10;
            // 
            // lblDevice1
            // 
            this.lblDevice1.AutoSize = true;
            this.lblDevice1.Location = new System.Drawing.Point(61, 9);
            this.lblDevice1.Name = "lblDevice1";
            this.lblDevice1.Size = new System.Drawing.Size(79, 13);
            this.lblDevice1.TabIndex = 1;
            this.lblDevice1.Text = "Label Device 1";
            // 
            // btnInstallDriver
            // 
            this.btnInstallDriver.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnInstallDriver.ForeColor = System.Drawing.Color.Black;
            this.btnInstallDriver.Location = new System.Drawing.Point(433, 24);
            this.btnInstallDriver.Name = "btnInstallDriver";
            this.btnInstallDriver.Size = new System.Drawing.Size(121, 23);
            this.btnInstallDriver.TabIndex = 60;
            this.btnInstallDriver.Text = "Install Driver (64 bit)";
            this.btnInstallDriver.UseVisualStyleBackColor = true;
            this.btnInstallDriver.Click += new System.EventHandler(this.btnInstallDriver_Click);
            // 
            // btnSearch
            // 
            this.btnSearch.ForeColor = System.Drawing.Color.Black;
            this.btnSearch.Location = new System.Drawing.Point(193, 24);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(56, 23);
            this.btnSearch.TabIndex = 20;
            this.btnSearch.Text = "Search";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.ForeColor = System.Drawing.Color.Black;
            this.btnOpen.Location = new System.Drawing.Point(305, 24);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(48, 23);
            this.btnOpen.TabIndex = 40;
            this.btnOpen.Text = "Open";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 261);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(566, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 6;
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
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(434, 9);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(105, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "IVI USB TMC Driver:";
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 32000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 100;
            // 
            // radioRS232
            // 
            this.radioRS232.AutoSize = true;
            this.radioRS232.Location = new System.Drawing.Point(14, 50);
            this.radioRS232.Name = "radioRS232";
            this.radioRS232.Size = new System.Drawing.Size(125, 17);
            this.radioRS232.TabIndex = 4;
            this.radioRS232.Text = "RS232 / Virtual COM";
            this.toolTip.SetToolTip(this.radioRS232, "RS232 serial port or USB virtual COM port (e.g. Hameg HO720 interface)");
            this.radioRS232.UseVisualStyleBackColor = true;
            this.radioRS232.CheckedChanged += new System.EventHandler(this.OnRadioButton_CheckedChanged);
            // 
            // comboRS232
            // 
            this.comboRS232.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRS232.FormattingEnabled = true;
            this.comboRS232.Location = new System.Drawing.Point(222, 25);
            this.comboRS232.Name = "comboRS232";
            this.comboRS232.Size = new System.Drawing.Size(79, 21);
            this.comboRS232.TabIndex = 35;
            this.toolTip.SetToolTip(this.comboRS232, "Baudrate, Databits, Parity (N,E,O), Stopbits, Handshake (RTS, XON, NONE)\nThese mu" +
                    "st match the settings in the interface menu of the oscilloscope.");
            // 
            // groupCommand
            // 
            this.groupCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupCommand.Controls.Add(this.label9);
            this.groupCommand.Controls.Add(this.textResponse);
            this.groupCommand.Controls.Add(this.btnSend);
            this.groupCommand.Controls.Add(this.label7);
            this.groupCommand.Controls.Add(this.textCommand);
            this.groupCommand.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.groupCommand.Location = new System.Drawing.Point(13, 133);
            this.groupCommand.Name = "groupCommand";
            this.groupCommand.Size = new System.Drawing.Size(541, 114);
            this.groupCommand.TabIndex = 100;
            this.groupCommand.TabStop = false;
            this.groupCommand.Text = "  Send SCPI Command Manually  ";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.ForeColor = System.Drawing.Color.White;
            this.label9.Location = new System.Drawing.Point(16, 63);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(58, 13);
            this.label9.TabIndex = 4;
            this.label9.Text = "Response:";
            // 
            // textResponse
            // 
            this.textResponse.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textResponse.Location = new System.Drawing.Point(17, 79);
            this.textResponse.Name = "textResponse";
            this.textResponse.Size = new System.Drawing.Size(511, 20);
            this.textResponse.TabIndex = 3;
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.ForeColor = System.Drawing.Color.Black;
            this.btnSend.Location = new System.Drawing.Point(464, 36);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(65, 23);
            this.btnSend.TabIndex = 2;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.ForeColor = System.Drawing.Color.White;
            this.label7.Location = new System.Drawing.Point(18, 21);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(84, 13);
            this.label7.TabIndex = 1;
            this.label7.Text = "SCPI Command:";
            // 
            // textCommand
            // 
            this.textCommand.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textCommand.Location = new System.Drawing.Point(17, 37);
            this.textCommand.Name = "textCommand";
            this.textCommand.Size = new System.Drawing.Size(441, 20);
            this.textCommand.TabIndex = 0;
            this.textCommand.Text = "*IDN?";
            // 
            // linkHelp
            // 
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(362, 28);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 50;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // radioUSB
            // 
            this.radioUSB.AutoSize = true;
            this.radioUSB.Location = new System.Drawing.Point(14, 5);
            this.radioUSB.Name = "radioUSB";
            this.radioUSB.Size = new System.Drawing.Size(47, 17);
            this.radioUSB.TabIndex = 1;
            this.radioUSB.Text = "USB";
            this.radioUSB.UseVisualStyleBackColor = true;
            this.radioUSB.CheckedChanged += new System.EventHandler(this.OnRadioButton_CheckedChanged);
            // 
            // radioTCP
            // 
            this.radioTCP.AutoSize = true;
            this.radioTCP.Location = new System.Drawing.Point(14, 35);
            this.radioTCP.Name = "radioTCP";
            this.radioTCP.Size = new System.Drawing.Size(46, 17);
            this.radioTCP.TabIndex = 3;
            this.radioTCP.Text = "TCP";
            this.radioTCP.UseVisualStyleBackColor = true;
            this.radioTCP.CheckedChanged += new System.EventHandler(this.OnRadioButton_CheckedChanged);
            // 
            // radioVXI
            // 
            this.radioVXI.AutoSize = true;
            this.radioVXI.Location = new System.Drawing.Point(14, 20);
            this.radioVXI.Name = "radioVXI";
            this.radioVXI.Size = new System.Drawing.Size(42, 17);
            this.radioVXI.TabIndex = 2;
            this.radioVXI.Text = "VXI";
            this.radioVXI.UseVisualStyleBackColor = true;
            this.radioVXI.CheckedChanged += new System.EventHandler(this.OnRadioButton_CheckedChanged);
            // 
            // textVxiLink
            // 
            this.textVxiLink.Location = new System.Drawing.Point(253, 25);
            this.textVxiLink.Name = "textVxiLink";
            this.textVxiLink.Size = new System.Drawing.Size(48, 20);
            this.textVxiLink.TabIndex = 30;
            // 
            // lblDevice3
            // 
            this.lblDevice3.AutoSize = true;
            this.lblDevice3.Location = new System.Drawing.Point(253, 9);
            this.lblDevice3.Name = "lblDevice3";
            this.lblDevice3.Size = new System.Drawing.Size(79, 13);
            this.lblDevice3.TabIndex = 15;
            this.lblDevice3.Text = "Label Device 3";
            // 
            // lblDevice2
            // 
            this.lblDevice2.AutoSize = true;
            this.lblDevice2.Location = new System.Drawing.Point(140, 9);
            this.lblDevice2.Name = "lblDevice2";
            this.lblDevice2.Size = new System.Drawing.Size(76, 13);
            this.lblDevice2.TabIndex = 16;
            this.lblDevice2.Text = "Label Device2";
            // 
            // comboBaud
            // 
            this.comboBaud.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBaud.FormattingEnabled = true;
            this.comboBaud.Items.AddRange(new object[] {
            "9600",
            "14400",
            "19200",
            "28800",
            "38400",
            "57600",
            "115200"});
            this.comboBaud.Location = new System.Drawing.Point(141, 25);
            this.comboBaud.Name = "comboBaud";
            this.comboBaud.Size = new System.Drawing.Size(75, 21);
            this.comboBaud.TabIndex = 101;
            // 
            // groupDcCapture
            // 
            this.groupDcCapture.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.groupDcCapture.Controls.Add(this.comboTotalTime);
            this.groupDcCapture.Controls.Add(this.label2);
            this.groupDcCapture.Controls.Add(this.btnDcCapture);
            this.groupDcCapture.Controls.Add(this.comboInterval);
            this.groupDcCapture.Controls.Add(this.label3);
            this.groupDcCapture.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.groupDcCapture.Location = new System.Drawing.Point(13, 71);
            this.groupDcCapture.Name = "groupDcCapture";
            this.groupDcCapture.Size = new System.Drawing.Size(540, 55);
            this.groupDcCapture.TabIndex = 102;
            this.groupDcCapture.TabStop = false;
            this.groupDcCapture.Text = "  DC Capture  ";
            // 
            // comboTotalTime
            // 
            this.comboTotalTime.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTotalTime.FormattingEnabled = true;
            this.comboTotalTime.Items.AddRange(new object[] {
            "10 minutes",
            "30 minutes",
            "1 hour",
            "2 hours",
            "6 hours",
            "12 hours",
            "1 day",
            "2 days",
            "5 days",
            "10 days"});
            this.comboTotalTime.Location = new System.Drawing.Point(265, 22);
            this.comboTotalTime.Name = "comboTotalTime";
            this.comboTotalTime.Size = new System.Drawing.Size(85, 21);
            this.comboTotalTime.TabIndex = 29;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(201, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 28;
            this.label2.Text = "Total Time:";
            // 
            // btnDcCapture
            // 
            this.btnDcCapture.ForeColor = System.Drawing.Color.Black;
            this.btnDcCapture.Location = new System.Drawing.Point(443, 20);
            this.btnDcCapture.Name = "btnDcCapture";
            this.btnDcCapture.Size = new System.Drawing.Size(84, 23);
            this.btnDcCapture.TabIndex = 26;
            this.btnDcCapture.Text = "DC Capture";
            this.btnDcCapture.UseVisualStyleBackColor = true;
            this.btnDcCapture.Click += new System.EventHandler(this.btnDcCapture_Click);
            // 
            // comboInterval
            // 
            this.comboInterval.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboInterval.FormattingEnabled = true;
            this.comboInterval.Items.AddRange(new object[] {
            "500 ms",
            "1 second",
            "2 seconds",
            "5 seconds",
            "10 seconds",
            "30 seconds"});
            this.comboInterval.Location = new System.Drawing.Point(102, 22);
            this.comboInterval.Name = "comboInterval";
            this.comboInterval.Size = new System.Drawing.Size(85, 21);
            this.comboInterval.TabIndex = 27;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(13, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(85, 13);
            this.label3.TabIndex = 26;
            this.label3.Text = "Capture Interval:";
            // 
            // FormTransfer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(566, 283);
            this.Controls.Add(this.groupDcCapture);
            this.Controls.Add(this.comboBaud);
            this.Controls.Add(this.lblDevice2);
            this.Controls.Add(this.comboRS232);
            this.Controls.Add(this.radioRS232);
            this.Controls.Add(this.lblDevice3);
            this.Controls.Add(this.textVxiLink);
            this.Controls.Add(this.radioVXI);
            this.Controls.Add(this.radioTCP);
            this.Controls.Add(this.radioUSB);
            this.Controls.Add(this.linkHelp);
            this.Controls.Add(this.groupCommand);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.btnSearch);
            this.Controls.Add(this.btnInstallDriver);
            this.Controls.Add(this.lblDevice1);
            this.Controls.Add(this.comboDevices);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormTransfer";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Capture / Transfer";
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.groupCommand.ResumeLayout(false);
            this.groupCommand.PerformLayout();
            this.groupDcCapture.ResumeLayout(false);
            this.groupDcCapture.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboDevices;
        private System.Windows.Forms.Label lblDevice1;
        private System.Windows.Forms.Button btnInstallDriver;
        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.GroupBox groupCommand;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox textCommand;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textResponse;
        private System.Windows.Forms.LinkLabel linkHelp;
        private System.Windows.Forms.RadioButton radioUSB;
        private System.Windows.Forms.RadioButton radioTCP;
        private System.Windows.Forms.RadioButton radioVXI;
        private System.Windows.Forms.TextBox textVxiLink;
        private System.Windows.Forms.Label lblDevice3;
        private System.Windows.Forms.RadioButton radioRS232;
        private System.Windows.Forms.ComboBox comboRS232;
        private System.Windows.Forms.Label lblDevice2;
        private System.Windows.Forms.ComboBox comboBaud;
        private System.Windows.Forms.GroupBox groupDcCapture;
        private System.Windows.Forms.ComboBox comboTotalTime;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnDcCapture;
        private System.Windows.Forms.ComboBox comboInterval;
        private System.Windows.Forms.Label label3;

    }
}