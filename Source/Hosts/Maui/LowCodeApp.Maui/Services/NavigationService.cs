using Codeer.LowCode.Blazor.Extras.Services;
using Codeer.LowCode.Blazor.RequestInterfaces;
using Microsoft.AspNetCore.Components;
using LowCodeApp.Client.Shared.Services;

namespace LowCodeApp.Maui.Services
{
    public class NavigationService : NavigationServiceBase
    {
        readonly IHttpService _http;
        readonly NavigationManager _nav;
        readonly ServerConnection _server;

        public NavigationService(NavigationManager nav, IHttpService http, ServerConnection server) : base(nav)
        {
            _http = http;
            _nav = nav;
            _server = server;
        }

        public override bool CanLogout => true;

        public override async Task Logout()
        {
            await _http.PostAsJsonAsync("api/account/logout", "");
            _server.ResetAntiforgeryToken();
            _nav.NavigateTo("/login", true);
        }
    }
}
