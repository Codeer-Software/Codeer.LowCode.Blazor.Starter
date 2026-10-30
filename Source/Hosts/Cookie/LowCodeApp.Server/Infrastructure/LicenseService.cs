using Codeer.LowCode.Blazor.License;

namespace LowCodeApp.Server.Infrastructure
{
    static class LicenseService
    {
        static DateTime _checkedTime;

        //サーバーライセンスの更新 (1 分間隔。失敗しても処理は続ける)
        internal static async Task UpdateAsync(HttpRequest request)
        {
            var now = DateTime.Now;
            if (now - _checkedTime < TimeSpan.FromMinutes(1)) return;
            _checkedTime = now;
            try
            {
                await LicenseManager.CheckServerLicense(request);
            }
            catch { }
        }
    }
}
