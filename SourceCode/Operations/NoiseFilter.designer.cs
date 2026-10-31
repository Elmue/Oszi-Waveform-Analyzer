namespace Operations
{
    partial class NoiseFilter
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NoiseFilter));
            this.label1 = new System.Windows.Forms.Label();
            this.btnApply = new System.Windows.Forms.Button();
            this.trackSuppr = new System.Windows.Forms.TrackBar();
            this.lblSuppr = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.trackCycles = new System.Windows.Forms.TrackBar();
            this.lblCycles = new System.Windows.Forms.Label();
            this.lblChannel = new System.Windows.Forms.Label();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.lblApply = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.trackSuppr)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackCycles)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(33, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(0, 13);
            this.label1.TabIndex = 0;
            // 
            // btnApply
            // 
            this.btnApply.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnApply.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnApply.ForeColor = System.Drawing.Color.Black;
            this.btnApply.Location = new System.Drawing.Point(228, 161);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(85, 23);
            this.btnApply.TabIndex = 5;
            this.btnApply.Text = "Apply Filter";
            this.btnApply.UseVisualStyleBackColor = true;
            // 
            // trackSuppr
            // 
            this.trackSuppr.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.trackSuppr.Location = new System.Drawing.Point(1, 46);
            this.trackSuppr.Maximum = 950;
            this.trackSuppr.Name = "trackSuppr";
            this.trackSuppr.Size = new System.Drawing.Size(437, 45);
            this.trackSuppr.TabIndex = 6;
            this.trackSuppr.TickFrequency = 100;
            this.trackSuppr.Scroll += new System.EventHandler(this.trackBar_Scroll);
            // 
            // lblSuppr
            // 
            this.lblSuppr.AutoSize = true;
            this.lblSuppr.ForeColor = System.Drawing.Color.White;
            this.lblSuppr.Location = new System.Drawing.Point(8, 27);
            this.lblSuppr.Name = "lblSuppr";
            this.lblSuppr.Size = new System.Drawing.Size(68, 13);
            this.lblSuppr.TabIndex = 7;
            this.lblSuppr.Text = "Suppression:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.ForeColor = System.Drawing.Color.Black;
            this.btnCancel.Location = new System.Drawing.Point(129, 161);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(85, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // trackCycles
            // 
            this.trackCycles.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.trackCycles.Location = new System.Drawing.Point(1, 102);
            this.trackCycles.Maximum = 15;
            this.trackCycles.Minimum = 1;
            this.trackCycles.Name = "trackCycles";
            this.trackCycles.Size = new System.Drawing.Size(437, 45);
            this.trackCycles.TabIndex = 9;
            this.trackCycles.Value = 1;
            this.trackCycles.Scroll += new System.EventHandler(this.trackBar_Scroll);
            // 
            // lblCycles
            // 
            this.lblCycles.AutoSize = true;
            this.lblCycles.ForeColor = System.Drawing.Color.White;
            this.lblCycles.Location = new System.Drawing.Point(8, 83);
            this.lblCycles.Name = "lblCycles";
            this.lblCycles.Size = new System.Drawing.Size(41, 13);
            this.lblCycles.TabIndex = 10;
            this.lblCycles.Text = "Cycles:";
            // 
            // lblChannel
            // 
            this.lblChannel.AutoSize = true;
            this.lblChannel.ForeColor = System.Drawing.Color.White;
            this.lblChannel.Location = new System.Drawing.Point(8, 9);
            this.lblChannel.Name = "lblChannel";
            this.lblChannel.Size = new System.Drawing.Size(80, 13);
            this.lblChannel.TabIndex = 11;
            this.lblChannel.Text = "Channel: Name";
            // 
            // linkHelp
            // 
            this.linkHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(323, 165);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 116;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // lblApply
            // 
            this.lblApply.AutoSize = true;
            this.lblApply.ForeColor = System.Drawing.Color.White;
            this.lblApply.Location = new System.Drawing.Point(7, 141);
            this.lblApply.Name = "lblApply";
            this.lblApply.Size = new System.Drawing.Size(48, 13);
            this.lblApply.TabIndex = 117;
            this.lblApply.Text = "Apply to:";
            // 
            // NoiseFilter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(441, 195);
            this.Controls.Add(this.lblApply);
            this.Controls.Add(this.linkHelp);
            this.Controls.Add(this.lblChannel);
            this.Controls.Add(this.lblCycles);
            this.Controls.Add(this.trackCycles);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblSuppr);
            this.Controls.Add(this.trackSuppr);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.label1);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NoiseFilter";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Noise Suppression";
            ((System.ComponentModel.ISupportInitialize)(this.trackSuppr)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackCycles)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.TrackBar trackSuppr;
        private System.Windows.Forms.Label lblSuppr;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TrackBar trackCycles;
        private System.Windows.Forms.Label lblCycles;
        private System.Windows.Forms.Label lblChannel;
        private System.Windows.Forms.LinkLabel linkHelp;
        private System.Windows.Forms.Label lblApply;
    }
}