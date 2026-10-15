using System;
using System.Windows.Forms;
using LCS.Cache;
using LCS.Theming;

namespace LCS.Forms
{
    // Changes are saved immediately; the theme is applied right away.
    public partial class Parameters : LCS.Theming.ThemedForm
    {
        private bool _loading;

        public Parameters()
        {
            InitializeComponent();
            BuildLayout();
        }

        public bool Autorefresh { get; private set; }
        public bool Cancelled { get; private set; }
        public bool CachingEnabled { get; private set; }
        public bool StoreCache { get; private set; }

        private void BuildLayout()
        {
            page.AddHeader("Appearance");
            page.AddRow("App theme", "Light, dark, or the same as Windows. Applied immediately.", themeComboBox);

            page.AddHeader("Sign-in");
            page.AddRow("Keep me signed in", "Renew the LCS session in the background before it expires, so you do not have to sign in again every hour.", keepSessionAliveCheckBox);

            page.AddHeader("General");
            page.AddRow("Refresh environments after switching project", null, AutoRefreshCheckBox);
            page.AddRow("Minimize to the notification area", "Closing the window keeps 2LCS running in the notification area.", minimizeToNotificationArea);

            page.AddHeader("Credentials cache");
            page.AddRow("Cache credentials", "Remember environment credentials loaded from LCS while 2LCS is running.", CachingEnabledCheckbox);
            page.AddRow("Keep the cache between sessions", "Saved encrypted for your Windows account when 2LCS closes.", StoreCacheCheckBox);
            page.AddRow("Clear cached credentials", null, ClearCacheButton);

            page.AddHeader("Exports");
            page.AddRow("Exclude organizations", "Projects of organizations whose name contains this text are skipped when exporting all projects.", textBoxProjectExcl);
            page.AddRow("Include RDP credentials", "In the project data export.", RDPCredentialsCheckbox);
            page.AddRow("Include local credentials", "In the project data export.", LocalCredentialsCheckbox);

            page.AddHeader("Cloud-hosted environments");
            page.AddRow("Show the real VM state from Azure", "LCS only knows about actions done in LCS - not when a VM is shut down from Windows or in the Azure portal. Reads the power state from Azure (needs read access to the subscription).", azureVmStateCheckBox);
            page.AddRow("Check the VM state", "How often the state of running or stopped VMs is checked (one small request to Azure per VM). While a VM is starting or stopping, it is checked every minute until it settles; after Start or Stop from 2LCS a few times within 10 minutes.", vmRefreshComboBox);
            page.AddRow("Always connect as the admin user", "Skip choosing the user when opening a remote desktop connection.", alwaysLogAsAdmin);
            page.AddRow("Enable ms-2lcs:// links", "Lets links open remote desktop connections through 2LCS. Requires administrator rights.", uriSchemeEnabled);

            page.AddHeader("Microsoft-hosted environments");
            page.AddRow("Add my public IP address to new firewall rules", null, addExternalIpAddress);

            page.AddHeader("LCS addresses");
            page.AddRow("Lifecycle Services", null, textBoxLcsUrl);
            page.AddRow("Updates", null, textBoxLcsUpdateUrl);
            page.AddRow("Issue search", null, textBoxLcsFixUrl);
            page.AddRow("Diagnostics", null, textBoxLcsDiagUrl);

            foreach (var toggle in new CheckBox[] { keepSessionAliveCheckBox, AutoRefreshCheckBox, minimizeToNotificationArea, CachingEnabledCheckbox,
                StoreCacheCheckBox, RDPCredentialsCheckbox, LocalCredentialsCheckbox, alwaysLogAsAdmin, addExternalIpAddress, azureVmStateCheckBox })
            {
                toggle.CheckedChanged += (s, e) => SaveIfLoaded();
            }
            foreach (var textBox in new[] { textBoxProjectExcl, textBoxLcsUrl, textBoxLcsUpdateUrl, textBoxLcsFixUrl, textBoxLcsDiagUrl })
            {
                textBox.Validated += (s, e) => SaveIfLoaded();
            }
            vmRefreshComboBox.SelectedIndexChanged += (s, e) => SaveIfLoaded();
            StoreCacheCheckBox.CheckedChanged += (s, e) =>
            {
                if (!_loading && !StoreCacheCheckBox.Checked) CredentialsCacheHelper.DeleteStore();
            };
            themeComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_loading) return;
                SetParameters();
                ThemeManager.ApplyThemeChange();
            };
            FormClosing += (s, e) => SetParameters();
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        private void SaveIfLoaded()
        {
            if (!_loading) SetParameters();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            SetParameters();
            Close();
        }

        private void Parameters_Load(object sender, EventArgs e)
        {
            LoadParameters();
        }

        private void LoadParameters()
        {
            _loading = true;
            try
            {
                AutoRefreshCheckBox.Checked = Properties.Settings.Default.autorefresh;
                minimizeToNotificationArea.Checked = Properties.Settings.Default.minimizeToNotificationArea;
                textBoxProjectExcl.Text = Properties.Settings.Default.projOrgExcl;
                RDPCredentialsCheckbox.Checked = Properties.Settings.Default.RDPCredentialsEnabled;
                LocalCredentialsCheckbox.Checked = Properties.Settings.Default.LocalCredentialsEnabled;
                CachingEnabledCheckbox.Checked = Properties.Settings.Default.cachingEnabled;
                StoreCacheCheckBox.Checked = Properties.Settings.Default.keepCache;
                alwaysLogAsAdmin.Checked = Properties.Settings.Default.alwaysLogAsAdmin;
                uriSchemeEnabled.Checked = Properties.Settings.Default.uriSchemeEnabled;
                textBoxLcsUrl.Text = Properties.Settings.Default.lcsURL;
                textBoxLcsUpdateUrl.Text = Properties.Settings.Default.lcsUpdateURL;
                textBoxLcsDiagUrl.Text = Properties.Settings.Default.lcsDiagURL;
                textBoxLcsFixUrl.Text = Properties.Settings.Default.lcsFixURL;
                addExternalIpAddress.Checked = Properties.Settings.Default.populateOwnIPForNSG;
                keepSessionAliveCheckBox.Checked = Properties.Settings.Default.keepSessionAlive;
                azureVmStateCheckBox.Checked = Properties.Settings.Default.azureVmState;
                vmRefreshComboBox.SelectedIndex = Array.IndexOf(VmRefreshMinutes, Properties.Settings.Default.vmStateRefreshMinutes) is var index && index >= 0 ? index : 1;
                themeComboBox.SelectedIndex = Enum.TryParse(Properties.Settings.Default.theme, true, out ThemeMode mode) ? (int)mode : 0;

                SetStoreCacheEnabledDisabled();
            }
            finally
            {
                _loading = false;
            }
        }

        private void SetParameters()
        {
            Properties.Settings.Default.autorefresh = AutoRefreshCheckBox.Checked;
            Properties.Settings.Default.minimizeToNotificationArea = minimizeToNotificationArea.Checked;
            Properties.Settings.Default.projOrgExcl = textBoxProjectExcl.Text;
            Properties.Settings.Default.RDPCredentialsEnabled = RDPCredentialsCheckbox.Checked;
            Properties.Settings.Default.LocalCredentialsEnabled = LocalCredentialsCheckbox.Checked;
            Properties.Settings.Default.cachingEnabled = CachingEnabledCheckbox.Checked;
            Properties.Settings.Default.keepCache = StoreCacheCheckBox.Checked;
            Properties.Settings.Default.alwaysLogAsAdmin = alwaysLogAsAdmin.Checked;
            Properties.Settings.Default.uriSchemeEnabled = uriSchemeEnabled.Checked;
            Properties.Settings.Default.lcsURL = textBoxLcsUrl.Text;
            Properties.Settings.Default.lcsUpdateURL = textBoxLcsUpdateUrl.Text;
            Properties.Settings.Default.lcsDiagURL = textBoxLcsDiagUrl.Text;
            Properties.Settings.Default.lcsFixURL = textBoxLcsFixUrl.Text;
            Properties.Settings.Default.populateOwnIPForNSG = addExternalIpAddress.Checked;
            Properties.Settings.Default.keepSessionAlive = keepSessionAliveCheckBox.Checked;
            Properties.Settings.Default.azureVmState = azureVmStateCheckBox.Checked;
            Properties.Settings.Default.vmStateRefreshMinutes = VmRefreshMinutes[Math.Max(0, vmRefreshComboBox.SelectedIndex)];
            Properties.Settings.Default.theme = SelectedTheme.ToString();

            Properties.Settings.Default.Save();
        }

        // Items of vmRefreshComboBox; 0 = only with Refresh.
        private static readonly int[] VmRefreshMinutes = { 5, 15, 30, 60, 0 };

        private ThemeMode SelectedTheme => themeComboBox.SelectedIndex < 0 ? ThemeMode.System : (ThemeMode)themeComboBox.SelectedIndex;

        private void CachingEnabledCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            SetStoreCacheEnabledDisabled();
        }

        private void SetStoreCacheEnabledDisabled()
        {
            if (CachingEnabledCheckbox.Checked)
            {
                StoreCacheCheckBox.Enabled = true;
            }
            else
            {
                StoreCacheCheckBox.Enabled = false;
                StoreCacheCheckBox.Checked = false;
                CredentialsCacheHelper.DisableCache();
            }
            StoreCacheCheckBox.Parent?.Invalidate();
        }

        private void ClearCacheButton_Click(object sender, EventArgs e)
        {
            var result = CredentialsCacheHelper.ClearCache();

            if (result != null)
            {
                FluentMessageBox.Show(this, string.Format("Error while trying to clear cache, caching disabled.\n {0}", result), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                FluentMessageBox.Show(this, "Cached credentials were deleted.", "Credentials cache", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnUriSchemeEnabledCheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            if (Properties.Settings.Default.uriSchemeEnabled != uriSchemeEnabled.Checked)
            {
                try
                {
                    bool success = false;
                    if (uriSchemeEnabled.Checked)
                    {
                        success = URIHandler.RegisterHandler();
                    }
                    else
                    {
                        success = URIHandler.RemoveHandler();
                    }
                    if (success)
                    {
                        SetParameters();
                    }
                }
                finally
                {
                    LoadParameters();
                }
            }
        }
    }
}
