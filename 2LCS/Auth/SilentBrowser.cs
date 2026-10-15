using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LCS.Auth
{
    // Hidden WebView2 on its own STA thread, so the session can be renewed while the UI thread blocks in .Result.
    internal sealed class SilentBrowser : IDisposable
    {
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(20);
        // A silent Entra ID page submits itself at once; an idle one waits for the user.
        private static readonly TimeSpan SignInPageIdleTimeout = TimeSpan.FromSeconds(8);

        private readonly object _sync = new object();
        private Task _startTask;
        private readonly List<Action<Exception>> _pending = new List<Action<Exception>>();
        private HostWindow _host;
        private CoreWebView2Controller _controller;

        public Task StartAsync()
        {
            lock (_sync)
            {
                if (_startTask == null || _startTask.IsFaulted || _startTask.IsCanceled)
                {
                    _startTask = StartThread();
                }
                return _startTask;
            }
        }

        public async Task<List<Cookie>> TrySignInAsync(TimeSpan timeout)
        {
            await StartAsync().ConfigureAwait(false);
            return await InvokeAsync(() => SignInCoreAsync(timeout)).ConfigureAwait(false);
        }

        public async Task ClearProfileAsync()
        {
            await StartAsync().ConfigureAwait(false);
            await InvokeAsync(async () =>
            {
                await _controller.CoreWebView2.Profile.ClearBrowsingDataAsync();
                return true;
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            var host = _host;
            if (host == null || !host.IsHandleCreated) return;
            try
            {
                host.BeginInvoke(new Action(() =>
                {
                    _controller?.Close();
                    _controller = null;
                    host.Dispose();
                    Application.ExitThread();
                }));
            }
            catch (InvalidOperationException)
            {
                // Thread already gone.
            }
        }

        private Task StartThread()
        {
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    _host = new HostWindow();
                    _host.CreateHandleNow();
                }
                catch (Exception ex)
                {
                    ready.TrySetException(ex);
                    return;
                }

                _host.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var environment = await WebView2Profile.CreateEnvironmentAsync();
                        _controller = await environment.CreateCoreWebView2ControllerAsync(_host.Handle);
                        _controller.Bounds = new Rectangle(0, 0, 1280, 800);
                        var settings = _controller.CoreWebView2.Settings;
                        settings.AreDefaultScriptDialogsEnabled = false;
                        settings.AreDevToolsEnabled = false;
                        settings.IsStatusBarEnabled = false;
                        _controller.CoreWebView2.NewWindowRequested += (s, e) => e.Handled = true;
                        _controller.CoreWebView2.ProcessFailed += OnProcessFailed;
                        ready.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        ready.TrySetException(ex);
                        Application.ExitThread();
                    }
                }));

                Application.Run();
            })
            {
                IsBackground = true,
                Name = "2LCS session renewal"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return ready.Task;
        }

        // Runs on the browser thread. The next StartAsync starts a new thread and browser.
        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            if (e.ProcessFailedKind != CoreWebView2ProcessFailedKind.BrowserProcessExited
                && e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessExited
                && e.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessUnresponsive) return;
            List<Action<Exception>> pending;
            lock (_sync)
            {
                _startTask = null;
                pending = new List<Action<Exception>>(_pending);
            }
            // Continuations on this thread never run once it exits.
            foreach (var fail in pending)
            {
                fail(new InvalidOperationException("The WebView2 process exited."));
            }
            try
            {
                _controller?.Close();
            }
            catch (Exception)
            {
                // The browser is gone already.
            }
            _controller = null;
            _host?.Dispose();
            Application.ExitThread();
        }

        private Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<Exception> fail = ex => tcs.TrySetException(ex);
            lock (_sync)
            {
                _pending.Add(fail);
            }
            tcs.Task.ContinueWith(_ =>
            {
                lock (_sync)
                {
                    _pending.Remove(fail);
                }
            }, TaskScheduler.Default);
            _host.BeginInvoke(new Action(async () =>
            {
                try
                {
                    tcs.TrySetResult(await action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        // Runs on the browser thread.
        private async Task<List<Cookie>> SignInCoreAsync(TimeSpan timeout)
        {
            var core = _controller.CoreWebView2;

            // Drop the LCS cookies so LCS issues a fresh session; Entra ID cookies stay, so sign-in is silent.
            foreach (var cookie in await core.CookieManager.GetCookiesAsync(URIHandler.LCS_URL))
            {
                core.CookieManager.DeleteCookie(cookie);
            }

            var signedIn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lastActivity = DateTime.UtcNow;
            var navigating = false;
            var onSignInPage = false;

            void OnStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
            {
                navigating = true;
                lastActivity = DateTime.UtcNow;
            }

            void OnCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                navigating = false;
                lastActivity = DateTime.UtcNow;
                if (!Uri.TryCreate(core.Source, UriKind.Absolute, out var source)) return;
                onSignInPage = WebView2Profile.IsSignInHost(source);
                if (WebView2Profile.IsSignedInUri(source))
                {
                    signedIn.TrySetResult(true);
                }
            }

            core.NavigationStarting += OnStarting;
            core.NavigationCompleted += OnCompleted;
            try
            {
                core.Navigate($"{URIHandler.LCS_URL}/Logon/AdLogon");
                var deadline = DateTime.UtcNow + timeout;
                while (!signedIn.Task.IsCompleted)
                {
                    await Task.WhenAny(signedIn.Task, Task.Delay(250));
                    if (signedIn.Task.IsCompleted) break;

                    // Idle and not signed in = a page waits for the user.
                    var idle = onSignInPage ? SignInPageIdleTimeout : IdleTimeout;
                    if (DateTime.UtcNow > deadline || (!navigating && DateTime.UtcNow - lastActivity > idle))
                    {
                        return null;
                    }
                }
                return await WebView2Profile.ReadLcsCookiesAsync(core.CookieManager);
            }
            finally
            {
                core.NavigationStarting -= OnStarting;
                core.NavigationCompleted -= OnCompleted;
                // Do not keep the LCS page running in the background.
                core.Navigate("about:blank");
            }
        }

        private sealed class HostWindow : Control
        {
            public void CreateHandleNow()
            {
                if (!IsHandleCreated) CreateHandle();
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_POPUP = unchecked((int)0x80000000);
                    const int WS_EX_TOOLWINDOW = 0x80;
                    var cp = base.CreateParams;
                    cp.Style = WS_POPUP;
                    cp.ExStyle |= WS_EX_TOOLWINDOW;
                    cp.X = -32000;
                    cp.Y = -32000;
                    cp.Width = 1280;
                    cp.Height = 800;
                    return cp;
                }
            }
        }
    }
}
