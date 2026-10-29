using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.Design;

namespace LowCodeApp.Server.Infrastructure
{
    /// <summary>
    /// 配置されたデザイン (DesignFileDirectory の App.zip)。プロセスに 1 つ。App.zip が差し替わると次の参照から新しい版になる (読み直したときは表定義のキャッシュを捨てる)。
    /// リクエストの中では版を途中で変えないよう RequestDesign (DataService.Design) を使う。ここを直接使うのはリクエストの外 (起動時・ジョブ・バックグラウンド)。
    /// </summary>
    static class AppDesign
    {
        static readonly Lazy<DesignDataStore> _store = new(() => new(SystemConfig.Instance.DesignFileDirectory, DbAccessor.ClearTableDefinitionCache));
        public static DesignDataStore Store => _store.Value;
    }
}
