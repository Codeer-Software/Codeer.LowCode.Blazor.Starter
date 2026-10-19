using LowCodeApp.Jobs.Services;

namespace LowCodeApp.Jobs
{
    /// <summary>
    /// バッチのジョブ 1 つ。このプロジェクトに実装クラス (引数なし public コンストラクタ) を足せば、Name で呼べるようになる。
    /// 失敗は例外で投げる (終了コード 1 になる)。
    /// </summary>
    public interface IJob
    {
        /// <summary>コマンドラインで指定する名前 (大文字小文字は区別しない)。</summary>
        string Name { get; }

        /// <summary>data はこのジョブ 1 回分の DB アクセス。args はジョブ名の後ろのコマンドライン引数。</summary>
        Task RunAsync(DataService data, string[] args);
    }
}
