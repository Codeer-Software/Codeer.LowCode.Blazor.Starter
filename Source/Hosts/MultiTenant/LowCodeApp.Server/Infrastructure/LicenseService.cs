using Codeer.LowCode.Blazor.License;
using System.Collections.Concurrent;

namespace LowCodeApp.Server.Infrastructure
{
    static class LicenseService
    {
        static readonly ConcurrentDictionary<string, DateTime> _checkedTimes = new();

        //サーバーライセンスの更新 (テナントごとに 1 分間隔。失敗しても処理は続ける)
        internal static async Task UpdateAsync(HttpRequest request, MultiTenantUserInfo userInfo)
        {
            var now = DateTime.Now;
            if (_checkedTimes.TryGetValue(userInfo.TenantKey, out var last) && now - last < TimeSpan.FromMinutes(1)) return;
            _checkedTimes[userInfo.TenantKey] = now;
            try
            {
                await LicenseManager.CheckServerLicenseForMultiTenant(request, userInfo.TenantKey, userInfo.LicenseString);
            }
            catch { }
        }
    }
}
