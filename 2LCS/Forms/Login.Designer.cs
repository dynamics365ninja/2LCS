namespace LCS.Forms
{
    partial class Login
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
            headerPanel = new System.Windows.Forms.Panel();
            subtitleLabel = new System.Windows.Forms.Label();
            titleLabel = new System.Windows.Forms.Label();
            progressLine = new LCS.Theming.ProgressLine();
            webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
            SuspendLayout();
            //
            // headerPanel
            //
            headerPanel.Controls.Add(subtitleLabel);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            headerPanel.Location = new System.Drawing.Point(0, 0);
            headerPanel.Name = "headerPanel";
            headerPanel.Padding = new System.Windows.Forms.Padding(20, 12, 20, 10);
            headerPanel.Size = new System.Drawing.Size(720, 76);
            headerPanel.TabIndex = 0;
            //
            // subtitleLabel
            //
            subtitleLabel.AutoEllipsis = true;
            subtitleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            subtitleLabel.Location = new System.Drawing.Point(20, 44);
            subtitleLabel.Name = "subtitleLabel";
            subtitleLabel.Size = new System.Drawing.Size(680, 22);
            subtitleLabel.TabIndex = 1;
            subtitleLabel.Text = "Use your work account. 2LCS keeps you signed in and renews the session in the background.";
            //
            // titleLabel
            //
            titleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            titleLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 13F);
            titleLabel.Location = new System.Drawing.Point(20, 12);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new System.Drawing.Size(680, 32);
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Sign in to Lifecycle Services";
            //
            // progressLine
            //
            progressLine.Dock = System.Windows.Forms.DockStyle.Top;
            progressLine.Location = new System.Drawing.Point(0, 76);
            progressLine.Name = "progressLine";
            progressLine.Size = new System.Drawing.Size(720, 3);
            progressLine.TabIndex = 1;
            //
            // webView
            //
            webView.AllowExternalDrop = false;
            webView.CreationProperties = null;
            webView.DefaultBackgroundColor = System.Drawing.Color.White;
            webView.Dock = System.Windows.Forms.DockStyle.Fill;
            webView.Location = new System.Drawing.Point(0, 79);
            webView.Name = "webView";
            webView.Size = new System.Drawing.Size(720, 641);
            webView.TabIndex = 2;
            webView.ZoomFactor = 1D;
            //
            // Login
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(720, 720);
            Controls.Add(webView);
            Controls.Add(progressLine);
            Controls.Add(headerPanel);
            Icon = Properties.Resources.favicon_blue;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(480, 480);
            Name = "Login";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Sign in to LCS";
            Load += Login_Load;
            headerPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)webView).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label subtitleLabel;
        private System.Windows.Forms.Label titleLabel;
        private LCS.Theming.ProgressLine progressLine;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
    }
}
