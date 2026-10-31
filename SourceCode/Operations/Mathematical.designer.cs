namespace Operations
{
    partial class Mathematical
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Mathematical));
            this.btnExecute = new System.Windows.Forms.Button();
            this.comboMath = new System.Windows.Forms.ComboBox();
            this.lblChannelA = new System.Windows.Forms.Label();
            this.lblChannelB = new System.Windows.Forms.Label();
            this.comboChannelB = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.lblResult = new System.Windows.Forms.Label();
            this.textResult = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.lblMathMode = new System.Windows.Forms.Label();
            this.btnExecuteClose = new System.Windows.Forms.Button();
            this.linkHelp = new System.Windows.Forms.LinkLabel();
            this.lblParam = new System.Windows.Forms.Label();
            this.textParameter = new System.Windows.Forms.TextBox();
            this.lblUnit = new System.Windows.Forms.Label();
            this.comboChannelA = new System.Windows.Forms.ComboBox();
            this.lblApplyTo = new System.Windows.Forms.Label();
            this.panelParam = new System.Windows.Forms.Panel();
            this.panelApply = new System.Windows.Forms.Panel();
            this.radioApplyAll = new System.Windows.Forms.RadioButton();
            this.radioApplyB = new System.Windows.Forms.RadioButton();
            this.radioApplyA = new System.Windows.Forms.RadioButton();
            this.panelResult = new System.Windows.Forms.Panel();
            this.panelChannelB = new System.Windows.Forms.Panel();
            this.lblApply = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.panelParam.SuspendLayout();
            this.panelApply.SuspendLayout();
            this.panelResult.SuspendLayout();
            this.panelChannelB.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnExecute
            // 
            this.btnExecute.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExecute.ForeColor = System.Drawing.Color.Black;
            this.btnExecute.Location = new System.Drawing.Point(137, 224);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(66, 23);
            this.btnExecute.TabIndex = 51;
            this.btnExecute.Text = "Execute";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // comboMath
            // 
            this.comboMath.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMath.FormattingEnabled = true;
            this.comboMath.Location = new System.Drawing.Point(103, 11);
            this.comboMath.Name = "comboMath";
            this.comboMath.Size = new System.Drawing.Size(163, 21);
            this.comboMath.TabIndex = 20;
            this.comboMath.SelectedIndexChanged += new System.EventHandler(this.comboMath_SelectedIndexChanged);
            // 
            // lblChannelA
            // 
            this.lblChannelA.AutoSize = true;
            this.lblChannelA.ForeColor = System.Drawing.Color.White;
            this.lblChannelA.Location = new System.Drawing.Point(13, 87);
            this.lblChannelA.Name = "lblChannelA";
            this.lblChannelA.Size = new System.Drawing.Size(59, 13);
            this.lblChannelA.TabIndex = 7;
            this.lblChannelA.Text = "Channel A:";
            // 
            // lblChannelB
            // 
            this.lblChannelB.AutoSize = true;
            this.lblChannelB.ForeColor = System.Drawing.Color.White;
            this.lblChannelB.Location = new System.Drawing.Point(8, 7);
            this.lblChannelB.Name = "lblChannelB";
            this.lblChannelB.Size = new System.Drawing.Size(59, 13);
            this.lblChannelB.TabIndex = 8;
            this.lblChannelB.Text = "Channel B:";
            // 
            // comboChannelB
            // 
            this.comboChannelB.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboChannelB.FormattingEnabled = true;
            this.comboChannelB.Location = new System.Drawing.Point(98, 4);
            this.comboChannelB.Name = "comboChannelB";
            this.comboChannelB.Size = new System.Drawing.Size(163, 21);
            this.comboChannelB.TabIndex = 10;
            this.comboChannelB.SelectedIndexChanged += new System.EventHandler(this.comboChannelB_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(13, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 13);
            this.label3.TabIndex = 11;
            this.label3.Text = "Math Operation:";
            // 
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.ForeColor = System.Drawing.Color.White;
            this.lblResult.Location = new System.Drawing.Point(7, 9);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(84, 13);
            this.lblResult.TabIndex = 12;
            this.lblResult.Text = "Output Channel:";
            // 
            // textResult
            // 
            this.textResult.Location = new System.Drawing.Point(99, 6);
            this.textResult.Name = "textResult";
            this.textResult.Size = new System.Drawing.Size(162, 20);
            this.textResult.TabIndex = 40;
            this.textResult.Text = "Math Result";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(13, 40);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(64, 13);
            this.label5.TabIndex = 14;
            this.label5.Text = "Math Mode:";
            // 
            // lblMathMode
            // 
            this.lblMathMode.AutoSize = true;
            this.lblMathMode.Location = new System.Drawing.Point(101, 40);
            this.lblMathMode.Name = "lblMathMode";
            this.lblMathMode.Size = new System.Drawing.Size(40, 13);
            this.lblMathMode.TabIndex = 15;
            this.lblMathMode.Text = "Analog";
            // 
            // btnExecuteClose
            // 
            this.btnExecuteClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExecuteClose.ForeColor = System.Drawing.Color.Black;
            this.btnExecuteClose.Location = new System.Drawing.Point(12, 224);
            this.btnExecuteClose.Name = "btnExecuteClose";
            this.btnExecuteClose.Size = new System.Drawing.Size(116, 23);
            this.btnExecuteClose.TabIndex = 50;
            this.btnExecuteClose.Text = "Execute and Close";
            this.btnExecuteClose.UseVisualStyleBackColor = true;
            this.btnExecuteClose.Click += new System.EventHandler(this.btnExecuteClose_Click);
            // 
            // linkHelp
            // 
            this.linkHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.linkHelp.AutoSize = true;
            this.linkHelp.LinkColor = System.Drawing.Color.Lime;
            this.linkHelp.Location = new System.Drawing.Point(211, 227);
            this.linkHelp.Name = "linkHelp";
            this.linkHelp.Size = new System.Drawing.Size(59, 13);
            this.linkHelp.TabIndex = 52;
            this.linkHelp.TabStop = true;
            this.linkHelp.Text = "Show Help";
            this.linkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkHelp_LinkClicked);
            // 
            // lblParam
            // 
            this.lblParam.AutoSize = true;
            this.lblParam.ForeColor = System.Drawing.Color.White;
            this.lblParam.Location = new System.Drawing.Point(8, 7);
            this.lblParam.Name = "lblParam";
            this.lblParam.Size = new System.Drawing.Size(58, 13);
            this.lblParam.TabIndex = 117;
            this.lblParam.Text = "Parameter:";
            // 
            // textParameter
            // 
            this.textParameter.Location = new System.Drawing.Point(99, 4);
            this.textParameter.Name = "textParameter";
            this.textParameter.Size = new System.Drawing.Size(77, 20);
            this.textParameter.TabIndex = 30;
            // 
            // lblUnit
            // 
            this.lblUnit.AutoSize = true;
            this.lblUnit.ForeColor = System.Drawing.Color.White;
            this.lblUnit.Location = new System.Drawing.Point(184, 7);
            this.lblUnit.Name = "lblUnit";
            this.lblUnit.Size = new System.Drawing.Size(26, 13);
            this.lblUnit.TabIndex = 118;
            this.lblUnit.Text = "Unit";
            // 
            // comboChannelA
            // 
            this.comboChannelA.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboChannelA.FormattingEnabled = true;
            this.comboChannelA.Location = new System.Drawing.Point(103, 83);
            this.comboChannelA.Name = "comboChannelA";
            this.comboChannelA.Size = new System.Drawing.Size(163, 21);
            this.comboChannelA.TabIndex = 41;
            this.comboChannelA.SelectedIndexChanged += new System.EventHandler(this.comboChannelA_SelectedIndexChanged);
            // 
            // lblApplyTo
            // 
            this.lblApplyTo.AutoSize = true;
            this.lblApplyTo.ForeColor = System.Drawing.Color.White;
            this.lblApplyTo.Location = new System.Drawing.Point(9, 5);
            this.lblApplyTo.Name = "lblApplyTo";
            this.lblApplyTo.Size = new System.Drawing.Size(52, 13);
            this.lblApplyTo.TabIndex = 119;
            this.lblApplyTo.Text = "Apply To:";
            // 
            // panelParam
            // 
            this.panelParam.Controls.Add(this.lblParam);
            this.panelParam.Controls.Add(this.lblUnit);
            this.panelParam.Controls.Add(this.textParameter);
            this.panelParam.Location = new System.Drawing.Point(4, 188);
            this.panelParam.Name = "panelParam";
            this.panelParam.Size = new System.Drawing.Size(276, 28);
            this.panelParam.TabIndex = 122;
            // 
            // panelApply
            // 
            this.panelApply.Controls.Add(this.radioApplyAll);
            this.panelApply.Controls.Add(this.radioApplyB);
            this.panelApply.Controls.Add(this.radioApplyA);
            this.panelApply.Controls.Add(this.lblApplyTo);
            this.panelApply.Location = new System.Drawing.Point(4, 165);
            this.panelApply.Name = "panelApply";
            this.panelApply.Size = new System.Drawing.Size(276, 24);
            this.panelApply.TabIndex = 123;
            // 
            // radioApplyAll
            // 
            this.radioApplyAll.AutoSize = true;
            this.radioApplyAll.Location = new System.Drawing.Point(228, 3);
            this.radioApplyAll.Name = "radioApplyAll";
            this.radioApplyAll.Size = new System.Drawing.Size(36, 17);
            this.radioApplyAll.TabIndex = 122;
            this.radioApplyAll.Text = "All";
            this.radioApplyAll.UseVisualStyleBackColor = true;
            // 
            // radioApplyB
            // 
            this.radioApplyB.AutoSize = true;
            this.radioApplyB.Location = new System.Drawing.Point(164, 3);
            this.radioApplyB.Name = "radioApplyB";
            this.radioApplyB.Size = new System.Drawing.Size(60, 17);
            this.radioApplyB.TabIndex = 121;
            this.radioApplyB.Text = "Chan B";
            this.radioApplyB.UseVisualStyleBackColor = true;
            // 
            // radioApplyA
            // 
            this.radioApplyA.AutoSize = true;
            this.radioApplyA.Checked = true;
            this.radioApplyA.Location = new System.Drawing.Point(98, 3);
            this.radioApplyA.Name = "radioApplyA";
            this.radioApplyA.Size = new System.Drawing.Size(60, 17);
            this.radioApplyA.TabIndex = 120;
            this.radioApplyA.TabStop = true;
            this.radioApplyA.Text = "Chan A";
            this.radioApplyA.UseVisualStyleBackColor = true;
            // 
            // panelResult
            // 
            this.panelResult.Controls.Add(this.textResult);
            this.panelResult.Controls.Add(this.lblResult);
            this.panelResult.Location = new System.Drawing.Point(4, 135);
            this.panelResult.Name = "panelResult";
            this.panelResult.Size = new System.Drawing.Size(276, 32);
            this.panelResult.TabIndex = 124;
            // 
            // panelChannelB
            // 
            this.panelChannelB.Controls.Add(this.comboChannelB);
            this.panelChannelB.Controls.Add(this.lblChannelB);
            this.panelChannelB.Location = new System.Drawing.Point(4, 106);
            this.panelChannelB.Name = "panelChannelB";
            this.panelChannelB.Size = new System.Drawing.Size(276, 30);
            this.panelChannelB.TabIndex = 125;
            // 
            // lblApply
            // 
            this.lblApply.AutoSize = true;
            this.lblApply.ForeColor = System.Drawing.Color.White;
            this.lblApply.Location = new System.Drawing.Point(101, 60);
            this.lblApply.Name = "lblApply";
            this.lblApply.Size = new System.Drawing.Size(79, 13);
            this.lblApply.TabIndex = 126;
            this.lblApply.Text = "Samples X ... Y";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 60);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 13);
            this.label1.TabIndex = 127;
            this.label1.Text = "Apply To:";
            // 
            // Mathematical
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DimGray;
            this.ClientSize = new System.Drawing.Size(283, 259);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblApply);
            this.Controls.Add(this.panelChannelB);
            this.Controls.Add(this.panelResult);
            this.Controls.Add(this.panelApply);
            this.Controls.Add(this.panelParam);
            this.Controls.Add(this.comboChannelA);
            this.Controls.Add(this.linkHelp);
            this.Controls.Add(this.btnExecuteClose);
            this.Controls.Add(this.lblChannelA);
            this.Controls.Add(this.lblMathMode);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.comboMath);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.btnExecute);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Mathematical";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Math Operations";
            this.panelParam.ResumeLayout(false);
            this.panelParam.PerformLayout();
            this.panelApply.ResumeLayout(false);
            this.panelApply.PerformLayout();
            this.panelResult.ResumeLayout(false);
            this.panelResult.PerformLayout();
            this.panelChannelB.ResumeLayout(false);
            this.panelChannelB.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnExecute;
        private System.Windows.Forms.ComboBox comboMath;
        private System.Windows.Forms.Label lblChannelA;
        private System.Windows.Forms.Label lblChannelB;
        private System.Windows.Forms.ComboBox comboChannelB;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.TextBox textResult;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblMathMode;
        private System.Windows.Forms.Button btnExecuteClose;
        private System.Windows.Forms.LinkLabel linkHelp;
        private System.Windows.Forms.Label lblParam;
        private System.Windows.Forms.TextBox textParameter;
        private System.Windows.Forms.Label lblUnit;
        private System.Windows.Forms.ComboBox comboChannelA;
        private System.Windows.Forms.Label lblApplyTo;
        private System.Windows.Forms.Panel panelParam;
        private System.Windows.Forms.Panel panelApply;
        private System.Windows.Forms.Panel panelResult;
        private System.Windows.Forms.Panel panelChannelB;
        private System.Windows.Forms.RadioButton radioApplyAll;
        private System.Windows.Forms.RadioButton radioApplyB;
        private System.Windows.Forms.RadioButton radioApplyA;
        private System.Windows.Forms.Label lblApply;
        private System.Windows.Forms.Label label1;
    }
}