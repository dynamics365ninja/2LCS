using LCS.JsonObjects;
using LCS.Theming;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Windows.Forms;

namespace LCS.Forms
{
    public partial class MainForm
    {
        // Columns visible by default, in this order.
        private static readonly string[] DefaultColumns =
        {
            "DisplayName", "DeploymentStatus", "VmState", "CurrentApplicationReleaseName", "CurrentPlatformReleaseName",
            "CurrentApplicationBuildVersion", "DeployedOn", "DeployedBy", "TopologyType", "EnvironmentId"
        };

        private ToolStripMenuItem _cmdLogon;
        private ToolStripMenuItem _cmdConnect;
        private ToolStripMenuItem _cmdStart;
        private ToolStripMenuItem _cmdStop;
        private ToolStripMenuItem _cmdMore;
        private ToolStripMenuItem _cmdDetails;
        private ToolStripSeparator _cmdSeparator;
        private bool _menuFromCommandBar;
        private bool _restoringLayout;
        private readonly Dictionary<DataGridView, (string Property, bool Ascending)?> _sort = new Dictionary<DataGridView, (string, bool)?>();

        private bool DetailsVisible => Properties.Settings.Default.detailsPaneVisible;

        private void CreateContextCommands()
        {
            _cmdSeparator = new ToolStripSeparator();
            _cmdLogon = new ToolStripMenuItem("Log on") { ToolTipText = "Open the environment in the browser" };
            _cmdConnect = new ToolStripMenuItem("Connect") { ToolTipText = "Open a remote desktop connection" };
            _cmdStart = new ToolStripMenuItem("Start") { ToolTipText = "Start the selected environments" };
            _cmdStop = new ToolStripMenuItem("Stop") { ToolTipText = "Stop the selected environments" };
            _cmdMore = new ToolStripMenuItem("More actions") { ToolTipText = "All actions for the selected environments" };
            _cmdDetails = new ToolStripMenuItem("Details") { Alignment = ToolStripItemAlignment.Right };

            _cmdLogon.Click += (s, e) => LogonToApplicationToolStripMenuItem_Click(s, e);
            _cmdConnect.Click += (s, e) =>
            {
                if (SelectedDataGridView == cheDataGridView) OpenRDPConnectionToolStripMenuItem_Click(s, e);
                else SaasOpenRdpConnectionToolStripMenuItem_Click(s, e);
            };
            _cmdStart.Click += (s, e) => StartInstanceMenuItem_Click(s, e);
            _cmdStop.Click += (s, e) => StopInstanceMenuItem_Click(s, e);
            _cmdMore.Click += (s, e) =>
            {
                var menu = SelectedDataGridView.ContextMenuStrip;
                if (menu == null) return;
                _menuFromCommandBar = true;
                menu.Show(mainMenuStrip, new Point(_cmdMore.Bounds.Left, _cmdMore.Bounds.Bottom));
            };
            _cmdDetails.Click += (s, e) =>
            {
                Properties.Settings.Default.detailsPaneVisible = !DetailsVisible;
                Properties.Settings.Default.Save();
                ApplyDetailsVisibility();
            };

            // Right-aligned items are laid out from the right edge in collection order.
            var index = mainMenuStrip.Items.IndexOf(exportToolStripMenuItem) + 1;
            mainMenuStrip.Items.Insert(index++, _cmdSeparator);
            foreach (var item in new[] { _cmdLogon, _cmdConnect, _cmdStart, _cmdStop, _cmdMore })
            {
                mainMenuStrip.Items.Insert(index++, item);
            }
            foreach (var item in new ToolStripItem[] { aboutToolStripMenuItem, parametersToolStripMenuItem, cookieToolStripMenuItem, _cmdDetails, logoutToolStripMenuItem })
            {
                mainMenuStrip.Items.Remove(item);
                mainMenuStrip.Items.Add(item);
                item.Alignment = ToolStripItemAlignment.Right;
            }
            cheInstanceContextMenu.Opening += ContextMenuFromCommandBar;
            saasInstanceContextMenu.Opening += ContextMenuFromCommandBar;
        }

        private void ContextMenuFromCommandBar(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // InstanceContextMenu_Opening cancels menus not opened over a row.
            if (!_menuFromCommandBar) return;
            _menuFromCommandBar = false;
            e.Cancel = SelectedInstances().Count == 0;
        }

        private void InitializeGridFeatures()
        {
            foreach (var grid in new[] { cheDataGridView, saasDataGridView })
            {
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.MultiSelect = true;
                grid.SelectionChanged += (s, e) =>
                {
                    // Ctrl+A selects filtered-out rows too.
                    var view = (DataGridView)s;
                    foreach (var hidden in view.SelectedRows.Cast<DataGridViewRow>().Where(r => !r.Visible).ToList())
                    {
                        hidden.Selected = false;
                    }
                    UpdateSelectionUi();
                };
                grid.ColumnHeaderMouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Right) ShowColumnChooser((DataGridView)s);
                };
                grid.ColumnDisplayIndexChanged += (s, e) =>
                {
                    if (!_restoringLayout) SaveGridLayouts();
                };
                _sort[grid] = null;
            }
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                Properties.Settings.Default.lastTab = tabControl.SelectedIndex;
                UpdateSelectionUi();
            };
            toolStripStatusLabel1.Text = "Right-click an environment to see all its actions. Use Ctrl or Shift to select several environments.";
            RestoreGridLayouts();
            RebindSorted(cheDataGridView);
            RebindSorted(saasDataGridView);
            ApplyDetailsVisibility();
        }

        private void UpdateSelectionUi()
        {
            if (_cmdLogon == null) return;
            var instances = _selectedProject == null ? new List<CloudHostedInstance>() : SelectedInstances();
            var any = instances.Count > 0 && LcsSession_HasCookies();
            foreach (var item in new ToolStripItem[] { _cmdSeparator, _cmdLogon, _cmdConnect, _cmdStart, _cmdStop, _cmdMore })
            {
                item.Visible = any;
            }
            if (any)
            {
                var che = SelectedDataGridView == cheDataGridView;
                _cmdLogon.Enabled = instances.Any(i => i.NavigationLinks?.Any(l => l.DisplayName == "Log on to environment") == true);
                _cmdConnect.Visible = che || saasOpenRdpConnectionToolStripMenuItem.Visible;
                _cmdConnect.Enabled = instances.Count == 1 || che;
                _cmdStart.Enabled = instances.Any(i => IsRunning(i) == false);
                _cmdStop.Enabled = instances.Any(i => IsRunning(i) == true);
            }
            if (DetailsVisible)
            {
                _details?.SetEnvironments(instances, instance => _httpClientHelper?.GetEnvironmentDetailsUrl(instance));
            }
        }

        // Azure VM state when known, otherwise the LCS deployment status; null = neither start nor stop.
        private static bool? IsRunning(CloudHostedInstance instance)
        {
            // LCS offers no start/stop (e.g. production) or an operation is in progress.
            if (!instance.CanStart && !instance.CanStop) return null;
            if (instance.VmStateChanging) return null;
            if (instance.VmState == VmState.AzureVmStateService.Running) return true;
            if (instance.VmState == VmState.AzureVmStateService.Stopped) return false;
            var status = (instance.DeploymentStatus ?? string.Empty).Trim().ToLowerInvariant();
            if (status == "deployed") return true;
            if (status.StartsWith("stopped") || status.StartsWith("deallocated")) return false;
            return null;
        }

        private static bool LcsSession_HasCookies() => Auth.LcsSession.Current.HasCookies;

        private void ApplyDetailsVisibility()
        {
            if (_details == null) return;
            _details.Visible = DetailsVisible;
            _detailsSpacer.Visible = DetailsVisible;
            if (_cmdDetails != null)
            {
                _cmdDetails.ToolTipText = DetailsVisible ? "Hide details pane" : "Show details pane";
                _cmdDetails.Image = FluentIcons.Get(DetailsVisible ? "" : "", Theme.Current.Text, IconSize);
            }
            UpdateSelectionUi();
        }

        private void ShowColumnChooser(DataGridView grid)
        {
            var menu = new ContextMenuStrip();
            ThemeManager.StyleToolStrip(menu);
            foreach (var column in grid.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex))
            {
                var item = new ToolStripMenuItem(column.HeaderText)
                {
                    Checked = column.Visible,
                    CheckOnClick = true,
                    // The environment name always stays visible.
                    Enabled = column.DataPropertyName != "DisplayName"
                };
                item.CheckedChanged += (s, e) =>
                {
                    column.Visible = item.Checked;
                    SaveGridLayouts();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            var reset = new ToolStripMenuItem("Reset to default columns");
            reset.Click += (s, e) =>
            {
                ApplyDefaultLayout(grid);
                SaveGridLayouts();
            };
            menu.Items.Add(reset);
            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            ThemeManager.StyleToolStrip(menu);
            menu.Show(Cursor.Position);
        }

        private void SortGrid(DataGridView grid, DataGridViewColumn column)
        {
            var current = _sort[grid];
            var ascending = current?.Property != column.DataPropertyName || !current.Value.Ascending;
            _sort[grid] = (column.DataPropertyName, ascending);
            RebindSorted(grid);
            SaveGridLayouts();
        }

        private void RebindSorted(DataGridView grid)
        {
            var source = grid == cheDataGridView ? _cheInstancesSource : _saasInstancesSource;
            var list = grid == cheDataGridView ? _cheInstancesList : _saasInstancesList;
            if (list == null) return;
            source.DataSource = Sorted(grid, list);
            grid.ClearSelection();
        }

        private List<CloudHostedInstance> Sorted(DataGridView grid, List<CloudHostedInstance> list)
        {
            if (list == null) return null;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
            var sort = _sort.TryGetValue(grid, out var value) ? value : null;
            if (sort == null) return list;
            var sortColumn = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.DataPropertyName == sort.Value.Property);
            if (sortColumn == null) return list;
            sortColumn.HeaderCell.SortGlyphDirection = sort.Value.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            var ordered = list.AsQueryable().OrderBy(sort.Value.Property).ToList();
            if (!sort.Value.Ascending) ordered.Reverse();
            return ordered;
        }

        private void ApplyDefaultLayout(DataGridView grid)
        {
            _restoringLayout = true;
            try
            {
                var index = 0;
                foreach (var property in DefaultColumns)
                {
                    var column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.DataPropertyName == property);
                    if (column == null) continue;
                    column.Visible = true;
                    column.DisplayIndex = index++;
                }
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!DefaultColumns.Contains(column.DataPropertyName)) column.Visible = false;
                }
            }
            finally
            {
                _restoringLayout = false;
            }
        }

        private sealed class GridLayout
        {
            public List<ColumnLayout> Columns { get; set; } = new List<ColumnLayout>();
            public string SortProperty { get; set; }
            public bool SortAscending { get; set; } = true;
        }

        private sealed class ColumnLayout
        {
            public string Property { get; set; }
            public bool Visible { get; set; }
            public int DisplayIndex { get; set; }
        }

        private void SaveGridLayouts()
        {
            if (_restoringLayout) return;
            var layouts = new Dictionary<string, GridLayout>
            {
                ["che"] = LayoutOf(cheDataGridView),
                ["saas"] = LayoutOf(saasDataGridView)
            };
            Properties.Settings.Default.gridLayout = JsonConvert.SerializeObject(layouts);
            Properties.Settings.Default.Save();

            GridLayout LayoutOf(DataGridView grid) => new GridLayout
            {
                Columns = grid.Columns.Cast<DataGridViewColumn>()
                    .Select(c => new ColumnLayout { Property = c.DataPropertyName, Visible = c.Visible, DisplayIndex = c.DisplayIndex })
                    .ToList(),
                SortProperty = _sort[grid]?.Property,
                SortAscending = _sort[grid]?.Ascending ?? true
            };
        }

        private void RestoreGridLayouts()
        {
            Dictionary<string, GridLayout> layouts = null;
            try
            {
                layouts = JsonConvert.DeserializeObject<Dictionary<string, GridLayout>>(Properties.Settings.Default.gridLayout ?? "");
            }
            catch (JsonException)
            {
                // Corrupted layout - use the defaults.
            }
            Restore(cheDataGridView, layouts?.GetValueOrDefault("che"));
            Restore(saasDataGridView, layouts?.GetValueOrDefault("saas"));

            void Restore(DataGridView grid, GridLayout layout)
            {
                if (layout == null || layout.Columns.Count == 0)
                {
                    ApplyDefaultLayout(grid);
                    return;
                }
                _restoringLayout = true;
                try
                {
                    foreach (var columnLayout in layout.Columns.OrderBy(c => c.DisplayIndex))
                    {
                        var column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.DataPropertyName == columnLayout.Property);
                        if (column == null) continue;
                        column.Visible = columnLayout.Visible || column.DataPropertyName == "DisplayName";
                        column.DisplayIndex = Math.Min(columnLayout.DisplayIndex, grid.Columns.Count - 1);
                    }
                }
                finally
                {
                    _restoringLayout = false;
                }
                _sort[grid] = string.IsNullOrEmpty(layout.SortProperty) ? null : (layout.SortProperty, layout.SortAscending);
            }
        }

        private void RestoreWindowState()
        {
            var parts = (Properties.Settings.Default.windowPlacement ?? "").Split(',');
            if (parts.Length == 5 && parts.Take(4).All(v => int.TryParse(v, out _)))
            {
                var bounds = new Rectangle(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                // Only if the title bar is on one of the current screens (monitors can be removed).
                var titleBar = new Rectangle(bounds.X, bounds.Y, bounds.Width, SystemInformation.CaptionHeight);
                if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height
                    && Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.WorkingArea, titleBar).Width > 200))
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = bounds;
                    if (parts[4] == nameof(FormWindowState.Maximized)) WindowState = FormWindowState.Maximized;
                }
            }

            if (Properties.Settings.Default.lastTab >= 0 && Properties.Settings.Default.lastTab < tabControl.TabCount)
            {
                tabControl.SelectedIndex = Properties.Settings.Default.lastTab;
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.lastFilter))
            {
                _pivot.Search.SetQuery(Properties.Settings.Default.lastFilter);
            }

            // Save also after moving/resizing, so the placement survives a killed process.
            var lastState = WindowState;
            Shown += (s, e) =>
            {
                ResizeEnd += (sender, args) => SaveWindowState();
                SizeChanged += (sender, args) =>
                {
                    if (WindowState == lastState) return;
                    lastState = WindowState;
                    if (WindowState != FormWindowState.Minimized) SaveWindowState();
                };
            };
        }

        private void SaveWindowState()
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var state = WindowState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            // A window minimized to the tray can report an empty restore size.
            if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height)
            {
                Properties.Settings.Default.windowPlacement = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height},{state}";
            }
            Properties.Settings.Default.lastTab = tabControl.SelectedIndex;
            Properties.Settings.Default.lastFilter = _pivot?.Search.Query ?? "";
            Properties.Settings.Default.Save();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (_cmdLogon != null) OnThemeChanged(this, EventArgs.Empty);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // Icons are bitmaps in theme colors - redraw them.
            StyleCommandBar();
            foreach (var menu in new[] { cheInstanceContextMenu, saasInstanceContextMenu, notifyIconContextMenu })
            {
                ThemeManager.StyleToolStrip(menu);
                ApplyMenuIcons(menu.Items, force: true);
            }
            ApplyDetailsVisibility();
            UpdateSessionIndicator();
            Invalidate(true);
        }
    }
}
