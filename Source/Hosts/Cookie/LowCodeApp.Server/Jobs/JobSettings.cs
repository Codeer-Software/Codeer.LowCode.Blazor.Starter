namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// appsettings の Jobs。
    /// </summary>
    public class JobSettings
    {
        /// <summary>
        /// 画面のボタン (JobTriggerField) から起動したジョブの実行先 (ジョブ名 → 実行先)。
        /// "Server" = サーバー内実行 (Web サーバーのプロセスの中でそのまま動かす)。書いていないジョブは別プロセスで起動する。
        /// </summary>
        public Dictionary<string, string> RunIn { get; set; } = new();
    }
}
