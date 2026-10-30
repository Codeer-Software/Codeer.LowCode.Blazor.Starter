using System.Net;

namespace LowCodeApp.Maui.Services
{
    /// <summary>
    /// サーバーとの接続。Cookie (認証・antiforgery) と、X-ANTIFORGERY-TOKEN ヘッダに入れるトークンを持つ (ブラウザが行うことをネイティブアプリではここで行う)。
    /// </summary>
    public class ServerConnection
    {
        const string AntiforgeryTokenName = "X-ANTIFORGERY-TOKEN";

        readonly CookieContainer _cookies = new();
        string? _antiforgeryToken;

        /// <summary>現在のサーバー URL (ServerSettings)。HttpClient を作るときに読む。</summary>
        public Uri BaseAddress => new(ServerSettings.BaseUrl);

        public HttpClient CreateHttpClient()
        {
            //HttpClient は BlazorWebView ごとに作るので、サーバー URL の変更はページの再起動後に効く
            _antiforgeryToken = null;
            var inner = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = _cookies
            };
#if DEBUG
            //開発証明書は localhost 向けで端末からは信頼されない (エミュレータは 10.0.2.2 で PC に届く)。Debug ビルドだけ証明書を検証しない
            inner.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif
            var handler = new AntiforgeryHandler(this) { InnerHandler = inner };
            //大きな添付のアップロードに備えて長めにする (BaseUrl の誤りは直前の antiforgery の取得が短いタイムアウトで失敗する)
            return new HttpClient(handler) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromMinutes(30) };
        }

        /// <summary>
        /// antiforgery トークンはログインユーザーに紐づく。ログイン / ログアウトの後に呼ぶと次の要求で取り直す。
        /// </summary>
        public void ResetAntiforgeryToken() => _antiforgeryToken = null;

        //トークンは GET api/account/antiforgery の Set-Cookie ヘッダから読む
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
                //antiforgery の検証は POST 等の更新系だけ
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
                //ログイン前の最初の要求。BaseUrl の誤りが固まって見えないよう短く打ち切る
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_connection.BaseAddress, "api/account/antiforgery"));
                using var response = await base.SendAsync(request, timeout.Token);
                _connection.CaptureAntiforgeryToken(response);
                return _connection._antiforgeryToken;
            }
        }
    }
}
