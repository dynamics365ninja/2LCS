using LCS.Auth;
using LCS.Theming;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.Forms
{
    public partial class MainForm
    {
        private static readonly Dictionary<string, string> MenuGlyphs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Logon to application"] = Glyph.OpenInNew,
            ["Open RDP connection"] = Glyph.Monitor,
            ["Manage instance"] = Glyph.Manage,
            ["Start instance(s)"] = Glyph.Play,
            ["Stop instance(s)"] = Glyph.Stop,
            ["Deallocate instance"] = Glyph.Power,
            ["Delete instance"] = Glyph.Delete,
            ["Add firewall exception"] = Glyph.Shield,
            ["Delete firewall exception"] = Glyph.Shield,
            ["Restart service"] = Glyph.Restart,
            ["RDP and passwords"] = Glyph.Key,
            ["Show passwords"] = Glyph.PasswordShow,
            ["Show passwords (powershell)"] = Glyph.Command,
            ["Show RDP details"] = Glyph.Info,
            ["Export RDCMan connections"] = Glyph.Export,
            ["Export RDM connections"] = Glyph.Export,
            ["Available hotfixes"] = Glyph.Repair,
            ["Upcoming updates"] = Glyph.Calendar,
            ["Detailed build info"] = Glyph.Document,
            ["Environment changes"] = Glyph.History,
            ["LCS instance links"] = Glyph.Globe,
            ["LCS project links"] = Glyph.Link,
            ["Custom links"] = Glyph.Link,
            ["Deploy package"] = Glyph.Package,
            ["Close 2LCS"] = Glyph.Close
        };

        private HeaderBar _header;
        private PivotStrip _pivot;
        private EnvironmentDetailsPane _details;
        private Panel _detailsSpacer;
        private ProgressLine _progress;
        private ToolStripStatusLabel _statusRightLabel;
        private DateTime? _lastRefreshed;
        private string _busyText;
        private bool _filtering;

        private int Px(float value) => (int)Math.Round(value * DeviceDpi / 96f);

        private int IconSize => Px(16);

        private void InitializeModernUi()
        {
            var p = Theme.Current;
            SuspendLayout();

            _header = new HeaderBar { Dock = DockStyle.Top, Height = Px(60), BackColor = p.Window };
            _header.ProjectClick += (s, e) =>
            {
                if (changeProjectMenuItem.Enabled) ChangeProjectMenuItem_Click(s, e);
            };
            _header.SessionClick += OnSessionIndicatorClick;

            StyleCommandBar();
            StyleContextMenus();
            StyleStatusBar();

            var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Px(16), 0, Px(16), Px(12)), BackColor = p.Window };
            _pivot = new PivotStrip { Dock = DockStyle.Top, Height = Px(52), BackColor = p.Window, Tabs = tabControl };
            _pivot.Search.Placeholder = "Filter environments (Ctrl+F)";
            _pivot.Search.QueryChanged += (s, e) =>
            {
                ApplyFilter();
                Properties.Settings.Default.lastFilter = _pivot.Search.Query;
            };
            _infoBar = new InfoBar { Dock = DockStyle.Top, Height = Px(56), BackColor = p.Window };
            _progress = new ProgressLine { Dock = DockStyle.Top, Height = Px(2), BackColor = p.Window };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = p.Window };
            var card = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = p.Stroke };
            _detailsSpacer = new Panel { Dock = DockStyle.Right, Width = Px(12), BackColor = p.Window };
            _details = new EnvironmentDetailsPane { Dock = DockStyle.Right, Width = Px(340), BackColor = p.Window };

            Controls.Remove(tabControl);
            tabControl.Dock = DockStyle.Fill;
            card.Controls.Add(tabControl);
            body.Controls.Add(card);
            body.Controls.Add(_detailsSpacer);
            body.Controls.Add(_details);
            contentHost.Controls.Add(body);
            contentHost.Controls.Add(_progress);
            contentHost.Controls.Add(_infoBar);
            contentHost.Controls.Add(_pivot);
            foreach (TabPage page in tabControl.TabPages)
            {
                page.Padding = Padding.Empty;
                page.UseVisualStyleBackColor = false;
                page.BackColor = p.Surface;
            }

            Controls.Add(contentHost);
            Controls.Add(_header);
            // Docking order: status bar, header, command bar, content.
            Controls.SetChildIndex(contentHost, 0);
            Controls.SetChildIndex(mainMenuStrip, 1);
            Controls.SetChildIndex(_header, 2);
            Controls.SetChildIndex(statusStrip, 3);

            CreateVmStateColumn();
            GridStyler.Apply(cheDataGridView, new GridOptions
            {
                StatusColumns = new HashSet<string> { cheDeploymentStatus.Name, _vmStateColumn.Name },
                WarningColumns = new HashSet<string> { _vmStateColumn.Name },
                IsWarning = item => item is JsonObjects.CloudHostedInstance instance && instance.VmStateMismatch,
                EmptyState = () => EmptyState("cloud-hosted environments")
            });
            GridStyler.Apply(saasDataGridView, new GridOptions
            {
                StatusColumns = new HashSet<string> { saasDeploymentStatus.Name },
                EmptyState = () => EmptyState("Microsoft-hosted environments")
            });
            cheDataGridView.DataBindingComplete += (s, e) => OnGridDataChanged((DataGridView)s);
            saasDataGridView.DataBindingComplete += (s, e) => OnGridDataChanged((DataGridView)s);
            InitializeGridFeatures();

            loginToLcsMenuItem.EnabledChanged += (s, e) => SyncAuthCommands();
            logoutToolStripMenuItem.EnabledChanged += (s, e) => SyncAuthCommands();
            changeProjectMenuItem.EnabledChanged += (s, e) => UpdateHeader();
            LcsSession.Current.StateChanged += OnSessionStateChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
            FormClosed += (s, e) =>
            {
                LcsSession.Current.StateChanged -= OnSessionStateChanged;
                ThemeManager.ThemeChanged -= OnThemeChanged;
            };
            FormClosing += (s, e) => SaveWindowState();

            ResumeLayout(true);

            RestoreWindowState();
            InitializeLiveState();
            SyncAuthCommands();
            UpdateHeader();
            UpdateCounts();
            UpdateSessionIndicator();
            UpdateSelectionUi();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && _pivot != null)
            {
                _pivot.Search.FocusSearch();
                return true;
            }
            if (keyData == Keys.F5 && refreshMenuItem.Enabled && refreshMenuItem.Visible)
            {
                refreshMenuItem.PerformClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void StyleCommandBar()
        {
            var p = Theme.Current;
            mainMenuStrip.ImageScalingSize = new Size(IconSize, IconSize);
            mainMenuStrip.Padding = new Padding(Px(12), 0, Px(12), Px(6));
            mainMenuStrip.ShowItemToolTips = true;
            mainMenuStrip.BackColor = p.Window;

            if (_cmdLogon == null) CreateContextCommands();

            Command(loginToLcsMenuItem, Glyph.Person);
            Command(changeProjectMenuItem, Glyph.Switch);
            Command(refreshMenuItem, Glyph.Refresh);
            Command(customLinksToolStripMenuItem, Glyph.Link);
            Command(exportToolStripMenuItem, Glyph.Export);
            Command(_cmdLogon, Glyph.OpenInNew);
            Command(_cmdConnect, Glyph.Monitor);
            Command(_cmdStart, Glyph.Play);
            Command(_cmdStop, Glyph.Stop);
            Command(_cmdMore, "", iconOnly: true);
            Command(logoutToolStripMenuItem, Glyph.SignOut);
            Command(_cmdDetails, DetailsVisible ? "" : "", iconOnly: true);
            Command(cookieToolStripMenuItem, Glyph.Key, iconOnly: true);
            Command(parametersToolStripMenuItem, Glyph.Settings, iconOnly: true);
            Command(aboutToolStripMenuItem, Glyph.Info, iconOnly: true);
            refreshMenuItem.ToolTipText = "Refresh environments from LCS (F5)";

            void Command(ToolStripMenuItem item, string glyph, bool iconOnly = false)
            {
                item.Image = FluentIcons.Get(glyph, p.Text, IconSize);
                item.ImageScaling = ToolStripItemImageScaling.None;
                item.Padding = new Padding(Px(8), Px(7), Px(8), Px(7));
                item.Margin = new Padding(Px(1), 0, Px(1), 0);
                if (iconOnly)
                {
                    item.DisplayStyle = ToolStripItemDisplayStyle.Image;
                    item.Padding = new Padding(Px(7), Px(7), Px(7), Px(7));
                }
            }
        }

        private void StyleContextMenus()
        {
            foreach (var menu in new[] { cheInstanceContextMenu, saasInstanceContextMenu, notifyIconContextMenu })
            {
                ThemeManager.StyleToolStrip(menu);
                menu.ImageScalingSize = new Size(IconSize, IconSize);
                ApplyMenuIcons(menu.Items);
                // Custom links are created at runtime.
                menu.Opening += (s, e) => ApplyMenuIcons(((ContextMenuStrip)s).Items);
            }
        }

        private void ApplyMenuIcons(ToolStripItemCollection items, bool force = false)
        {
            var color = Theme.Current.Text;
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if ((menuItem.Image == null || force) && MenuGlyphs.TryGetValue(menuItem.Text.Replace("&", ""), out var glyph))
                    {
                        menuItem.Image = FluentIcons.Get(glyph, color, IconSize);
                        menuItem.ImageScaling = ToolStripItemImageScaling.None;
                    }
                    if (menuItem.HasDropDownItems)
                    {
                        menuItem.DropDown.ImageScalingSize = new Size(IconSize, IconSize);
                        ApplyMenuIcons(menuItem.DropDownItems, force);
                    }
                }
            }
        }

        private void StyleStatusBar()
        {
            statusStrip.Padding = new Padding(Px(16), Px(3), Px(16), Px(3));
            statusStrip.SizingGrip = false;
            // The header shows the project.
            projectDescriptionLabel.Visible = false;
            toolStripStatusLabel1.Spring = true;
            toolStripStatusLabel1.TextAlign = ContentAlignment.MiddleLeft;
            _statusRightLabel = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight };
            statusStrip.Items.Add(_statusRightLabel);
        }

        private void SyncAuthCommands()
        {
            // Show either "Sign in" or "Sign out", not a disabled one of them.
            loginToLcsMenuItem.Visible = loginToLcsMenuItem.Enabled;
            logoutToolStripMenuItem.Visible = logoutToolStripMenuItem.Enabled;
            UpdateHeader();
        }

        private void UpdateHeader()
        {
            if (_header == null) return;
            if (_selectedProject != null)
            {
                var detail = string.IsNullOrEmpty(_selectedProject.OrganizationName)
                    ? $"#{_selectedProject.Id}"
                    : $"{_selectedProject.OrganizationName} · #{_selectedProject.Id}";
                _header.SetProject(_selectedProject.Name, detail, changeProjectMenuItem.Enabled);
            }
            else
            {
                _header.SetProject(null, null, changeProjectMenuItem.Enabled);
            }
            cheDataGridView.Invalidate();
            saasDataGridView.Invalidate();
        }

        private void OnSessionStateChanged(object sender, EventArgs e)
        {
            // Raised on background threads; BeginInvoke - the UI thread may be blocked in an LCS call.
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(UpdateSessionIndicator));
            }
            catch (InvalidOperationException)
            {
                // Window is closing.
            }
        }

        private void UpdateSessionIndicator()
        {
            if (_header == null) return;
            var session = LcsSession.Current;
            var p = Theme.Current;
            string sessionStatus = null;
            switch (session.State)
            {
                case SessionState.Renewing:
                    _header.SetSession("Renewing session…", p.AccentText, "2LCS is renewing your LCS session in the background.");
                    sessionStatus = "Renewing LCS session…";
                    break;
                case SessionState.SignInRequired:
                    _header.SetSession("Sign-in required", p.Caution,
                        "The LCS session could not be renewed automatically (for example because of multi-factor authentication). Click to sign in again.");
                    sessionStatus = "LCS session could not be renewed automatically";
                    break;
                case SessionState.Active when session.HasCookies:
                    var tooltip = session.LastRenewed.HasValue
                        ? $"LCS session renewed at {session.LastRenewed.Value:HH:mm}."
                        : "LCS session restored from the previous run.";
                    if (session.KeepAliveEnabled && session.NextRenewal.HasValue)
                    {
                        tooltip += $" Next automatic renewal around {session.NextRenewal.Value:HH:mm}.";
                    }
                    else if (!session.KeepAliveEnabled)
                    {
                        tooltip += " Automatic renewal is turned off in Settings.";
                    }
                    _header.SetSession("Signed in", p.Success, tooltip + " Click to renew now.");
                    if (session.LastRenewed.HasValue) sessionStatus = $"Session renewed {session.LastRenewed.Value:HH:mm}";
                    break;
                default:
                    _header.SetSession("Not signed in", p.Neutral, "Click to sign in to LCS.");
                    break;
            }

            var parts = new List<string>();
            if (_busyText != null) parts.Add(_busyText);
            else if (_lastRefreshed.HasValue) parts.Add($"Environments updated {_lastRefreshed.Value:HH:mm}");
            if (VmStateEnabled && _lastVmRefresh > DateTime.MinValue) parts.Add($"VM state checked {_lastVmRefresh:HH:mm}");
            if (sessionStatus != null) parts.Add(sessionStatus);
            _statusRightLabel.Text = string.Join("   ·   ", parts);
            cheDataGridView.Invalidate();
            saasDataGridView.Invalidate();
        }

        private async void OnSessionIndicatorClick(object sender, EventArgs e)
        {
            var session = LcsSession.Current;
            if (session.State == SessionState.Renewing) return;
            if (!session.HasCookies || session.State == SessionState.SignedOut)
            {
                if (loginToLcsMenuItem.Enabled) LoginToLCSMenuItem_Click(sender, e);
                return;
            }
            if (await session.RenewAsync()) return;
            if (FluentMessageBox.Show(this, "The LCS session could not be renewed automatically. Do you want to sign in again?", "2LCS",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await ShowSignInAsync();
            }
        }

        private async Task<bool> ShowSignInAsync()
        {
            try
            {
                // Keep the browser process (and its Entra ID session) alive after the sign-in window closes.
                await LcsSession.Current.WarmUpAsync();
            }
            catch (Exception)
            {
                // A missing WebView2 runtime is reported by the sign-in window.
            }
            using var form = new Login();
            form.ShowDialog(this);
            if (form.Cancelled || !LcsSession.Current.HasCookies) return false;
            LcsSession.Current.StartKeepAlive();
            return true;
        }

        private (string Title, string Hint) EmptyState(string kind)
        {
            if (_busyText != null) return ("Loading environments…", "Fetching the latest data from Lifecycle Services.");
            if (!LcsSession.Current.HasCookies) return ("Sign in to get started", "Select Sign in to connect 2LCS to Lifecycle Services.");
            if (_selectedProject == null) return ("Choose a project", "Select the project button at the top of the window to pick an LCS project.");
            var query = _pivot?.Search.Query;
            if (!string.IsNullOrEmpty(query)) return ($"No {kind} match \"{query}\"", "Try a different filter, or press Esc in the filter box to clear it.");
            return ($"No {kind}", "Select Refresh (F5) to load them from LCS.");
        }

        private void OnGridDataChanged(DataGridView grid)
        {
            if (_filtering) return;
            var query = _pivot?.Search.Query;
            if (!string.IsNullOrEmpty(query)) ApplyFilter(grid, query);
            UpdateCounts();
            UpdateSelectionUi();
        }

        private void ApplyFilter()
        {
            var query = _pivot.Search.Query;
            ApplyFilter(cheDataGridView, query);
            ApplyFilter(saasDataGridView, query);
            UpdateCounts();
            UpdateSelectionUi();
        }

        private void ApplyFilter(DataGridView grid, string query)
        {
            if (grid.DataSource == null || _filtering) return;
            _filtering = true;
            try
            {
                // Rows of a bound grid can only be hidden while the binding is suspended.
                var currencyManager = (CurrencyManager)grid.BindingContext[grid.DataSource];
                currencyManager.SuspendBinding();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var visible = string.IsNullOrEmpty(query) || row.Cells.Cast<DataGridViewCell>().Any(cell =>
                        cell.OwningColumn.Visible
                        && cell.FormattedValue?.ToString()?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                    // Actions apply to the selection - it must not contain rows the user cannot see.
                    if (!visible) row.Selected = false;
                    row.Visible = visible;
                }
                currencyManager.ResumeBinding();
            }
            finally
            {
                _filtering = false;
            }
            grid.Invalidate();
        }

        private void UpdateCounts()
        {
            if (_pivot == null) return;
            _pivot.SetBadge(cheTabPage, CountText(cheDataGridView));
            _pivot.SetBadge(saasTabPage, CountText(saasDataGridView));

            static string CountText(DataGridView grid)
            {
                var total = grid.Rows.Count;
                var visible = grid.Rows.GetRowCount(DataGridViewElementStates.Visible);
                return visible == total ? total.ToString() : $"{visible} / {total}";
            }
        }
    }
}
