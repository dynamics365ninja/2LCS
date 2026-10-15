using LCS.JsonObjects;
using LCS.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.Forms
{
    public partial class MainForm
    {
        private readonly List<string> _busyOperations = new List<string>();
        // HttpClientHelper changes shared request headers in some calls - one blocking call at a time.
        private readonly SemaphoreSlim _lcsGate = new SemaphoreSlim(1, 1);
        private InfoBar _infoBar;

        // Blocking LCS calls run off the UI thread.
        private async Task<T> RunLcsAsync<T>(string status, Func<T> request)
        {
            using (Busy(status))
            {
                await _lcsGate.WaitAsync();
                try
                {
                    return await Task.Run(request);
                }
                finally
                {
                    _lcsGate.Release();
                }
            }
        }

        private Task RunLcsAsync(string status, Action request) => RunLcsAsync<object>(status, () =>
        {
            request();
            return null;
        });

        private IDisposable Busy(string status)
        {
            _busyOperations.Add(status);
            OnBusyChanged();
            return new BusyScope(() =>
            {
                _busyOperations.Remove(status);
                OnBusyChanged();
            });
        }

        private void OnBusyChanged()
        {
            if (IsDisposed) return;
            _busyText = _busyOperations.LastOrDefault();
            if (_progress != null) _progress.Running = _busyOperations.Count > 0;
            Cursor = _busyOperations.Count > 0 ? Cursors.AppStarting : Cursors.Default;
            UpdateSessionIndicator();
        }

        private void Notify(InfoSeverity severity, string title, string message = null, string actionText = null, Action action = null)
        {
            if (IsDisposed) return;
            var autoClose = severity == InfoSeverity.Success || severity == InfoSeverity.Informational
                ? TimeSpan.FromSeconds(12)
                : (TimeSpan?)null;
            _infoBar?.Show(severity, title, message, autoClose, actionText, action);

            if (!ContainsFocus || WindowState == FormWindowState.Minimized || !Visible)
            {
                var icon = severity switch
                {
                    InfoSeverity.Error => ToolTipIcon.Error,
                    InfoSeverity.Warning => ToolTipIcon.Warning,
                    _ => ToolTipIcon.Info
                };
                notifyIcon.ShowBalloonTip(5000, title, string.IsNullOrEmpty(message) ? title : message, icon);
            }
        }

        private void NotifySaved(string path)
        {
            Notify(InfoSeverity.Success, "Export saved", System.IO.Path.GetFileName(path), "Open", () => WebBrowserHelper.OpenUri(path));
        }

        private HttpClientHelper NewLcsClient()
        {
            return new HttpClientHelper(_cookies)
            {
                LcsUrl = _lcsUrl,
                LcsUpdateUrl = _lcsUpdateUrl,
                LcsDiagUrl = _lcsDiagUrl,
                LcsProjectId = _selectedProject.Id.ToString(),
                LcsProjectTypeId = _selectedProject.ProjectTypeId
            };
        }

        private List<CloudHostedInstance> SelectedInstances(DataGridView grid = null)
        {
            grid ??= SelectedDataGridView;
            return grid.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Visible)
                .OrderBy(r => r.Index)
                .Select(r => r.DataBoundItem as CloudHostedInstance)
                .Where(i => i != null)
                .ToList();
        }

        private async Task RunForInstancesAsync(List<CloudHostedInstance> instances, string verb, Func<HttpClientHelper, CloudHostedInstance, Task<bool>> action,
            bool changesState = true)
        {
            if (instances.Count == 0) return;
            var names = string.Join(", ", instances.Select(i => i.DisplayName));
            bool[] results;
            using (Busy($"{verb} {names}…"))
            {
                results = await Task.WhenAll(instances.Select(instance => Task.Run(async () =>
                {
                    try
                    {
                        return await action(NewLcsClient(), instance);
                    }
                    catch (Exception ex) when (Auth.LcsSessionExpiredException.IsCauseOf(ex))
                    {
                        // Usually wrapped in an AggregateException (.Result inside HttpClientHelper).
                        throw;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                })));
            }

            var failed = instances.Where((_, index) => !results[index]).Select(i => i.DisplayName).ToList();
            var accepted = instances.Where((_, index) => results[index]).ToList();
            if (changesState && accepted.Count > 0) ExpectStateChange(accepted);
            if (failed.Count == 0)
            {
                if (changesState)
                {
                    Notify(InfoSeverity.Success, $"{verb} requested", $"LCS accepted the request for {names}. The state is refreshed automatically.",
                        "Refresh now", () => RefreshMenuItem_Click(this, EventArgs.Empty));
                }
                else
                {
                    Notify(InfoSeverity.Success, $"{verb} done", $"LCS accepted the request for {names}.");
                }
            }
            else
            {
                var acceptedText = accepted.Count > 0 ? $" Accepted for {string.Join(", ", accepted.Select(i => i.DisplayName))}." : string.Empty;
                Notify(InfoSeverity.Error, $"{verb} failed", $"LCS did not accept the request for {string.Join(", ", failed)}.{acceptedText}");
            }
        }

        // Keyed by environment id - the list can be reloaded (new objects) while this runs.
        private Task<Dictionary<string, List<RDPConnectionDetails>>> FetchRdpDetailsAsync(IEnumerable<CloudHostedInstance> instances)
        {
            var list = instances.Where(i => i.EnvironmentId != null).GroupBy(i => i.EnvironmentId).Select(g => g.First()).ToList();
            return RunLcsAsync("Getting RDP connection details…",
                () => list.ToDictionary(instance => instance.EnvironmentId, instance => _httpClientHelper.GetRdpConnectionDetails(instance) ?? new List<RDPConnectionDetails>()));
        }

        private static List<RDPConnectionDetails> RdpFor(Dictionary<string, List<RDPConnectionDetails>> details, CloudHostedInstance instance) =>
            instance.EnvironmentId != null && details.TryGetValue(instance.EnvironmentId, out var list) ? list : new List<RDPConnectionDetails>();

        private static Dictionary<string, string> CredentialsFor(Dictionary<(string EnvironmentId, string ItemName), Dictionary<string, string>> credentials, string environmentId, string itemName) =>
            credentials.TryGetValue((environmentId, itemName), out var values) ? values : new Dictionary<string, string>();

        private Task<Dictionary<(string EnvironmentId, string ItemName), Dictionary<string, string>>> FetchCredentialsAsync(IEnumerable<(string EnvironmentId, string ItemName)> items)
        {
            var list = items.Distinct().ToList();
            return RunLcsAsync("Getting credentials…",
                () => list.ToDictionary(item => item, item => _httpClientHelper.GetCredentials(item.EnvironmentId, item.ItemName) ?? new Dictionary<string, string>()));
        }

        private async Task OpenRdpAsync(CloudHostedInstance instance)
        {
            var rdpList = await RunLcsAsync($"Getting RDP connection details for {instance.DisplayName}…",
                () => _httpClientHelper.GetRdpConnectionDetails(instance));
            if (rdpList == null || rdpList.Count == 0)
            {
                Notify(InfoSeverity.Warning, "RDP is not available",
                    $"Cannot retrieve RDP connection details for {instance.DisplayName}. The environment is not accessible through RDP or you do not have the Environment Manager role.");
                return;
            }
            var rdpEntry = rdpList.Count > 1 ? ChooseRdpLogonUser(rdpList, this) : rdpList.First();
            if (rdpEntry != null) StartRdp(rdpEntry);
        }

        private static void StartRdp(RDPConnectionDetails rdpEntry)
        {
            RdpCredentials.Connect(rdpEntry);
        }

        private sealed class BusyScope : IDisposable
        {
            private Action _onDispose;

            public BusyScope(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                _onDispose?.Invoke();
                _onDispose = null;
            }
        }
    }
}
