using Microsoft.AspNetCore.Components;
using System.Net;

namespace LowCodeApp.Client
{
    /// <summary>
    /// 開いている画面からの API 呼び出しが 401 (ログインしていない / Cookie が失効した: 有効期限切れ・パスワード変更・停止・全端末ログアウト) を返したら、
    /// ログイン画面へ抜ける (login.html は Blazor の外なのでフルナビゲーション)。元の画面は returnUrl で戻る。
    /// 失効した Cookie はサーバーが消しているので、ログイン画面側で改めてログインする。403 (権限不足) はそのまま (画面のエラー表示に任せる)。
    /// </summary>
    public class UnauthorizedToLoginHandler : DelegatingHandler
    {
        readonly NavigationManager _nav;

        public UnauthorizedToLoginHandler(NavigationManager nav)
        {
            _nav = nav;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) NavigateToLogin(_nav);
            return response;
        }

        /// <summary>ログイン画面 (login.html) へ抜ける。今の画面を returnUrl に積む (ログイン後に戻る)。</summary>
        public static void NavigateToLogin(NavigationManager nav)
        {
            var returnUrl = "/" + nav.ToBaseRelativePath(nav.Uri);
            nav.NavigateTo(returnUrl == "/" ? "login.html" : "login.html?returnUrl=" + Uri.EscapeDataString(returnUrl), forceLoad: true);
        }
    }
}
