namespace OsziWaveformAnalyzer
{
    partial class FormMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnSave = new System.Windows.Forms.Button();
            this.lblInfo = new System.Windows.Forms.Label();
            this.groupModel = new System.Windows.Forms.GroupBox();
            this.btnRecordAudio = new System.Windows.Forms.Button();
            this.btnFolder = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.comboOsziModel = new OsziWaveformAnalyzer.ComboBoxEx();
            this.label9 = new System.Windows.Forms.Label();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.comboInput = new OsziWaveformAnalyzer.ComboBoxEx();
            this.groupSave = new System.Windows.Forms.GroupBox();
            this.checkShrink8Bit = new System.Windows.Forms.CheckBox();
            this.linkSave = new System.Windows.Forms.LinkLabel();
            this.checkApplyFactor = new System.Windows.Forms.CheckBox();
            this.label6 = new System.Windows.Forms.Label();
            this.comboSaveAs = new OsziWaveformAnalyzer.ComboBoxEx();
            this.label5 = new System.Windows.Forms.Label();
            this.textFileName = new System.Windows.Forms.TextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabOszi = new System.Windows.Forms.TabPage();
            this.panelOszi = new OsziWaveformAnalyzer.PanelOszi();
            this.pictureOszi = new System.Windows.Forms.PictureBox();
            this.tabXY = new System.Windows.Forms.TabPage();
            this.scrollBarXY = new System.Windows.Forms.HScrollBar();
            this.panelXY = new OsziWaveformAnalyzer.PanelXY();
            this.tabFFT = new System.Windows.Forms.TabPage();
            this.panelFFT = new OsziWaveformAnalyzer.PanelFFT();
            this.tabDecoder = new System.Windows.Forms.TabPage();
            this.panelRtf = new OsziWaveformAnalyzer.PanelRtf();
            this.imagesToolbar = new System.Windows.Forms.ImageList(this.components);
            this.panelHint = new System.Windows.Forms.Panel();
            this.pictureLogo = new System.Windows.Forms.PictureBox();
            this.statusStrip.SuspendLayout();
            this.groupModel.SuspendLayout();
            this.groupSave.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabOszi.SuspendLayout();
            this.panelOszi.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureOszi)).BeginInit();
            this.tabXY.SuspendLayout();
            this.tabFFT.SuspendLayout();
            this.tabDecoder.SuspendLayout();
            this.panelHint.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // statusStrip
            // 
            this.statusStrip.BackColor = System.Drawing.SystemColors.Control;
            this.statusStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Visible;
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 494);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.ShowItemToolTips = true;
            this.statusStrip.Size = new System.Drawing.Size(895, 22);
            this.statusStrip.TabIndex = 4;
            this.statusStrip.Text = "statusStrip";
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = false;
            this.statusLabel.BackColor = System.Drawing.SystemColors.Control;
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.statusLabel.ForeColor = System.Drawing.Color.Black;
            this.statusLabel.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(400, 17);
            this.statusLabel.Text = "Status";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.ForeColor = System.Drawing.Color.Black;
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.Location = new System.Drawing.Point(117, 25);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(24, 24);
            this.btnSave.TabIndex = 3;
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // lblInfo
            // 
            this.lblInfo.AutoEllipsis = true;
            this.lblInfo.ForeColor = System.Drawing.Color.Cyan;
            this.lblInfo.Location = new System.Drawing.Point(0, 5);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(427, 13);
            this.lblInfo.TabIndex = 100;
            this.lblInfo.Text = "Total Samples:  0";
            // 
            // groupModel
            // 
            this.groupModel.Controls.Add(this.btnRecordAudio);
            this.groupModel.Controls.Add(this.btnFolder);
            this.groupModel.Controls.Add(this.btnCapture);
            this.groupModel.Controls.Add(this.comboOsziModel);
            this.groupModel.Controls.Add(this.label9);
            this.groupModel.Controls.Add(this.btnRefresh);
            this.groupModel.Controls.Add(this.label1);
            this.groupModel.Controls.Add(this.comboInput);
            this.groupModel.Location = new System.Drawing.Point(116, 2);
            this.groupModel.Name = "groupModel";
            this.groupModel.Size = new System.Drawing.Size(357, 102);
            this.groupModel.TabIndex = 1;
            this.groupModel.TabStop = false;
            this.toolTip.SetToolTip(this.groupModel, "Capture (Transfer Oscilloscope Waveform)");
            // 
            // btnRecordAudio
            // 
            this.btnRecordAudio.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRecordAudio.ForeColor = System.Drawing.Color.Black;
            this.btnRecordAudio.Image = ((System.Drawing.Image)(resources.GetObject("btnRecordAudio.Image")));
            this.btnRecordAudio.Location = new System.Drawing.Point(327, 25);
            this.btnRecordAudio.Name = "btnRecordAudio";
            this.btnRecordAudio.Size = new System.Drawing.Size(24, 24);
            this.btnRecordAudio.TabIndex = 13;
            this.toolTip.SetToolTip(this.btnRecordAudio, "Capture (Record signal from soundcard)");
            this.btnRecordAudio.UseVisualStyleBackColor = true;
            this.btnRecordAudio.Click += new System.EventHandler(this.btnRecordAudio_Click);
            // 
            // btnFolder
            // 
            this.btnFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFolder.ForeColor = System.Drawing.Color.Black;
            this.btnFolder.Image = ((System.Drawing.Image)(resources.GetObject("btnFolder.Image")));
            this.btnFolder.Location = new System.Drawing.Point(327, 65);
            this.btnFolder.Name = "btnFolder";
            this.btnFolder.Size = new System.Drawing.Size(24, 24);
            this.btnFolder.TabIndex = 12;
            this.btnFolder.UseVisualStyleBackColor = true;
            this.btnFolder.Click += new System.EventHandler(this.btnFolder_Click);
            // 
            // btnCapture
            // 
            this.btnCapture.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCapture.ForeColor = System.Drawing.Color.Black;
            this.btnCapture.Image = ((System.Drawing.Image)(resources.GetObject("btnCapture.Image")));
            this.btnCapture.Location = new System.Drawing.Point(299, 25);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(24, 24);
            this.btnCapture.TabIndex = 2;
            this.toolTip.SetToolTip(this.btnCapture, "Capture (Transfer waveform from oscilloscope to computer)");
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // comboOsziModel
            // 
            this.comboOsziModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboOsziModel.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboOsziModel.DropDownHeight = 500;
            this.comboOsziModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboOsziModel.FormattingEnabled = true;
            this.comboOsziModel.IntegralHeight = false;
            this.comboOsziModel.Location = new System.Drawing.Point(8, 27);
            this.comboOsziModel.MaxDropDownItems = 30;
            this.comboOsziModel.Name = "comboOsziModel";
            this.comboOsziModel.Size = new System.Drawing.Size(285, 21);
            this.comboOsziModel.Sorted = true;
            this.comboOsziModel.TabIndex = 1;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(8, 12);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(62, 13);
            this.label9.TabIndex = 6;
            this.label9.Text = "Oszi Model:";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.ForeColor = System.Drawing.Color.Black;
            this.btnRefresh.Image = ((System.Drawing.Image)(resources.GetObject("btnRefresh.Image")));
            this.btnRefresh.Location = new System.Drawing.Point(299, 65);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(24, 24);
            this.btnRefresh.TabIndex = 11;
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 52);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Input File:";
            // 
            // comboInput
            // 
            this.comboInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboInput.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboInput.DropDownHeight = 500;
            this.comboInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboInput.FormattingEnabled = true;
            this.comboInput.IntegralHeight = false;
            this.comboInput.Location = new System.Drawing.Point(8, 67);
            this.comboInput.MaxDropDownItems = 30;
            this.comboInput.Name = "comboInput";
            this.comboInput.Size = new System.Drawing.Size(285, 21);
            this.comboInput.Sorted = true;
            this.comboInput.TabIndex = 10;
            this.comboInput.SelectedIndexChanged += new System.EventHandler(this.comboInput_SelectedIndexChanged);
            // 
            // groupSave
            // 
            this.groupSave.Controls.Add(this.checkShrink8Bit);
            this.groupSave.Controls.Add(this.linkSave);
            this.groupSave.Controls.Add(this.checkApplyFactor);
            this.groupSave.Controls.Add(this.label6);
            this.groupSave.Controls.Add(this.comboSaveAs);
            this.groupSave.Controls.Add(this.label5);
            this.groupSave.Controls.Add(this.textFileName);
            this.groupSave.Controls.Add(this.btnSave);
            this.groupSave.Location = new System.Drawing.Point(479, 2);
            this.groupSave.Name = "groupSave";
            this.groupSave.Size = new System.Drawing.Size(246, 102);
            this.groupSave.TabIndex = 4;
            this.groupSave.TabStop = false;
            // 
            // checkShrink8Bit
            // 
            this.checkShrink8Bit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkShrink8Bit.AutoSize = true;
            this.checkShrink8Bit.Location = new System.Drawing.Point(147, 32);
            this.checkShrink8Bit.Name = "checkShrink8Bit";
            this.checkShrink8Bit.Size = new System.Drawing.Size(91, 17);
            this.checkShrink8Bit.TabIndex = 103;
            this.checkShrink8Bit.Text = "Shrink to 8 bit";
            this.toolTip.SetToolTip(this.checkShrink8Bit, "ATTENTION:\nThis checkbox reduces the vertical resolution of a 16 bit capture to 8" +
                    " bit.\nThis results in a smaller file size but means also a loss of signal qualit" +
                    "y.");
            this.checkShrink8Bit.UseVisualStyleBackColor = true;
            // 
            // linkSave
            // 
            this.linkSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.linkSave.AutoSize = true;
            this.linkSave.LinkColor = System.Drawing.Color.Lime;
            this.linkSave.Location = new System.Drawing.Point(181, 50);
            this.linkSave.Name = "linkSave";
            this.linkSave.Size = new System.Drawing.Size(59, 13);
            this.linkSave.TabIndex = 102;
            this.linkSave.TabStop = true;
            this.linkSave.Text = "Show Help";
            this.toolTip.SetToolTip(this.linkSave, "Show help about the Save otions and the Factor");
            this.linkSave.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkSave_LinkClicked);
            // 
            // checkApplyFactor
            // 
            this.checkApplyFactor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkApplyFactor.AutoSize = true;
            this.checkApplyFactor.Location = new System.Drawing.Point(147, 16);
            this.checkApplyFactor.Name = "checkApplyFactor";
            this.checkApplyFactor.Size = new System.Drawing.Size(85, 17);
            this.checkApplyFactor.TabIndex = 34;
            this.checkApplyFactor.Text = "Apply Factor";
            this.toolTip.SetToolTip(this.checkApplyFactor, resources.GetString("checkApplyFactor.ToolTip"));
            this.checkApplyFactor.UseVisualStyleBackColor = true;
            this.checkApplyFactor.CheckedChanged += new System.EventHandler(this.checkSaveFactor_CheckedChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 12);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(49, 13);
            this.label6.TabIndex = 33;
            this.label6.Text = "Save as:";
            // 
            // comboSaveAs
            // 
            this.comboSaveAs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.comboSaveAs.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
            this.comboSaveAs.DropDownHeight = 500;
            this.comboSaveAs.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSaveAs.DropDownWidth = 150;
            this.comboSaveAs.FormattingEnabled = true;
            this.comboSaveAs.IntegralHeight = false;
            this.comboSaveAs.Location = new System.Drawing.Point(8, 27);
            this.comboSaveAs.MaxDropDownItems = 30;
            this.comboSaveAs.Name = "comboSaveAs";
            this.comboSaveAs.Size = new System.Drawing.Size(103, 21);
            this.comboSaveAs.TabIndex = 1;
            this.comboSaveAs.SelectedIndexChanged += new System.EventHandler(this.comboSaveAs_SelectedIndexChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(7, 52);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(52, 13);
            this.label5.TabIndex = 32;
            this.label5.Text = "Filename:";
            // 
            // textFileName
            // 
            this.textFileName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.textFileName.Location = new System.Drawing.Point(8, 68);
            this.textFileName.Name = "textFileName";
            this.textFileName.Size = new System.Drawing.Size(229, 20);
            this.textFileName.TabIndex = 2;
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 32000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 100;
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabOszi);
            this.tabControl.Controls.Add(this.tabXY);
            this.tabControl.Controls.Add(this.tabFFT);
            this.tabControl.Controls.Add(this.tabDecoder);
            this.tabControl.ImageList = this.imagesToolbar;
            this.tabControl.ItemSize = new System.Drawing.Size(40, 19);
            this.tabControl.Location = new System.Drawing.Point(8, 110);
            this.tabControl.Margin = new System.Windows.Forms.Padding(0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(878, 380);
            this.tabControl.TabIndex = 100;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.OnTabControl_SelectedIndexChanged);
            // 
            // tabOszi
            // 
            this.tabOszi.BackColor = System.Drawing.Color.Black;
            this.tabOszi.Controls.Add(this.panelOszi);
            this.tabOszi.ImageIndex = 0;
            this.tabOszi.Location = new System.Drawing.Point(4, 23);
            this.tabOszi.Name = "tabOszi";
            this.tabOszi.Size = new System.Drawing.Size(870, 353);
            this.tabOszi.TabIndex = 0;
            this.tabOszi.Text = "Oszi";
            // 
            // panelOszi
            // 
            this.panelOszi.AutoScroll = true;
            this.panelOszi.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelOszi.BackColor = System.Drawing.Color.Transparent;
            this.panelOszi.Controls.Add(this.pictureOszi);
            this.panelOszi.DispSteps = 0;
            this.panelOszi.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelOszi.Location = new System.Drawing.Point(0, 0);
            this.panelOszi.Name = "panelOszi";
            this.panelOszi.SeparateChannels = false;
            this.panelOszi.Size = new System.Drawing.Size(870, 353);
            this.panelOszi.TabIndex = 0;
            this.panelOszi.TabStop = true;
            this.panelOszi.Zoom = 0;
            // 
            // pictureOszi
            // 
            this.pictureOszi.BackColor = System.Drawing.Color.Silver;
            this.pictureOszi.Image = ((System.Drawing.Image)(resources.GetObject("pictureOszi.Image")));
            this.pictureOszi.Location = new System.Drawing.Point(170, 43);
            this.pictureOszi.Name = "pictureOszi";
            this.pictureOszi.Size = new System.Drawing.Size(500, 256);
            this.pictureOszi.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureOszi.TabIndex = 0;
            this.pictureOszi.TabStop = false;
            // 
            // tabXY
            // 
            this.tabXY.BackColor = System.Drawing.Color.Black;
            this.tabXY.Controls.Add(this.scrollBarXY);
            this.tabXY.Controls.Add(this.panelXY);
            this.tabXY.ForeColor = System.Drawing.Color.White;
            this.tabXY.ImageIndex = 1;
            this.tabXY.Location = new System.Drawing.Point(4, 23);
            this.tabXY.Name = "tabXY";
            this.tabXY.Size = new System.Drawing.Size(870, 353);
            this.tabXY.TabIndex = 2;
            this.tabXY.Text = "X/Y";
            // 
            // scrollBarXY
            // 
            this.scrollBarXY.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.scrollBarXY.Location = new System.Drawing.Point(0, 336);
            this.scrollBarXY.Name = "scrollBarXY";
            this.scrollBarXY.Size = new System.Drawing.Size(870, 17);
            this.scrollBarXY.TabIndex = 1;
            // 
            // panelXY
            // 
            this.panelXY.BackColor = System.Drawing.Color.Gray;
            this.panelXY.Location = new System.Drawing.Point(236, 25);
            this.panelXY.Name = "panelXY";
            this.panelXY.Size = new System.Drawing.Size(388, 285);
            this.panelXY.TabIndex = 0;
            // 
            // tabFFT
            // 
            this.tabFFT.BackColor = System.Drawing.Color.Black;
            this.tabFFT.Controls.Add(this.panelFFT);
            this.tabFFT.ImageIndex = 2;
            this.tabFFT.Location = new System.Drawing.Point(4, 23);
            this.tabFFT.Name = "tabFFT";
            this.tabFFT.Size = new System.Drawing.Size(870, 353);
            this.tabFFT.TabIndex = 3;
            this.tabFFT.Text = "FFT";
            // 
            // panelFFT
            // 
            this.panelFFT.BackColor = System.Drawing.Color.Gray;
            this.panelFFT.Location = new System.Drawing.Point(236, 52);
            this.panelFFT.Name = "panelFFT";
            this.panelFFT.Size = new System.Drawing.Size(379, 259);
            this.panelFFT.TabIndex = 0;
            // 
            // tabDecoder
            // 
            this.tabDecoder.BackColor = System.Drawing.Color.Black;
            this.tabDecoder.Controls.Add(this.panelRtf);
            this.tabDecoder.ImageIndex = 3;
            this.tabDecoder.Location = new System.Drawing.Point(4, 23);
            this.tabDecoder.Name = "tabDecoder";
            this.tabDecoder.Size = new System.Drawing.Size(870, 353);
            this.tabDecoder.TabIndex = 1;
            this.tabDecoder.Text = "Decoder";
            // 
            // panelRtf
            // 
            this.panelRtf.BackColor = System.Drawing.Color.Black;
            this.panelRtf.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRtf.ForeColor = System.Drawing.Color.White;
            this.panelRtf.Location = new System.Drawing.Point(0, 0);
            this.panelRtf.Name = "panelRtf";
            this.panelRtf.Size = new System.Drawing.Size(870, 353);
            this.panelRtf.TabIndex = 1;
            this.panelRtf.Text = "";
            // 
            // imagesToolbar
            // 
            this.imagesToolbar.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imagesToolbar.ImageStream")));
            this.imagesToolbar.TransparentColor = System.Drawing.Color.Transparent;
            this.imagesToolbar.Images.SetKeyName(0, "Oszi.ico");
            this.imagesToolbar.Images.SetKeyName(1, "XYPlot.ico");
            this.imagesToolbar.Images.SetKeyName(2, "SpectrumFFT.ico");
            this.imagesToolbar.Images.SetKeyName(3, "ComboDecoder.png");
            // 
            // panelHint
            // 
            this.panelHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelHint.Controls.Add(this.lblInfo);
            this.panelHint.Location = new System.Drawing.Point(253, 108);
            this.panelHint.Name = "panelHint";
            this.panelHint.Size = new System.Drawing.Size(623, 22);
            this.panelHint.TabIndex = 0;
            // 
            // pictureLogo
            // 
            this.pictureLogo.Location = new System.Drawing.Point(10, 8);
            this.pictureLogo.Name = "pictureLogo";
            this.pictureLogo.Size = new System.Drawing.Size(96, 96);
            this.pictureLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureLogo.TabIndex = 101;
            this.pictureLogo.TabStop = false;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(895, 516);
            this.Controls.Add(this.pictureLogo);
            this.Controls.Add(this.panelHint);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.groupModel);
            this.Controls.Add(this.groupSave);
            this.Controls.Add(this.statusStrip);
            this.ForeColor = System.Drawing.Color.White;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(700, 474);
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Oszi";
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.groupModel.ResumeLayout(false);
            this.groupModel.PerformLayout();
            this.groupSave.ResumeLayout(false);
            this.groupSave.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabOszi.ResumeLayout(false);
            this.panelOszi.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureOszi)).EndInit();
            this.tabXY.ResumeLayout(false);
            this.tabFFT.ResumeLayout(false);
            this.tabDecoder.ResumeLayout(false);
            this.panelHint.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private PanelOszi panelOszi;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.GroupBox groupModel;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Label label1;
        private OsziWaveformAnalyzer.ComboBoxEx comboInput;
        private System.Windows.Forms.GroupBox groupSave;
        private System.Windows.Forms.Label label6;
        private OsziWaveformAnalyzer.ComboBoxEx comboSaveAs;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textFileName;
        private System.Windows.Forms.Button btnCapture;
        private OsziWaveformAnalyzer.ComboBoxEx comboOsziModel;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.CheckBox checkApplyFactor;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabOszi;
        private System.Windows.Forms.TabPage tabDecoder;
        private System.Windows.Forms.Panel panelHint;
        private System.Windows.Forms.PictureBox pictureLogo;
        private PanelRtf panelRtf;
        private System.Windows.Forms.LinkLabel linkSave;
        private System.Windows.Forms.Button btnFolder;
        private System.Windows.Forms.TabPage tabXY;
        private System.Windows.Forms.TabPage tabFFT;
        private OsziWaveformAnalyzer.PanelXY panelXY;
        private System.Windows.Forms.HScrollBar scrollBarXY;
        private System.Windows.Forms.CheckBox checkShrink8Bit;
        private OsziWaveformAnalyzer.PanelFFT panelFFT;
        private System.Windows.Forms.ImageList imagesToolbar;
        private System.Windows.Forms.PictureBox pictureOszi;
        private System.Windows.Forms.Button btnRecordAudio;
    }
}

