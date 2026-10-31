namespace OsziWaveformAnalyzer
{
    partial class CtrlXY
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CtrlXY));
            this.textGlowCount = new System.Windows.Forms.TextBox();
            this.lblSmplCount = new System.Windows.Forms.Label();
            this.btnSet = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.lblPlayStep = new System.Windows.Forms.Label();
            this.textPlayStep = new System.Windows.Forms.TextBox();
            this.groupBox = new System.Windows.Forms.GroupBox();
            this.btnJump = new System.Windows.Forms.Button();
            this.btnRepeat = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.groupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // textGlowCount
            // 
            this.textGlowCount.Location = new System.Drawing.Point(84, 16);
            this.textGlowCount.Name = "textGlowCount";
            this.textGlowCount.Size = new System.Drawing.Size(49, 20);
            this.textGlowCount.TabIndex = 1;
            this.textGlowCount.Text = "100";
            this.textGlowCount.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnTextBox_KeyDown);
            // 
            // lblSmplCount
            // 
            this.lblSmplCount.ForeColor = System.Drawing.Color.White;
            this.lblSmplCount.Location = new System.Drawing.Point(9, 19);
            this.lblSmplCount.Name = "lblSmplCount";
            this.lblSmplCount.Size = new System.Drawing.Size(71, 13);
            this.lblSmplCount.TabIndex = 2;
            this.lblSmplCount.Text = "Glow Count:";
            this.lblSmplCount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnSet
            // 
            this.btnSet.ForeColor = System.Drawing.Color.Black;
            this.btnSet.Location = new System.Drawing.Point(140, 31);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(54, 23);
            this.btnSet.TabIndex = 4;
            this.btnSet.Text = "Set";
            this.btnSet.UseVisualStyleBackColor = true;
            this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
            // 
            // btnPlay
            // 
            this.btnPlay.ForeColor = System.Drawing.Color.Black;
            this.btnPlay.Image = ((System.Drawing.Image)(resources.GetObject("btnPlay.Image")));
            this.btnPlay.Location = new System.Drawing.Point(37, 69);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(24, 24);
            this.btnPlay.TabIndex = 5;
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // btnStop
            // 
            this.btnStop.ForeColor = System.Drawing.Color.Black;
            this.btnStop.Image = ((System.Drawing.Image)(resources.GetObject("btnStop.Image")));
            this.btnStop.Location = new System.Drawing.Point(63, 69);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(24, 24);
            this.btnStop.TabIndex = 8;
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // lblPlayStep
            // 
            this.lblPlayStep.ForeColor = System.Drawing.Color.White;
            this.lblPlayStep.Location = new System.Drawing.Point(9, 46);
            this.lblPlayStep.Name = "lblPlayStep";
            this.lblPlayStep.Size = new System.Drawing.Size(71, 13);
            this.lblPlayStep.TabIndex = 9;
            this.lblPlayStep.Text = "Play Step:";
            this.lblPlayStep.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textPlayStep
            // 
            this.textPlayStep.Location = new System.Drawing.Point(84, 43);
            this.textPlayStep.Name = "textPlayStep";
            this.textPlayStep.Size = new System.Drawing.Size(48, 20);
            this.textPlayStep.TabIndex = 10;
            this.textPlayStep.Text = "1";
            this.textPlayStep.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnTextBox_KeyDown);
            // 
            // groupBox
            // 
            this.groupBox.Controls.Add(this.linkHelp);
            this.groupBox.Controls.Add(this.btnJump);
            this.groupBox.Controls.Add(this.btnRepeat);
            this.groupBox.Controls.Add(this.btnReset);
            this.groupBox.Controls.Add(this.lblSmplCount);
            this.groupBox.Controls.Add(this.textPlayStep);
            this.groupBox.Controls.Add(this.textGlowCount);
            this.groupBox.Controls.Add(this.lblPlayStep);
            this.groupBox.Controls.Add(this.btnSet);
            this.groupBox.Controls.Add(this.btnStop);
            this.groupBox.Controls.Add(this.btnPlay);
            this.groupBox.Location = new System.Drawing.Point(0, 0);
            this.groupBox.Name = "groupBox";
            this.groupBox.Size = new System.Drawing.Size(207, 102);
            this.groupBox.TabIndex = 11;
            this.groupBox.TabStop = false;
            // 
            // btnJump
            // 
            this.btnJump.ForeColor = System.Drawing.Color.Black;
            this.btnJump.Location = new System.Drawing.Point(140, 69);
            this.btnJump.Name = "btnJump";
            this.btnJump.Size = new System.Drawing.Size(54, 23);
            this.btnJump.TabIndex = 13;
            this.btnJump.Text = "Jump";
            this.btnJump.UseVisualStyleBackColor = true;
            this.btnJump.Click += new System.EventHandler(this.btnJump_Click);
            // 
            // btnRepeat
            // 
            this.btnRepeat.ForeColor = System.Drawing.Color.Black;
            this.btnRepeat.Image = ((System.Drawing.Image)(resources.GetObject("btnRepeat.Image")));
            this.btnRepeat.Location = new System.Drawing.Point(109, 69);
            this.btnRepeat.Name = "btnRepeat";
            this.btnRepeat.Size = new System.Drawing.Size(24, 24);
            this.btnRepeat.TabIndex = 12;
            this.btnRepeat.UseVisualStyleBackColor = true;
            this.btnRepeat.Click += new System.EventHandler(this.btnRepeat_Click);
            // 
            // btnReset
            // 
            this.btnReset.ForeColor = System.Drawing.Color.Black;
            this.btnReset.Image = ((System.Drawing.Image)(resources.GetObject("btnReset.Image")));
            this.btnReset.Location = new System.Drawing.Point(10, 69);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(24, 24);
            this.btnReset.TabIndex = 11;
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // linkHelp
            // 
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(139, 15);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 106;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // CtrlXY
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.Controls.Add(this.groupBox);
            this.Name = "CtrlXY";
            this.Size = new System.Drawing.Size(215, 110);
            this.groupBox.ResumeLayout(false);
            this.groupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox textGlowCount;
        private System.Windows.Forms.Label lblSmplCount;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Label lblPlayStep;
        private System.Windows.Forms.TextBox textPlayStep;
        private System.Windows.Forms.GroupBox groupBox;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnRepeat;
        private System.Windows.Forms.Button btnJump;
        private System.Windows.Forms.LinkLabel linkHelp;

    }
}
