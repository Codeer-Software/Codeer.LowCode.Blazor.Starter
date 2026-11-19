using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// ジョブ (バッチ) 1 つ。このフォルダに実装クラス (引数なし public コンストラクタ) を足せば、
    /// dotnet LowCodeApp.Server.dll job &lt;Name&gt; で呼べるようになる。失敗は例外で投げる (終了コード 1 になる)。
    /// </summary>
    public interface IJob
    {
        /// <summary>コマンドラインで指定する名前 (大文字小文字は区別しない)。</summary>
        string Name { get; }

        /// <summary>
        /// このジョブを動かすサービスアカウントの名前 (appsettings の ServiceAccounts の Name)。
        /// 空なら起動したユーザーの権限で動く (画面のボタンからは押したユーザー、コマンドラインからは匿名)。
        /// </summary>
        string ServiceAccount => string.Empty;

        /// <summary>
        /// data はこのジョブ 1 回分の DB アクセス (Web のリクエストと同じ DataService)。args はジョブ名の後ろのコマンドライン引数。
        /// 戻り値は成功したときに画面に出す文言 (JobTriggerField から起動したとき。null なら既定の文言)。
        /// </summary>
        Task<string?> RunAsync(DataService data, string[] args);
    }
}
