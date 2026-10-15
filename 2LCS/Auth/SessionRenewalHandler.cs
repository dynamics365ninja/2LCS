using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LCS.Auth
{
    // Renews an expired session and replays the request once. ConfigureAwait(false) everywhere:
    // many callers block the UI thread with .Result.
    internal sealed class SessionRenewalHandler : DelegatingHandler
    {
        private readonly LcsSession _session;

        public SessionRenewalHandler(HttpMessageHandler innerHandler, LcsSession session) : base(innerHandler)
        {
            _session = session;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var originalUri = request.RequestUri;
            if (!LcsSession.IsLcsHost(originalUri))
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            // Capture the request up front: redirects mutate it and the cookie handler adds a Cookie header.
            var method = request.Method;
            var headers = request.Headers
                .Where(h => !h.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                .Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray()))
                .ToList();
            byte[] body = null;
            List<KeyValuePair<string, string[]>> contentHeaders = null;
            if (request.Content != null)
            {
                // Buffers the content so it can be resent.
                body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                contentHeaders = request.Content.Headers
                    .Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray()))
                    .ToList();
            }

            var version = _session.Version;
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!IsSessionExpired(response, originalUri))
            {
                return response;
            }

            if (!await _session.RenewAfterFailureAsync(version).ConfigureAwait(false))
            {
                // Report the expiry instead of letting callers parse the sign-in page.
                response.Dispose();
                throw new LcsSessionExpiredException();
            }

            var retry = new HttpRequestMessage(method, originalUri) { Version = request.Version };
            foreach (var header in headers)
            {
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            if (body != null)
            {
                retry.Content = new ByteArrayContent(body);
                foreach (var header in contentHeaders)
                {
                    retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            response.Dispose();
            return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        private static bool IsSessionExpired(HttpResponseMessage response, Uri originalUri)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) return true;

            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri == null || finalUri == originalUri) return false;
            if (WebView2Profile.IsSignInHost(finalUri)) return true;
            return LcsSession.IsLcsHost(finalUri)
                && finalUri.AbsolutePath.StartsWith("/Logon", StringComparison.OrdinalIgnoreCase)
                && !originalUri.AbsolutePath.StartsWith("/Logon", StringComparison.OrdinalIgnoreCase);
        }
    }
}
