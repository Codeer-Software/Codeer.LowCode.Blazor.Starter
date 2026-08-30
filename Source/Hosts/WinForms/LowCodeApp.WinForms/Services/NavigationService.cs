using Microsoft.AspNetCore.Components;
using LowCodeApp.Client.Shared.Services;

namespace LowCodeApp.WinForms.Services
{
    public class NavigationService : NavigationServiceBase
    {
        public NavigationService(NavigationManager nav) : base(nav) { }
        public override bool CanLogout => false;
        public override async Task Logout() => await Task.CompletedTask;
    }
}
