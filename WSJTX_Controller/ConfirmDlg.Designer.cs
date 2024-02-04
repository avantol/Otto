namespace WSJTX_Controller
{
    partial class ConfirmDlg
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
            this.nobutton = new System.Windows.Forms.Button();
            this.textBox = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.yesButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // nobutton
            // 
            this.nobutton.BackColor = System.Drawing.SystemColors.Control;
            this.nobutton.DialogResult = System.Windows.Forms.DialogResult.No;
            this.nobutton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.nobutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nobutton.Location = new System.Drawing.Point(154, 74);
            this.nobutton.Name = "nobutton";
            this.nobutton.Size = new System.Drawing.Size(80, 23);
            this.nobutton.TabIndex = 2;
            this.nobutton.Text = "No";
            this.nobutton.UseVisualStyleBackColor = false;
            this.nobutton.Click += new System.EventHandler(this.nobutton_Click);
            // 
            // textBox
            // 
            this.textBox.BackColor = System.Drawing.SystemColors.Window;
            this.textBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox.Location = new System.Drawing.Point(68, 26);
            this.textBox.MaximumSize = new System.Drawing.Size(190, 17);
            this.textBox.MinimumSize = new System.Drawing.Size(190, 17);
            this.textBox.Multiline = true;
            this.textBox.Name = "textBox";
            this.textBox.Size = new System.Drawing.Size(190, 17);
            this.textBox.TabIndex = 0;
            this.textBox.TabStop = false;
            // 
            // panel1
            // 
            this.panel1.Location = new System.Drawing.Point(9, 10);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(48, 44);
            this.panel1.TabIndex = 3;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.SystemColors.Control;
            this.panel2.Location = new System.Drawing.Point(-3, 63);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(279, 43);
            this.panel2.TabIndex = 4;
            // 
            // yesButton
            // 
            this.yesButton.BackColor = System.Drawing.SystemColors.Control;
            this.yesButton.DialogResult = System.Windows.Forms.DialogResult.Yes;
            this.yesButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.yesButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.yesButton.Location = new System.Drawing.Point(57, 74);
            this.yesButton.Name = "yesButton";
            this.yesButton.Size = new System.Drawing.Size(80, 23);
            this.yesButton.TabIndex = 1;
            this.yesButton.Text = "Yes";
            this.yesButton.UseVisualStyleBackColor = false;
            this.yesButton.Click += new System.EventHandler(this.yesButton_Click);
            // 
            // ConfirmDlg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(274, 104);
            this.ControlBox = false;
            this.Controls.Add(this.yesButton);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.nobutton);
            this.Controls.Add(this.textBox);
            this.Controls.Add(this.panel2);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(280, 110);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(280, 110);
            this.Name = "ConfirmDlg";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ConfirmDlg_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ConfirmDlg_FormClosed);
            this.Load += new System.EventHandler(this.ConfirmDlg_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button nobutton;
        public System.Windows.Forms.TextBox textBox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button yesButton;
    }
}