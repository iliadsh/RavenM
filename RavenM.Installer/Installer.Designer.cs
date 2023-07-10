namespace RavenM.Installer
{
    partial class RavenM
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RavenM));
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.RavenMFolder = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.Version = new System.Windows.Forms.GroupBox();
            this.Dev = new System.Windows.Forms.RadioButton();
            this.Release = new System.Windows.Forms.RadioButton();
            this.FolderPicker = new System.Windows.Forms.Button();
            this.Install = new System.Windows.Forms.Button();
            this.DoneText = new System.Windows.Forms.Label();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.Version.SuspendLayout();
            this.SuspendLayout();
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(12, 126);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(360, 23);
            this.progressBar1.TabIndex = 0;
            // 
            // RavenMFolder
            // 
            this.RavenMFolder.Enabled = false;
            this.RavenMFolder.Location = new System.Drawing.Point(12, 27);
            this.RavenMFolder.Name = "RavenMFolder";
            this.RavenMFolder.ReadOnly = true;
            this.RavenMFolder.Size = new System.Drawing.Size(292, 20);
            this.RavenMFolder.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(83, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Ravenfield Path";
            // 
            // Version
            // 
            this.Version.Controls.Add(this.Dev);
            this.Version.Controls.Add(this.Release);
            this.Version.Location = new System.Drawing.Point(12, 54);
            this.Version.Name = "Version";
            this.Version.Size = new System.Drawing.Size(232, 66);
            this.Version.TabIndex = 3;
            this.Version.TabStop = false;
            this.Version.Text = "RavenM Version";
            // 
            // Dev
            // 
            this.Dev.AutoSize = true;
            this.Dev.Location = new System.Drawing.Point(7, 43);
            this.Dev.Name = "Dev";
            this.Dev.Size = new System.Drawing.Size(45, 17);
            this.Dev.TabIndex = 1;
            this.Dev.TabStop = true;
            this.Dev.Text = "Dev";
            this.Dev.UseVisualStyleBackColor = true;
            // 
            // Release
            // 
            this.Release.AutoSize = true;
            this.Release.Location = new System.Drawing.Point(7, 20);
            this.Release.Name = "Release";
            this.Release.Size = new System.Drawing.Size(64, 17);
            this.Release.TabIndex = 0;
            this.Release.TabStop = true;
            this.Release.Text = "Release";
            this.Release.UseVisualStyleBackColor = true;
            // 
            // FolderPicker
            // 
            this.FolderPicker.Location = new System.Drawing.Point(312, 27);
            this.FolderPicker.Name = "FolderPicker";
            this.FolderPicker.Size = new System.Drawing.Size(60, 20);
            this.FolderPicker.TabIndex = 4;
            this.FolderPicker.Text = "Browse";
            this.FolderPicker.UseVisualStyleBackColor = true;
            this.FolderPicker.Click += new System.EventHandler(this.FolderPicker_Click);
            // 
            // Install
            // 
            this.Install.Location = new System.Drawing.Point(297, 84);
            this.Install.Name = "Install";
            this.Install.Size = new System.Drawing.Size(75, 23);
            this.Install.TabIndex = 5;
            this.Install.Text = "Install";
            this.Install.UseVisualStyleBackColor = true;
            this.Install.Click += new System.EventHandler(this.Install_Click);
            // 
            // DoneText
            // 
            this.DoneText.Location = new System.Drawing.Point(250, 110);
            this.DoneText.Name = "DoneText";
            this.DoneText.Size = new System.Drawing.Size(132, 13);
            this.DoneText.TabIndex = 6;
            this.DoneText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RavenM
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 161);
            this.Controls.Add(this.DoneText);
            this.Controls.Add(this.Install);
            this.Controls.Add(this.FolderPicker);
            this.Controls.Add(this.Version);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.RavenMFolder);
            this.Controls.Add(this.progressBar1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "RavenM";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "RavenM Installer";
            this.Load += new System.EventHandler(this.Installer_Load);
            this.Version.ResumeLayout(false);
            this.Version.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.TextBox RavenMFolder;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox Version;
        private System.Windows.Forms.RadioButton Dev;
        private System.Windows.Forms.RadioButton Release;
        private System.Windows.Forms.Button FolderPicker;
        private System.Windows.Forms.Button Install;
        private System.Windows.Forms.Label DoneText;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
    }
}

