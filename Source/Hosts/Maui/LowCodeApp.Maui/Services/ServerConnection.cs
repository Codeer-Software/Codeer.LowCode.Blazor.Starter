using System.Net;

namespace LowCodeApp.Maui.Services
{
    /// <summary>
    /// Connection to the Codeer.LowCode.Blazor server (the Cookie authentication variant).
    /// Holds the cookie jar (authentication cookie + antiforgery cookie) and the antiforgery
    /// request token that the server expects in the X-ANTIFORGERY-TOKEN header.
    /// In the browser these are handled by the browser itself; in a native app we do it here.
    /// </summary>
    public class ServerConnection
    {
        const string AntiforgeryTokenName = "X-ANTIFORGERY-TOKEN";

        readonly CookieContainer _cookies = new();
        string? _antiforgeryToken;
        bool _antiforgeryUnavailable;
        HttpMessageHandler? _handler;
        Uri? _handlerBaseAddress;
        Task<HttpResponseMessage>? _currentUserPrewarm;
        Uri? _prewarmBaseAddress;

        /// <summary>Current server URL (see ServerSettings). Read when an HttpClient is created.</summary>
        public Uri BaseAddress => new(ServerSettings.BaseUrl);

        public HttpClient CreateHttpClient()
        {
            //A new client is created per BlazorWebView, so a changed server URL is picked up after the page restarts.
            _antiforgeryToken = null;
            _antiforgeryUnavailable = false;
            return CreateClient();
        }

        //All clients share one handler so they also share its connection pool: the TLS handshake is paid once,
        //not again per BlazorWebView. The handler outlives the clients, so they must not dispose it.
        HttpMessageHandler GetHandler()
        {
            var baseAddress = BaseAddress;
            if (_handler != null && _handlerBaseAddress == baseAddress) return _handler;

            (_handler as IDisposable)?.Dispose();
            var inner = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookies,
                //The server compresses its responses (UseResponseCompression), but HttpClientHandler does not
                //ask for that by default, unlike a browser. Without this the design data arrives uncompressed:
                //364KB instead of 31KB on every start.
                AutomaticDecompression = DecompressionMethods.All
            };
#if DEBUG
            //The ASP.NET Core development certificate is issued for "localhost" and is not trusted by the device,
            //while the emulator reaches the PC as 10.0.2.2. Accept any certificate in Debug builds only.
            inner.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif
            _handler = new AntiforgeryHandler(this) { InnerHandler = inner };
            _handlerBaseAddress = baseAddress;
            return _handler;
        }

        //The default 100 second timeout makes a wrong BaseUrl look like a hang.
        HttpClient CreateClient()
            => new(GetHandler(), disposeHandler: false) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Starts the "who is signed in" request at app startup so it runs while the WebView and the Blazor
        /// runtime boot, instead of adding its round trip afterwards. The result is handed to the first page
        /// that asks for it (see TakeCurrentUserPrewarm); a later page issues its own request as before.
        /// </summary>
        public void PrewarmCurrentUser()
        {
            try
            {
                _prewarmBaseAddress = BaseAddress;
                _currentUserPrewarm = CreateClient().GetAsync("api/account/current_user");
                //Keep a failure from surfacing as an unobserved task exception when nobody takes the result.
                //Awaiting the task later still observes the exception.
                _ = _currentUserPrewarm.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch
            {
                //Prewarming is an optimization; the page falls back to requesting it itself.
                _currentUserPrewarm = null;
            }
        }

        /// <summary>
        /// Returns the prewarmed current_user response once, or null when there is none to use - already taken,
        /// or made against a server URL the user has since changed.
        /// </summary>
        public Task<HttpResponseMessage>? TakeCurrentUserPrewarm()
        {
            var prewarm = _currentUserPrewarm;
            _currentUserPrewarm = null;
            if (prewarm == null) return null;
            if (_prewarmBaseAddress == BaseAddress) return prewarm;

            _ = prewarm.ContinueWith(t => t.Result.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
            return null;
        }

        /// <summary>
        /// The antiforgery token is bound to the signed-in user. Call this after login/logout so the next
        /// request fetches a token for the new identity.
        /// </summary>
        public void ResetAntiforgeryToken()
        {
            _antiforgeryToken = null;
            _antiforgeryUnavailable = false;
        }

        //The server issues the token as a readable cookie from GET api/account/antiforgery (and marks it Secure,
        //so the cookie container would not send it back over http). We read it from the Set-Cookie header instead.
        void CaptureAntiforgeryToken(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies)) return;
            foreach (var setCookie in setCookies)
            {
                var first = setCookie.Split(';')[0].Trim();
                var eq = first.IndexOf('=');
                if (eq <= 0) continue;
                if (first.Substring(0, eq) != AntiforgeryTokenName) continue;
                _antiforgeryToken = Uri.UnescapeDataString(first.Substring(eq + 1));
            }
        }

        class AntiforgeryHandler : DelegatingHandler
        {
            readonly ServerConnection _connection;

            public AntiforgeryHandler(ServerConnection connection)
            {
                _connection = connection;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                //AutoValidateAntiforgeryToken only validates unsafe methods.
                if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
                {
                    var token = await GetAntiforgeryTokenAsync(cancellationToken);
                    if (token != null)
                    {
                        request.Headers.Remove(AntiforgeryTokenName);
                        request.Headers.Add(AntiforgeryTokenName, token);
                    }
                }

                var response = await base.SendAsync(request, cancellationToken);
                _connection.CaptureAntiforgeryToken(response);
                return response;
            }

            async Task<string?> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
            {
                if (_connection._antiforgeryToken != null) return _connection._antiforgeryToken;
                //A host variant without authentication issues no token (and needs none). Ask once, then stop,
                //so every write does not pay for an extra round trip.
                if (_connection._antiforgeryUnavailable) return null;
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_connection.BaseAddress, "api/account/antiforgery"));
                using var response = await base.SendAsync(request, cancellationToken);
                _connection.CaptureAntiforgeryToken(response);
                if (_connection._antiforgeryToken == null) _connection._antiforgeryUnavailable = true;
                return _connection._antiforgeryToken;
            }
        }
    }
}
