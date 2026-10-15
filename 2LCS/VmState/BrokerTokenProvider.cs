using Azure.Core;
using Azure.Identity;
using Azure.Identity.Broker;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LCS.VmState
{
    // ARM tokens via the Windows account broker; interactive sign-in only on request.
    internal sealed class BrokerTokenProvider : IArmTokenProvider
    {
        private const string ArmScope = "https://management.azure.com/.default";

        private readonly SemaphoreSlim _sync = new SemaphoreSlim(1, 1);
        private InteractiveBrowserCredential _credential;
        private AuthenticationRecord _record;
        private bool _recordLoaded;

        public IntPtr OwnerWindow { get; set; }

        private static string RecordPath => Path.Combine(Security.LocalProtection.DataFolder, "azure-account.json");

        public async Task<string> GetTokenAsync(string tenantId, bool allowInteraction, CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var credential = await GetCredentialAsync(cancellationToken).ConfigureAwait(false);
                var context = new TokenRequestContext(new[] { ArmScope }, tenantId: tenantId);
                try
                {
                    return (await credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false)).Token;
                }
                catch (AuthenticationRequiredException)
                {
                    if (!allowInteraction) return null;
                }
                catch (AuthenticationFailedException)
                {
                    if (!allowInteraction) return null;
                }

                // The user asked to sign in: show the Windows sign-in dialog.
                _record = await credential.AuthenticateAsync(context, cancellationToken).ConfigureAwait(false);
                await SaveRecordAsync(cancellationToken).ConfigureAwait(false);
                _credential = null;
                credential = await GetCredentialAsync(cancellationToken).ConfigureAwait(false);
                return (await credential.GetTokenAsync(context, cancellationToken).ConfigureAwait(false)).Token;
            }
            catch (AuthenticationFailedException) when (allowInteraction)
            {
                // Cancelled or failed interactive sign-in.
                return null;
            }
            finally
            {
                _sync.Release();
            }
        }

        public void SignOut()
        {
            _credential = null;
            _record = null;
            _recordLoaded = true;
            try
            {
                if (File.Exists(RecordPath)) File.Delete(RecordPath);
                // Persisted MSAL token cache (TokenCachePersistenceOptions "2LCS").
                var cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".IdentityService");
                if (Directory.Exists(cacheFolder))
                {
                    foreach (var file in Directory.GetFiles(cacheFolder, "2LCS*"))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (IOException)
            {
                // Best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort.
            }
        }

        private async Task<InteractiveBrowserCredential> GetCredentialAsync(CancellationToken cancellationToken)
        {
            if (_credential != null) return _credential;
            if (!_recordLoaded)
            {
                _recordLoaded = true;
                try
                {
                    if (File.Exists(RecordPath))
                    {
                        using var stream = File.OpenRead(RecordPath);
                        _record = await AuthenticationRecord.DeserializeAsync(stream, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    _record = null;
                }
            }

            var options = new InteractiveBrowserCredentialBrokerOptions(OwnerWindow)
            {
                // Silent sign-in with the Windows account.
                UseDefaultBrokerAccount = true,
                // Never prompt on its own - only when the user asks.
                DisableAutomaticAuthentication = true,
                AuthenticationRecord = _record,
                // DPAPI-encrypted token cache - no sign-in after a restart.
                TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "2LCS" }
            };
            // Customer subscriptions can be in other tenants.
            options.AdditionallyAllowedTenants.Add("*");
            _credential = new InteractiveBrowserCredential(options);
            return _credential;
        }

        private async Task SaveRecordAsync(CancellationToken cancellationToken)
        {
            if (_record == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(RecordPath));
            using var stream = File.Create(RecordPath);
            await _record.SerializeAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }
}
