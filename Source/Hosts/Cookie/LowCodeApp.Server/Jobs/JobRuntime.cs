namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// appsettings の JobRuntimes の 1 つ分。ジョブを動かす環境 (誰の権限で・どのプロセスで) と、そこで動かすジョブ。
    /// どのランタイムにも書いていないジョブは既定で動く (起動したユーザーの権限・画面のボタンからは別プロセス)。
    /// </summary>
    public class JobRuntime
    {
        /// <summary>
        /// ランタイムの名前 (ジョブの名前ではない)。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// ジョブを動かすユーザーのログイン ID (ログイン画面で入力するもの)。
        /// 書くと、起動したユーザーによらず、このユーザーの権限で読み書きする。書かなければ起動したユーザーの権限で動く。
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// 画面のボタン (JobTriggerField) から起動したときに動かすプロセス。コマンドラインからの起動には関係しない。振り分けるのは JobRunner.RunFromScreenAsync。
        /// </summary>
        public JobTriggerProcessType JobTriggerProcessType { get; set; }

        /// <summary>
        /// このランタイムで動かすジョブの名前 (IJob の Name、デザイナで作った Job の名前)。
        /// </summary>
        public string[] Jobs { get; set; } = [];
    }
}
