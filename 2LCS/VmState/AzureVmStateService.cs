using LCS.JsonObjects;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LCS.VmState
{
    internal interface IArmTokenProvider
    {
        Task<string> GetTokenAsync(string tenantId, bool allowInteraction, CancellationToken cancellationToken);
    }

    internal sealed class VmStateRefreshResult
    {
        public bool SignInRequired { get; set; }

        public int Updated { get; set; }
    }

    // Real VM power state from Azure - LCS only knows about actions done through LCS.
    internal sealed class AzureVmStateService
    {
        private const string ComputeApiVersion = "2024-03-01";
        private static readonly Regex AuthorizationUri = new Regex("authorization_uri=\"https://[^/\"]+/([^/\"]+)", RegexOptions.IgnoreCase);

        private readonly IArmTokenProvider _tokens;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ConcurrentDictionary<string, string> _tenantBySubscription = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal AzureVmStateService(IArmTokenProvider tokens)
        {
            _tokens = tokens;
        }

        public static AzureVmStateService Current { get; } = new AzureVmStateService(new BrokerTokenProvider());

        internal string ArmUrl { get; set; } = "https://management.azure.com";

        public static BrokerTokenProvider Broker => (BrokerTokenProvider)Current._tokens;

        public async Task<VmStateRefreshResult> RefreshAsync(IEnumerable<CloudHostedInstance> instances, bool allowInteraction, CancellationToken cancellationToken = default)
        {
            var result = new VmStateRefreshResult();
            var candidates = (instances ?? Enumerable.Empty<CloudHostedInstance>())
                .Where(i => !string.IsNullOrEmpty(i.AzureSubscriptionId) && !string.IsNullOrEmpty(i.ResourceGroupName)
                    && i.Instances != null && i.Instances.Any(vm => !string.IsNullOrEmpty(vm.MachineName)))
                .ToList();
            if (candidates.Count == 0) return result;

            using var throttle = new SemaphoreSlim(6);
            foreach (var subscription in candidates.GroupBy(i => i.AzureSubscriptionId, StringComparer.OrdinalIgnoreCase))
            {
                string token = null;
                try
                {
                    var tenant = await GetTenantAsync(subscription.Key, cancellationToken).ConfigureAwait(false);
                    token = await _tokens.GetTokenAsync(tenant, allowInteraction, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    foreach (var instance in subscription)
                    {
                        Set(instance, null, $"The VM state could not be read from Azure: {ex.Message}");
                    }
                    continue;
                }
                if (token == null)
                {
                    result.SignInRequired = true;
                    foreach (var instance in subscription)
                    {
                        Set(instance, null, "Sign in to Azure to see the real state of the virtual machine.");
                    }
                    continue;
                }

                await Task.WhenAll(subscription.Select(async instance =>
                {
                    await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await RefreshInstanceAsync(instance, token, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                })).ConfigureAwait(false);
                result.Updated += subscription.Count();
            }
            return result;
        }

        private async Task RefreshInstanceAsync(CloudHostedInstance instance, string token, CancellationToken cancellationToken)
        {
            var states = new List<(string Machine, string PowerState, string Error)>();
            foreach (var vm in instance.Instances.Where(vm => !string.IsNullOrEmpty(vm.MachineName)))
            {
                var url = $"{ArmUrl}/subscriptions/{Uri.EscapeDataString(instance.AzureSubscriptionId)}/resourceGroups/{Uri.EscapeDataString(instance.ResourceGroupName)}" +
                          $"/providers/Microsoft.Compute/virtualMachines/{Uri.EscapeDataString(vm.MachineName)}/instanceView?api-version={ComputeApiVersion}";
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                            var code = json["statuses"]?.Select(s => (string)s["code"]).FirstOrDefault(c => c != null && c.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase));
                            var powerState = code?.Substring("PowerState/".Length).ToLowerInvariant();
                            // No or unknown power state (e.g. during provisioning) is not reported as Stopped.
                            if (powerState == null || Precise(powerState) == UnknownState)
                                states.Add((vm.MachineName, null, $"Azure does not report a power state for the VM {vm.MachineName}{(powerState != null ? $" ({powerState})" : string.Empty)}."));
                            else
                                states.Add((vm.MachineName, powerState, null));
                            break;
                        case HttpStatusCode.Forbidden:
                        case HttpStatusCode.Unauthorized:
                            states.Add((vm.MachineName, null, $"Your Azure account cannot read the VM {vm.MachineName} (the Reader role on resource group {instance.ResourceGroupName} is needed)."));
                            break;
                        case HttpStatusCode.NotFound:
                            states.Add((vm.MachineName, "notfound", $"The VM {vm.MachineName} does not exist in resource group {instance.ResourceGroupName} - it may have been deleted outside LCS."));
                            break;
                        default:
                            states.Add((vm.MachineName, null, $"Azure returned {(int)response.StatusCode} {response.ReasonPhrase} for the VM {vm.MachineName}."));
                            break;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    states.Add((vm.MachineName, null, $"Azure could not be reached: {ex.Message}"));
                }
            }
            Apply(instance, states);
        }

        internal static void Apply(CloudHostedInstance instance, IReadOnlyList<(string Machine, string PowerState, string Error)> states)
        {
            if (states.Count == 0)
            {
                Set(instance, null, null);
                return;
            }
            var known = states.Where(s => s.PowerState != null).ToList();
            if (known.Count == 0)
            {
                // No state (no access, network error): the reason goes to the tooltip.
                Set(instance, null, string.Join(Environment.NewLine, states.Select(s => s.Error)));
                return;
            }

            // Starting counts as running; stopping, deallocated and missing VMs as stopped.
            var running = known.Any(s => s.PowerState == "running" || s.PowerState == "starting");
            var text = running ? Running : Stopped;
            var changing = known.Any(s => s.PowerState == "starting" || s.PowerState == "stopping" || s.PowerState == "deallocating");
            var detail = string.Join(Environment.NewLine, states.Select(s => s.PowerState != null ? $"{s.Machine}: {Precise(s.PowerState)}" : s.Error));

            var lcs = (instance.DeploymentStatus ?? string.Empty).Trim().ToLowerInvariant();
            var lcsSaysRunning = lcs == "deployed";
            var lcsSaysOff = lcs.StartsWith("stopped") || lcs.StartsWith("deallocated");

            var mismatch = false;
            if (lcsSaysRunning && !running)
            {
                mismatch = true;
                var reason = known.All(s => s.PowerState == "notfound")
                    ? "LCS shows Deployed, but the virtual machine does not exist in Azure - it was probably deleted outside LCS."
                    : "LCS shows Deployed, but the virtual machine is stopped in Azure. It was shut down outside LCS (from inside Windows, in the Azure portal or by an auto-shutdown policy), so LCS does not know about it."
                      + (known.Any(s => s.PowerState == "stopped") ? " The VM is not deallocated, so it is still billed." : string.Empty);
                detail = reason + Environment.NewLine + Environment.NewLine + detail;
            }
            else if (lcsSaysOff && running)
            {
                mismatch = true;
                detail = $"LCS shows {instance.DeploymentStatus}, but the virtual machine is running in Azure - it was started outside LCS and is billed."
                    + Environment.NewLine + Environment.NewLine + detail;
            }
            Set(instance, text, detail, mismatch, changing);
        }

        internal const string Running = "Running";
        internal const string Stopped = "Stopped";

        internal static string Precise(string powerState) => powerState switch
        {
            "running" => "running",
            "starting" => "starting",
            "stopping" => "stopping",
            "stopped" => "stopped, not deallocated (still billed)",
            "deallocating" => "deallocating",
            "deallocated" => "stopped and deallocated",
            "notfound" => "does not exist in Azure",
            _ => UnknownState
        };

        private const string UnknownState = "unknown state";

        private static void Set(CloudHostedInstance instance, string state, string detail, bool mismatch = false, bool changing = false)
        {
            instance.VmState = state;
            instance.VmStateDetail = detail;
            instance.VmStateMismatch = mismatch;
            instance.VmStateChanging = changing;
        }

        private async Task<string> GetTenantAsync(string subscriptionId, CancellationToken cancellationToken)
        {
            if (_tenantBySubscription.TryGetValue(subscriptionId, out var known)) return known;
            using var response = await _http.GetAsync($"{ArmUrl}/subscriptions/{Uri.EscapeDataString(subscriptionId)}?api-version=2022-12-01", cancellationToken).ConfigureAwait(false);
            var match = AuthorizationUri.Match(response.Headers.WwwAuthenticate.ToString());
            if (!match.Success) return "organizations";
            // Only a discovered tenant is cached - a transient failure is retried next time.
            _tenantBySubscription[subscriptionId] = match.Groups[1].Value;
            return match.Groups[1].Value;
        }
    }
}
