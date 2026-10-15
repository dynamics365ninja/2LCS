using LCS.Auth;
using LCS.Theming;
using Microsoft.Web.WebView2.Core;
using System;
using System.Windows.Forms;

namespace LCS.Forms
{
    // Interactive LCS sign-in. The WebView2 profile is shared with the background session renewal.
    public partial class Login : ThemedForm
    {
        private bool _completed;
        private IDisposable _renewalPause;

        public Login()
        {
            InitializeComponent();
            subtitleLabel.ForeColor = Theme.Current.TextSecondary;
            Cancelled = true;
        }

        internal bool Cancelled { get; private set; }

        protected override void OnLoad(EventArgs e)
        {
            var area = Screen.FromControl(Owner ?? (Control)this).WorkingArea;
            if (Height > area.Height) Height = area.Height;
            base.OnLoad(e);
        }

        private async void Login_Load(object sender, EventArgs e)
        {
            progressLine.Running = true;
            _renewalPause = await LcsSession.Current.BeginInteractiveSignInAsync();
            if (IsDisposed)
            {
                _renewalPause.Dispose();
                return;
            }
            try
            {
                var environment = await WebView2Profile.CreateEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex) when (ex is WebView2RuntimeNotFoundException || ex is System.Runtime.InteropServices.COMException)
            {
                progressLine.Running = false;
                FluentMessageBox.Show(this,
                    $"Signing in requires the Microsoft Edge WebView2 Runtime.{Environment.NewLine}{Environment.NewLine}" +
                    $"Download it from {WebView2Profile.RuntimeDownloadUrl} and try again.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    "Sign in to LCS", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            var core = webView.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NavigationStarting += (s, args) => progressLine.Running = true;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.NewWindowRequested += (s, args) =>
            {
                // Keep pop-ups inside this window.
                args.Handled = true;
                core.Navigate(args.Uri);
            };

            // Start with a fresh LCS session; Entra ID cookies stay, so an existing sign-in closes this window at once.
            foreach (var cookie in await core.CookieManager.GetCookiesAsync(URIHandler.LCS_URL))
            {
                core.CookieManager.DeleteCookie(cookie);
            }
            core.Navigate($"{URIHandler.LCS_URL}/Logon/AdLogon");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _renewalPause?.Dispose();
            base.OnFormClosed(e);
        }

        private async void Core_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            progressLine.Running = false;
            if (_completed || !Uri.TryCreate(webView.Source?.ToString(), UriKind.Absolute, out var source)
                || !WebView2Profile.IsSignedInUri(source))
            {
                return;
            }

            _completed = true;
            var cookies = await WebView2Profile.ReadLcsCookiesAsync(webView.CoreWebView2.CookieManager);
            if (cookies.Count == 0)
            {
                _completed = false;
                return;
            }
            LcsSession.Current.ApplyBrowserCookies(cookies);
            Cancelled = false;
            Close();
        }
    }
}
