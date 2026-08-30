using Codeer.LowCode.Blazor.DataIO;

namespace LowCodeApp.Wpf.Services
{
    public class AuthenticationContext : IAuthenticationContext
    {
        public async Task<string> GetCurrentUserIdAsync()
        {
            await Task.CompletedTask;
            return string.Empty;
        }
    }

}
