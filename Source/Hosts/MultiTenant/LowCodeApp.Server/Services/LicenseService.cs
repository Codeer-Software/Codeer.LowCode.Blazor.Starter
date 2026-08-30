using Codeer.LowCode.Blazor.License;
using System.Collections.Concurrent;

namespace LowCodeApp.Server.Services
{
    static class LicenseService
    {
        static readonly ConcurrentDictionary<string, DateTime> _checkedTimes = new();

        //ライセンス更新。専用エンドポイントは持たず、デザインデータ取得の直前に呼ぶ。
        //テナントごとに1分間隔にスロットリングし、更新に失敗しても既存のライセンス状態で処理を続行させる。
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
