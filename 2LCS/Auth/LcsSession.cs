using LCS.Security;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LCS.Auth
{
    internal enum SessionState
    {
        SignedOut,
        Active,
        Renewing,
        SignInRequired
    }

    // Owns the LCS cookies. The LCS session expires after about an hour; it is renewed silently
    // (SilentBrowser) before it expires, after resume and when a request fails.
    internal sealed class LcsSession
    {
        public static readonly TimeSpan RenewalInterval = TimeSpan.FromMinutes(40);
        private static readonly TimeSpan SignInTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)
        };

        // Cookies owned by 2LCS itself, which must survive a renewal.
        private const string ProjectCookie = "lcspid";
        private const string AntiForgeryCookiePrefix = "__RequestVerificationToken";

        private readonly object _sync = new object();
        private readonly SilentBrowser _browser = new SilentBrowser();
        private Task<bool> _renewal;
        private Timer _keepAliveTimer;
        private int _failedAttempts;
        private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;
        private int _version;
        private int _interactiveSignIns;
        private bool _signedOut;

        private LcsSession()
        {
        }

        public static LcsSession Current { get; } = new LcsSession();

        public CookieContainer Cookies { get; } = new CookieContainer();

        public SessionState State { get; private set; } = SessionState.SignedOut;

        public DateTimeOffset? LastRenewed { get; private set; }

        public DateTimeOffset? NextRenewal => LastRenewed?.Add(RenewalInterval);

        public int Version => Volatile.Read(ref _version);

        // Guards changes of the shared cookie container.
        internal object SyncRoot => _sync;

        public bool KeepAliveEnabled => Properties.Settings.Default.keepSessionAlive;

        public event EventHandler StateChanged;

        public bool HasCookies => Cookies.GetAllCookies().Any(c => !c.Expired);

        public static string StoredCookie
        {
            get => LocalProtection.Unprotect(Properties.Settings.Default.cookie);
            set => Properties.Settings.Default.cookie = LocalProtection.Protect(value);
        }

        public void LoadFromSettings()
        {
            var cookie = StoredCookie;
            if (string.IsNullOrEmpty(cookie)) return;
            if (!LocalProtection.IsProtected(Properties.Settings.Default.cookie))
            {
                // Encrypt a plain-text cookie stored by an older version.
                StoredCookie = cookie;
                Properties.Settings.Default.Save();
            }
            foreach (var uri in LcsUris())
            {
                Cookies.SetCookies(uri, cookie);
            }
            Interlocked.Increment(ref _version);
            SetState(SessionState.Active);
        }

        public void ApplyBrowserCookies(IReadOnlyCollection<Cookie> browserCookies)
        {
            if (browserCookies == null || browserCookies.Count == 0) return;

            var lcsUri = LcsUris().First();
            lock (_sync)
            {
                // A renewal finishing after sign-out must not bring the session back.
                if (_signedOut) return;
                var preserved = Cookies.GetCookies(lcsUri).Cast<Cookie>()
                    .Where(c => c.Name == ProjectCookie || c.Name.StartsWith(AntiForgeryCookiePrefix, StringComparison.Ordinal))
                    .Select(c => new Cookie(c.Name, c.Value))
                    .ToList();

                foreach (Cookie existing in Cookies.GetAllCookies())
                {
                    existing.Expired = true;
                }

                foreach (var uri in LcsUris())
                {
                    foreach (var cookie in browserCookies.Where(c => preserved.All(p => p.Name != c.Name)))
                    {
                        TryAdd(uri, new Cookie(cookie.Name, cookie.Value));
                    }
                    foreach (var cookie in preserved)
                    {
                        TryAdd(uri, new Cookie(cookie.Name, cookie.Value));
                    }
                }

                try
                {
                    StoredCookie = Cookies.GetCookieHeader(lcsUri).Replace(';', ',');
                    Properties.Settings.Default.Save();
                }
                catch (Exception)
                {
                    // The session works without the stored copy; it is saved again with the next renewal.
                }

                Interlocked.Increment(ref _version);
                LastRenewed = DateTimeOffset.Now;
                _failedAttempts = 0;
                _nextAttempt = DateTimeOffset.MinValue;
            }
            SetState(SessionState.Active);
        }

        public Task<bool> RenewAsync()
        {
            lock (_sync)
            {
                // The sign-in window and the renewal share the browser profile - never run both.
                if (_interactiveSignIns > 0 || _signedOut) return Task.FromResult(false);
                if (_renewal == null || _renewal.IsCompleted)
                {
                    _renewal = RenewCoreAsync();
                }
                return _renewal;
            }
        }

        public Task<bool> RenewAfterFailureAsync(int versionUsedByRequest)
        {
            if (Version != versionUsedByRequest) return Task.FromResult(true);
            if (!KeepAliveEnabled) return Task.FromResult(false);
            // Interaction needed: fail fast instead of blocking every request.
            if (State == SessionState.SignInRequired && DateTimeOffset.Now < _nextAttempt) return Task.FromResult(false);
            return RenewAsync();
        }

        public void StartKeepAlive()
        {
            lock (_sync)
            {
                if (_keepAliveTimer != null) return;
                _keepAliveTimer = new Timer(_ => OnKeepAliveTick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
            }
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        public void StopKeepAlive()
        {
            lock (_sync)
            {
                _keepAliveTimer?.Dispose();
                _keepAliveTimer = null;
            }
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }

        public Task WarmUpAsync() => _browser.StartAsync();

        // Pauses silent renewal while the sign-in window is open; returns once a running renewal has finished.
        public async Task<IDisposable> BeginInteractiveSignInAsync()
        {
            Task running;
            lock (_sync)
            {
                _interactiveSignIns++;
                running = _renewal;
            }
            await WaitQuietlyAsync(running).ConfigureAwait(true);
            return new InteractiveScope(this);
        }

        public async Task SignOutAsync()
        {
            StopKeepAlive();
            Task running;
            lock (_sync)
            {
                _signedOut = true;
                running = _renewal;
            }
            await WaitQuietlyAsync(running).ConfigureAwait(false);
            lock (_sync)
            {
                foreach (Cookie cookie in Cookies.GetAllCookies())
                {
                    cookie.Expired = true;
                }
            }
            LastRenewed = null;
            try
            {
                await _browser.ClearProfileAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best effort - the app restarts right after this.
            }
            SetState(SessionState.SignedOut);
        }

        public void Shutdown()
        {
            StopKeepAlive();
            _browser.Dispose();
        }

        private async Task<bool> RenewCoreAsync()
        {
            var previous = State;
            SetState(SessionState.Renewing);
            try
            {
                // Hard limit as well, so a hung browser never leaves the session in Renewing.
                var signIn = _browser.TrySignInAsync(SignInTimeout);
                if (await Task.WhenAny(signIn, Task.Delay(SignInTimeout + TimeSpan.FromSeconds(30))).ConfigureAwait(false) != signIn)
                {
                    throw new TimeoutException();
                }
                var cookies = await signIn.ConfigureAwait(false);
                if (cookies != null && cookies.Count > 0)
                {
                    ApplyBrowserCookies(cookies);
                    if (State == SessionState.Active) return true;
                }
            }
            catch (Exception)
            {
                // Missing WebView2 runtime or crashed browser = sign-in needed.
            }

            lock (_sync)
            {
                var delay = RetryDelays[Math.Min(_failedAttempts, RetryDelays.Length - 1)];
                _failedAttempts++;
                _nextAttempt = DateTimeOffset.Now + delay;
            }
            SetState(previous == SessionState.SignedOut || _signedOut ? SessionState.SignedOut : SessionState.SignInRequired);
            return false;
        }

        private void OnKeepAliveTick()
        {
            if (!KeepAliveEnabled || !HasCookies || State == SessionState.Renewing) return;
            var now = DateTimeOffset.Now;
            if (now < _nextAttempt) return;
            if (LastRenewed == null || now - LastRenewed.Value >= RenewalInterval)
            {
                _ = RenewAsync();
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume || !KeepAliveEnabled || !HasCookies) return;
            // Give the network a moment after resume.
            Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
            {
                if (LastRenewed == null || DateTimeOffset.Now - LastRenewed.Value >= TimeSpan.FromMinutes(5))
                {
                    _failedAttempts = 0;
                    _nextAttempt = DateTimeOffset.MinValue;
                    _ = RenewAsync();
                }
            }, TaskScheduler.Default);
        }

        private static async Task WaitQuietlyAsync(Task task)
        {
            if (task == null) return;
            try
            {
                await task;
            }
            catch (Exception)
            {
                // Only waiting for it to finish.
            }
        }

        private sealed class InteractiveScope : IDisposable
        {
            private LcsSession _owner;

            public InteractiveScope(LcsSession owner) => _owner = owner;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner == null) return;
                lock (owner._sync)
                {
                    owner._interactiveSignIns--;
                }
            }
        }

        private void SetState(SessionState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TryAdd(Uri uri, Cookie cookie)
        {
            try
            {
                Cookies.Add(uri, cookie);
            }
            catch (CookieException)
            {
                // Ignore cookies System.Net cannot represent.
            }
        }

        internal static IEnumerable<Uri> LcsUris()
        {
            foreach (var url in new[] { URIHandler.LCS_URL, URIHandler.LCS_UPDATE_URL, URIHandler.LCS_DIAG_URL })
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) yield return uri;
            }
        }

        internal static bool IsLcsHost(Uri uri)
        {
            return uri != null && LcsUris().Any(u => u.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase));
        }
    }
}
