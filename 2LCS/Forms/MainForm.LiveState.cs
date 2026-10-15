using LCS.JsonObjects;
using LCS.Theming;
using LCS.VmState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.Forms
{
    // VM state: every N minutes (Settings), every minute while a VM is changing. After an action from 2LCS:
    // follow-ups at 30 s, 2, 5 and 10 min until settled. LCS is not polled otherwise.
    public partial class MainForm
    {
        // A starting/stopping VM is checked more often until it settles.
        private static readonly TimeSpan ChangingVmRefreshInterval = TimeSpan.FromMinutes(1);

        private static readonly TimeSpan[] FollowUpDelays =
        {
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)
        };

        private readonly Timer _liveTimer = new Timer { Interval = 10000 };
        private readonly List<FollowUp> _followUps = new List<FollowUp>();
        private DataGridViewTextBoxColumn _vmStateColumn;
        private DateTime _lastVmRefresh = DateTime.MinValue;
        private Task _vmRefreshTask;
        private bool _vmRefreshAgain;
        private bool _vmRefreshInteractive;
        private bool _vmSignInOffered;

        private static bool VmStateEnabled => Properties.Settings.Default.azureVmState;

        private static int VmRefreshMinutes => Math.Max(0, Properties.Settings.Default.vmStateRefreshMinutes);

        private sealed class FollowUp
        {
            public Queue<DateTime> Due { get; init; }

            public Dictionary<string, string> StatusBefore { get; init; }
        }

        private void CreateVmStateColumn()
        {
            _vmStateColumn = new DataGridViewTextBoxColumn
            {
                Name = "cheVmState",
                DataPropertyName = nameof(CloudHostedInstance.VmState),
                HeaderText = "VM State (Azure)",
                ReadOnly = true,
                MinimumWidth = 8
            };
            cheDataGridView.Columns.Insert(cheDeploymentStatus.Index + 1, _vmStateColumn);
            cheDataGridView.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != _vmStateColumn.Index) return;
                if (cheDataGridView.Rows[e.RowIndex].DataBoundItem is CloudHostedInstance instance && !string.IsNullOrEmpty(instance.VmStateDetail))
                {
                    e.ToolTipText = instance.VmStateDetail;
                }
            };
        }

        private void InitializeLiveState()
        {
            VmState.AzureVmStateService.Broker.OwnerWindow = Handle;
            ApplyVmStateSetting();
            _liveTimer.Tick += async (s, e) => await OnLiveTimerAsync();
            _liveTimer.Start();
            FormClosed += (s, e) => _liveTimer.Dispose();
        }

        private void ApplyVmStateSetting()
        {
            if (_vmStateColumn == null) return;
            if (!VmStateEnabled)
            {
                _vmStateColumn.Visible = false;
                foreach (var instance in _cheInstancesList ?? new List<CloudHostedInstance>())
                {
                    instance.VmState = null;
                    instance.VmStateDetail = null;
                    instance.VmStateMismatch = false;
                    instance.VmStateChanging = false;
                }
                cheDataGridView.Invalidate();
            }
            UpdateSelectionUi();
        }

        private void ExpectStateChange(IEnumerable<CloudHostedInstance> instances)
        {
            var now = DateTime.Now;
            _followUps.Add(new FollowUp
            {
                Due = new Queue<DateTime>(FollowUpDelays.Select(delay => now + delay)),
                StatusBefore = instances.Where(i => i.EnvironmentId != null).GroupBy(i => i.EnvironmentId)
                    .ToDictionary(g => g.Key, g => g.First().DeploymentStatus)
            });
        }

        private async Task OnLiveTimerAsync()
        {
            if (IsDisposed || WindowState == FormWindowState.Minimized || _selectedProject == null || _httpClientHelper == null
                || !Auth.LcsSession.Current.HasCookies)
            {
                return;
            }
            var now = DateTime.Now;

            // Follow-up after an action (the VM state is read right after).
            var due = _followUps.Where(f => f.Due.Count > 0 && f.Due.Peek() <= now).ToList();
            if (due.Count > 0)
            {
                if (_busyOperations.Count > 0) return;
                foreach (var followUp in due)
                {
                    followUp.Due.Dequeue();
                }
                try
                {
                    await RefreshEnvironmentsAsync(reloadFromLcs: true, quiet: true);
                    if (_vmRefreshTask != null) await _vmRefreshTask;
                }
                catch (Exception)
                {
                    // Background refresh: no pop-ups, the session indicator shows sign-in problems.
                }
                _followUps.RemoveAll(f => f.Due.Count == 0 || Settled(f));
                return;
            }

            // Periodic VM state check (Azure only, one request per VM).
            if (!VmStateEnabled || VmRefreshMinutes == 0) return;
            var changing = (_cheInstancesList ?? new List<CloudHostedInstance>()).Any(i => i.VmStateChanging);
            var interval = changing ? ChangingVmRefreshInterval : TimeSpan.FromMinutes(VmRefreshMinutes);
            if (now - _lastVmRefresh >= interval)
            {
                await RefreshVmStatesAsync(allowInteraction: false);
            }
        }

        private bool Settled(FollowUp followUp)
        {
            var all = (_cheInstancesList ?? new List<CloudHostedInstance>()).Concat(_saasInstancesList ?? new List<CloudHostedInstance>()).ToList();
            foreach (var (environmentId, statusBefore) in followUp.StatusBefore)
            {
                var instance = all.FirstOrDefault(i => i.EnvironmentId == environmentId);
                // Gone from a non-empty list (deleted); an empty list may just be an LCS hiccup.
                if (instance == null)
                {
                    if (all.Count == 0) return false;
                    continue;
                }
                if (IsTransitional(instance.DeploymentStatus) || instance.VmStateChanging) return false;
                if (string.Equals(instance.DeploymentStatus, statusBefore, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static bool IsTransitional(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            var value = status.ToLowerInvariant();
            return value != "running" && value.Split(' ').Any(word => word.EndsWith("ing", StringComparison.Ordinal));
        }

        // Single refresh at a time; a request during a running one (e.g. for a reloaded list) runs right after it.
        private Task RefreshVmStatesAsync(bool allowInteraction)
        {
            if (!VmStateEnabled || _cheInstancesList == null || _cheInstancesList.Count == 0) return Task.CompletedTask;
            _vmRefreshInteractive |= allowInteraction;
            if (_vmRefreshTask != null && !_vmRefreshTask.IsCompleted)
            {
                _vmRefreshAgain = true;
                return _vmRefreshTask;
            }
            _vmRefreshTask = RunVmRefreshesAsync();
            return _vmRefreshTask;
        }

        private async Task RunVmRefreshesAsync()
        {
            do
            {
                _vmRefreshAgain = false;
                var interactive = _vmRefreshInteractive;
                _vmRefreshInteractive = false;
                await RefreshVmStatesOnceAsync(interactive);
            }
            while (_vmRefreshAgain && !IsDisposed);
        }

        private async Task RefreshVmStatesOnceAsync(bool allowInteraction)
        {
            if (!VmStateEnabled || _cheInstancesList == null || _cheInstancesList.Count == 0) return;
            _lastVmRefresh = DateTime.Now;
            try
            {
                var instances = _cheInstancesList;
                VmStateRefreshResult result;
                using (allowInteraction ? Busy("Reading the state of virtual machines from Azure…") : null)
                {
                    result = await Task.Run(() => VmState.AzureVmStateService.Current.RefreshAsync(instances, allowInteraction));
                }
                if (IsDisposed) return;
                cheDataGridView.Invalidate();
                _details?.Invalidate();
                UpdateSelectionUi();
                UpdateSessionIndicator();

                if (result.SignInRequired && !allowInteraction && !_vmSignInOffered)
                {
                    _vmSignInOffered = true;
                    _infoBar?.Show(InfoSeverity.Informational, "See the real state of the virtual machines",
                        "LCS does not know when a VM is shut down outside LCS. Sign in to Azure to read the real VM state.",
                        autoClose: null, actionText: "Sign in to Azure", action: async () =>
                        {
                            _infoBar.Dismiss();
                            await RefreshVmStatesAsync(allowInteraction: true);
                        });
                }
                else if (allowInteraction && result.SignInRequired)
                {
                    Notify(InfoSeverity.Warning, "Azure sign-in did not complete", "The VM state is shown only for subscriptions you are signed in to.");
                }
                else if (allowInteraction)
                {
                    Notify(InfoSeverity.Success, "Signed in to Azure", "2LCS now shows the real state of the virtual machines and keeps it up to date.");
                }

                var mismatches = instances.Where(i => i.VmStateMismatch).Select(i => i.DisplayName).ToList();
                if (mismatches.Count > 0 && !allowInteraction && _infoBar != null && !_infoBar.Visible)
                {
                    _infoBar.Show(InfoSeverity.Warning, "LCS shows an outdated state",
                        $"{string.Join(", ", mismatches)}: the virtual machine state in Azure differs from LCS. See the VM State (Azure) column.",
                        autoClose: TimeSpan.FromSeconds(20));
                }
            }
            catch (Exception)
            {
                // Optional information - never disturb the user.
            }
        }

        // Reloaded environments are new objects - keep the last known VM state until it is read again.
        private static void CarryOverVmState(List<CloudHostedInstance> previous, List<CloudHostedInstance> current)
        {
            if (previous == null || current == null) return;
            var byId = previous.Where(i => i.EnvironmentId != null).GroupBy(i => i.EnvironmentId).ToDictionary(g => g.Key, g => g.First());
            foreach (var instance in current)
            {
                if (instance.EnvironmentId == null || !byId.TryGetValue(instance.EnvironmentId, out var old)) continue;
                instance.VmState = old.VmState;
                instance.VmStateDetail = old.VmStateDetail;
                instance.VmStateMismatch = old.VmStateMismatch;
                instance.VmStateChanging = old.VmStateChanging;
            }
        }

        private Action RememberSelection(DataGridView grid)
        {
            var saved = new[] { grid }.Select(grid => (
                Grid: grid,
                Selected: SelectedInstances(grid).Select(i => i.EnvironmentId).ToHashSet(),
                FirstRow: grid.FirstDisplayedScrollingRowIndex,
                FirstColumn: grid.FirstDisplayedScrollingColumnIndex)).ToList();
            return () =>
            {
                foreach (var (grid, selected, firstRow, firstColumn) in saved)
                {
                    if (selected.Count == 0) continue;
                    grid.ClearSelection();
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        if (row.Visible && row.DataBoundItem is CloudHostedInstance instance && selected.Contains(instance.EnvironmentId))
                        {
                            row.Selected = true;
                        }
                    }
                    try
                    {
                        if (firstRow >= 0 && firstRow < grid.RowCount && grid.Rows[firstRow].Visible) grid.FirstDisplayedScrollingRowIndex = firstRow;
                        if (firstColumn >= 0 && firstColumn < grid.ColumnCount && grid.Columns[firstColumn].Visible) grid.FirstDisplayedScrollingColumnIndex = firstColumn;
                    }
                    catch (InvalidOperationException)
                    {
                        // Grid not laid out yet.
                    }
                }
                UpdateSelectionUi();
            };
        }
    }
}
