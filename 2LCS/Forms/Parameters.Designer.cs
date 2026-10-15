namespace LCS.Forms
{
    partial class Parameters
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
            page = new LCS.Theming.SettingsPage();
            footer = new System.Windows.Forms.Panel();
            closeButton = new System.Windows.Forms.Button();
            themeComboBox = new System.Windows.Forms.ComboBox();
            keepSessionAliveCheckBox = new LCS.Theming.ToggleSwitch();
            AutoRefreshCheckBox = new LCS.Theming.ToggleSwitch();
            minimizeToNotificationArea = new LCS.Theming.ToggleSwitch();
            CachingEnabledCheckbox = new LCS.Theming.ToggleSwitch();
            StoreCacheCheckBox = new LCS.Theming.ToggleSwitch();
            ClearCacheButton = new System.Windows.Forms.Button();
            textBoxProjectExcl = new System.Windows.Forms.TextBox();
            RDPCredentialsCheckbox = new LCS.Theming.ToggleSwitch();
            LocalCredentialsCheckbox = new LCS.Theming.ToggleSwitch();
            alwaysLogAsAdmin = new LCS.Theming.ToggleSwitch();
            uriSchemeEnabled = new LCS.Theming.ToggleSwitch();
            addExternalIpAddress = new LCS.Theming.ToggleSwitch();
            azureVmStateCheckBox = new LCS.Theming.ToggleSwitch();
            vmRefreshComboBox = new System.Windows.Forms.ComboBox();
            textBoxLcsUrl = new System.Windows.Forms.TextBox();
            textBoxLcsUpdateUrl = new System.Windows.Forms.TextBox();
            textBoxLcsFixUrl = new System.Windows.Forms.TextBox();
            textBoxLcsDiagUrl = new System.Windows.Forms.TextBox();
            footer.SuspendLayout();
            SuspendLayout();
            //
            // page
            //
            page.Dock = System.Windows.Forms.DockStyle.Fill;
            page.Name = "page";
            page.Padding = new System.Windows.Forms.Padding(24, 8, 24, 16);
            page.TabIndex = 0;
            //
            // footer
            //
            footer.Controls.Add(closeButton);
            footer.Dock = System.Windows.Forms.DockStyle.Bottom;
            footer.Name = "footer";
            footer.Size = new System.Drawing.Size(640, 64);
            footer.TabIndex = 1;
            //
            // closeButton
            //
            closeButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            closeButton.Location = new System.Drawing.Point(496, 16);
            closeButton.Name = "closeButton";
            closeButton.Size = new System.Drawing.Size(120, 32);
            closeButton.TabIndex = 0;
            closeButton.Text = "Close";
            closeButton.Click += OkButton_Click;
            //
            // themeComboBox
            //
            themeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            themeComboBox.Items.AddRange(new object[] { "Use Windows setting", "Light", "Dark" });
            themeComboBox.Name = "themeComboBox";
            themeComboBox.Size = new System.Drawing.Size(200, 28);
            //
            // toggles
            //
            keepSessionAliveCheckBox.Name = "keepSessionAliveCheckBox";
            keepSessionAliveCheckBox.Text = "Keep me signed in";
            AutoRefreshCheckBox.Name = "AutoRefreshCheckBox";
            AutoRefreshCheckBox.Text = "Refresh environments after switching project";
            minimizeToNotificationArea.Name = "minimizeToNotificationArea";
            minimizeToNotificationArea.Text = "Closing the window minimizes to the notification area";
            CachingEnabledCheckbox.Name = "CachingEnabledCheckbox";
            CachingEnabledCheckbox.Text = "Cache credentials";
            CachingEnabledCheckbox.CheckedChanged += CachingEnabledCheckbox_CheckedChanged;
            StoreCacheCheckBox.Name = "StoreCacheCheckBox";
            StoreCacheCheckBox.Text = "Keep the cache between sessions";
            RDPCredentialsCheckbox.Name = "RDPCredentialsCheckbox";
            RDPCredentialsCheckbox.Text = "Include RDP credentials";
            LocalCredentialsCheckbox.Name = "LocalCredentialsCheckbox";
            LocalCredentialsCheckbox.Text = "Include local credentials";
            alwaysLogAsAdmin.Name = "alwaysLogAsAdmin";
            alwaysLogAsAdmin.Text = "Always connect as the admin user";
            uriSchemeEnabled.Name = "uriSchemeEnabled";
            uriSchemeEnabled.Text = "Enable URI scheme support";
            uriSchemeEnabled.CheckedChanged += OnUriSchemeEnabledCheckedChanged;
            vmRefreshComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            vmRefreshComboBox.Items.AddRange(new object[] { "Every 5 minutes", "Every 15 minutes", "Every 30 minutes", "Every 60 minutes", "Only with Refresh (F5)" });
            vmRefreshComboBox.Name = "vmRefreshComboBox";
            vmRefreshComboBox.Size = new System.Drawing.Size(200, 28);
            azureVmStateCheckBox.Name = "azureVmStateCheckBox";
            azureVmStateCheckBox.Text = "Show the real VM state from Azure";
            addExternalIpAddress.Name = "addExternalIpAddress";
            addExternalIpAddress.Text = "Add external IP when creating firewall exception";
            //
            // ClearCacheButton
            //
            ClearCacheButton.Name = "ClearCacheButton";
            ClearCacheButton.Size = new System.Drawing.Size(120, 32);
            ClearCacheButton.Text = "Clear cache";
            ClearCacheButton.Click += ClearCacheButton_Click;
            //
            // text boxes
            //
            textBoxProjectExcl.Name = "textBoxProjectExcl";
            textBoxProjectExcl.Size = new System.Drawing.Size(200, 27);
            textBoxLcsUrl.Name = "textBoxLcsUrl";
            textBoxLcsUrl.Size = new System.Drawing.Size(260, 27);
            textBoxLcsUpdateUrl.Name = "textBoxLcsUpdateUrl";
            textBoxLcsUpdateUrl.Size = new System.Drawing.Size(260, 27);
            textBoxLcsFixUrl.Name = "textBoxLcsFixUrl";
            textBoxLcsFixUrl.Size = new System.Drawing.Size(260, 27);
            textBoxLcsDiagUrl.Name = "textBoxLcsDiagUrl";
            textBoxLcsDiagUrl.Size = new System.Drawing.Size(260, 27);
            //
            // Parameters
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(640, 720);
            Controls.Add(page);
            Controls.Add(footer);
            Icon = Properties.Resources.favicon_blue;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(520, 420);
            Name = "Parameters";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Settings";
            Load += Parameters_Load;
            footer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private LCS.Theming.SettingsPage page;
        private System.Windows.Forms.Panel footer;
        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.ComboBox themeComboBox;
        private LCS.Theming.ToggleSwitch keepSessionAliveCheckBox;
        private LCS.Theming.ToggleSwitch AutoRefreshCheckBox;
        private LCS.Theming.ToggleSwitch minimizeToNotificationArea;
        private LCS.Theming.ToggleSwitch CachingEnabledCheckbox;
        private LCS.Theming.ToggleSwitch StoreCacheCheckBox;
        private System.Windows.Forms.Button ClearCacheButton;
        private System.Windows.Forms.TextBox textBoxProjectExcl;
        private LCS.Theming.ToggleSwitch RDPCredentialsCheckbox;
        private LCS.Theming.ToggleSwitch LocalCredentialsCheckbox;
        private LCS.Theming.ToggleSwitch alwaysLogAsAdmin;
        private LCS.Theming.ToggleSwitch uriSchemeEnabled;
        private LCS.Theming.ToggleSwitch addExternalIpAddress;
        private LCS.Theming.ToggleSwitch azureVmStateCheckBox;
        private System.Windows.Forms.ComboBox vmRefreshComboBox;
        private System.Windows.Forms.TextBox textBoxLcsUrl;
        private System.Windows.Forms.TextBox textBoxLcsUpdateUrl;
        private System.Windows.Forms.TextBox textBoxLcsFixUrl;
        private System.Windows.Forms.TextBox textBoxLcsDiagUrl;
    }
}
