using Codeer.LowCode.Blazor.Extras.Services;
using Microsoft.AspNetCore.Components;
using LowCodeApp.Client.Shared.Services;
using Codeer.LowCode.Blazor.RequestInterfaces;

namespace LowCodeApp.Client
{
    public class NavigationService : NavigationServiceBase
    {
        readonly IHttpService _http;
        readonly NavigationManager _nav;
        readonly IAppInfoService _appInfo;

        public NavigationService(NavigationManager nav, IHttpService http, IAppInfoService appInfo) : base(nav)
        {
            _http = http;
            _nav = nav;
            _appInfo = appInfo;
        }

        public override bool CanLogout => true;

        public override async Task Logout()
        {
            //外部 IdP のログアウトはブラウザ遷移が要るので、サーバーが遷移先 URL を返したらそこへ遷移する
            var result = await _http.PostAsJsonAsync<string, LogoutResult>("api/account/logout", "");
            _nav.NavigateTo(string.IsNullOrEmpty(result?.Redirect) ? "/" : result.Redirect, true);
        }
    }
}
