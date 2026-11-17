namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// 画面のボタン (JobTriggerField) から起動したジョブを動かすプロセス。
    /// </summary>
    public enum JobTriggerProcessType
    {
        /// <summary>別プロセスで起動する (既定)。</summary>
        NewProcess,

        /// <summary>プロセス内実行 (Web サーバーのプロセスの中でそのまま動かす)。</summary>
        InProcess,
    }
}
