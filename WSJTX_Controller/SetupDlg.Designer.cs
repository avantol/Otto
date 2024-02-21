namespace WSJTX_Controller
{
    partial class SetupDlg
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.overrideCheckBox = new System.Windows.Forms.CheckBox();
            this.udpHelpLabel = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.addrLabel = new System.Windows.Forms.Label();
            this.multicastcheckBox = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.portTextBox = new System.Windows.Forms.TextBox();
            this.addrTextBox = new System.Windows.Forms.TextBox();
            this.OkButton = new System.Windows.Forms.Button();
            this.CancelButton = new System.Windows.Forms.Button();
            this.onTopCheckBox = new System.Windows.Forms.CheckBox();
            this.diagLogCheckBox = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.downButton = new System.Windows.Forms.Button();
            this.upButton = new System.Windows.Forms.Button();
            this.pctLabel = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.overrideCheckBox);
            this.groupBox1.Controls.Add(this.udpHelpLabel);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.addrLabel);
            this.groupBox1.Controls.Add(this.multicastcheckBox);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.portTextBox);
            this.groupBox1.Controls.Add(this.addrTextBox);
            this.groupBox1.Location = new System.Drawing.Point(13, 13);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(360, 139);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "UDP server settings";
            // 
            // overrideCheckBox
            // 
            this.overrideCheckBox.AutoSize = true;
            this.overrideCheckBox.Location = new System.Drawing.Point(12, 25);
            this.overrideCheckBox.Name = "overrideCheckBox";
            this.overrideCheckBox.Size = new System.Drawing.Size(259, 17);
            this.overrideCheckBox.TabIndex = 8;
            this.overrideCheckBox.Text = "Override automatic detection (not recommended!)";
            this.overrideCheckBox.UseVisualStyleBackColor = true;
            this.overrideCheckBox.CheckedChanged += new System.EventHandler(this.overrideCheckBox_CheckedChanged);
            // 
            // udpHelpLabel
            // 
            this.udpHelpLabel.AutoSize = true;
            this.udpHelpLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.udpHelpLabel.ForeColor = System.Drawing.Color.Blue;
            this.udpHelpLabel.Location = new System.Drawing.Point(109, 0);
            this.udpHelpLabel.Name = "udpHelpLabel";
            this.udpHelpLabel.Size = new System.Drawing.Size(14, 13);
            this.udpHelpLabel.TabIndex = 7;
            this.udpHelpLabel.Text = "?";
            this.udpHelpLabel.Click += new System.EventHandler(this.udpHelpLabel_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(231, 82);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(86, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "(Standard: 2237)";
            // 
            // addrLabel
            // 
            this.addrLabel.AutoSize = true;
            this.addrLabel.Location = new System.Drawing.Point(230, 52);
            this.addrLabel.Name = "addrLabel";
            this.addrLabel.Size = new System.Drawing.Size(116, 13);
            this.addrLabel.TabIndex = 5;
            this.addrLabel.Text = "(Standard: 239.255.0.0";
            // 
            // multicastcheckBox
            // 
            this.multicastcheckBox.AutoSize = true;
            this.multicastcheckBox.Location = new System.Drawing.Point(32, 109);
            this.multicastcheckBox.Name = "multicastcheckBox";
            this.multicastcheckBox.Size = new System.Drawing.Size(298, 17);
            this.multicastcheckBox.TabIndex = 4;
            this.multicastcheckBox.Text = "Mullticast (also select an \"Outgoing interface\" in WSJT-X)";
            this.multicastcheckBox.UseVisualStyleBackColor = true;
            this.multicastcheckBox.CheckedChanged += new System.EventHandler(this.multicastcheckBox_CheckedChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(30, 82);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "UDP port:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(29, 54);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "UDP address:";
            // 
            // portTextBox
            // 
            this.portTextBox.Location = new System.Drawing.Point(111, 79);
            this.portTextBox.Name = "portTextBox";
            this.portTextBox.Size = new System.Drawing.Size(113, 20);
            this.portTextBox.TabIndex = 1;
            // 
            // addrTextBox
            // 
            this.addrTextBox.Location = new System.Drawing.Point(111, 50);
            this.addrTextBox.Name = "addrTextBox";
            this.addrTextBox.Size = new System.Drawing.Size(113, 20);
            this.addrTextBox.TabIndex = 0;
            // 
            // OkButton
            // 
            this.OkButton.Location = new System.Drawing.Point(105, 232);
            this.OkButton.Name = "OkButton";
            this.OkButton.Size = new System.Drawing.Size(75, 23);
            this.OkButton.TabIndex = 1;
            this.OkButton.Text = "OK";
            this.OkButton.UseVisualStyleBackColor = true;
            this.OkButton.Click += new System.EventHandler(this.OkButton_Click);
            // 
            // CancelButton
            // 
            this.CancelButton.Location = new System.Drawing.Point(210, 232);
            this.CancelButton.Name = "CancelButton";
            this.CancelButton.Size = new System.Drawing.Size(75, 23);
            this.CancelButton.TabIndex = 2;
            this.CancelButton.Text = "Cancel";
            this.CancelButton.UseVisualStyleBackColor = true;
            this.CancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // onTopCheckBox
            // 
            this.onTopCheckBox.AutoSize = true;
            this.onTopCheckBox.Location = new System.Drawing.Point(23, 183);
            this.onTopCheckBox.Name = "onTopCheckBox";
            this.onTopCheckBox.Size = new System.Drawing.Size(138, 17);
            this.onTopCheckBox.TabIndex = 3;
            this.onTopCheckBox.Text = "Controller always on top";
            this.onTopCheckBox.UseVisualStyleBackColor = true;
            // 
            // diagLogCheckBox
            // 
            this.diagLogCheckBox.AutoSize = true;
            this.diagLogCheckBox.Location = new System.Drawing.Point(23, 206);
            this.diagLogCheckBox.Name = "diagLogCheckBox";
            this.diagLogCheckBox.Size = new System.Drawing.Size(115, 17);
            this.diagLogCheckBox.TabIndex = 6;
            this.diagLogCheckBox.Text = "Log diagnostic info";
            this.diagLogCheckBox.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(23, 162);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(114, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Controller window size:";
            // 
            // downButton
            // 
            this.downButton.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.downButton.Location = new System.Drawing.Point(143, 158);
            this.downButton.Name = "downButton";
            this.downButton.Size = new System.Drawing.Size(37, 23);
            this.downButton.TabIndex = 8;
            this.downButton.Text = "-";
            this.downButton.UseVisualStyleBackColor = true;
            this.downButton.Click += new System.EventHandler(this.downButton_Click);
            // 
            // upButton
            // 
            this.upButton.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.upButton.Location = new System.Drawing.Point(237, 158);
            this.upButton.Name = "upButton";
            this.upButton.Size = new System.Drawing.Size(37, 23);
            this.upButton.TabIndex = 9;
            this.upButton.Text = "+";
            this.upButton.UseVisualStyleBackColor = true;
            this.upButton.Click += new System.EventHandler(this.upButton_Click);
            // 
            // pctLabel
            // 
            this.pctLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pctLabel.Location = new System.Drawing.Point(184, 158);
            this.pctLabel.Name = "pctLabel";
            this.pctLabel.Size = new System.Drawing.Size(48, 23);
            this.pctLabel.TabIndex = 10;
            this.pctLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // SetupDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(385, 264);
            this.Controls.Add(this.pctLabel);
            this.Controls.Add(this.upButton);
            this.Controls.Add(this.downButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.diagLogCheckBox);
            this.Controls.Add(this.onTopCheckBox);
            this.Controls.Add(this.CancelButton);
            this.Controls.Add(this.OkButton);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "SetupDlg";
            this.Text = "Setup";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SetupDlg_FormClosed);
            this.Load += new System.EventHandler(this.SetupDlg_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox portTextBox;
        private System.Windows.Forms.TextBox addrTextBox;
        private System.Windows.Forms.Button OkButton;
        private System.Windows.Forms.Button CancelButton;
        private System.Windows.Forms.CheckBox multicastcheckBox;
        private System.Windows.Forms.CheckBox onTopCheckBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label addrLabel;
        private System.Windows.Forms.CheckBox diagLogCheckBox;
        private System.Windows.Forms.Label udpHelpLabel;
        private System.Windows.Forms.CheckBox overrideCheckBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button downButton;
        private System.Windows.Forms.Button upButton;
        private System.Windows.Forms.Label pctLabel;
    }
}