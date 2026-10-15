using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace LCS.Auth
{
    // Persistent profile shared by all WebView2 instances, so the Entra ID session survives restarts.
    internal static class WebView2Profile
    {
        public const string RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        private static readonly string[] SignInHosts =
        {
            "login.microsoftonline.com",
            "login.windows.net",
            "login.microsoft.com",
            "login.live.com",
            "device.login.microsoftonline.com"
        };

        public static string UserDataFolder { get; internal set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "2LCS", "WebView2");

        public static Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            Directory.CreateDirectory(UserDataFolder);
            var options = new CoreWebView2EnvironmentOptions
            {
                // SSO with the Windows account on Entra joined/registered devices.
                AllowSingleSignOnUsingOSPrimaryAccount = true
            };
            return CoreWebView2Environment.CreateAsync(null, UserDataFolder, options);
        }

        public static bool IsRuntimeInstalled()
        {
            try
            {
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch (WebView2RuntimeNotFoundException)
            {
                return false;
            }
        }

        public static bool IsSignedInUri(Uri uri)
        {
            if (uri == null || !Uri.TryCreate(URIHandler.LCS_URL, UriKind.Absolute, out var lcs)) return false;
            return uri.Host.Equals(lcs.Host, StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith("/v2", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSignInHost(Uri uri)
        {
            return uri != null && SignInHosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<List<Cookie>> ReadLcsCookiesAsync(CoreWebView2CookieManager cookieManager)
        {
            var result = new List<Cookie>();
            foreach (var cookie in await cookieManager.GetCookiesAsync(URIHandler.LCS_URL))
            {
                try
                {
                    result.Add(cookie.ToSystemNetCookie());
                }
                catch (CookieException)
                {
                    // Not representable by System.Net (e.g. contains a comma).
                }
            }
            return result;
        }
    }
}
