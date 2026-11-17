namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// appsettings の ServiceAccounts の 1 つ分。ジョブが名前で指すサービスアカウント (ジョブを動かすユーザー)。
    /// 指すのはジョブの側: IJob は ServiceAccount プロパティ、デザイナで作った Job はデザイナの Job のプロパティ。
    /// </summary>
    public class ServiceAccount
    {
        /// <summary>
        /// サービスアカウントの名前。ジョブはこの名前で指す。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// そのユーザーのログイン ID (ログイン画面で入力するもの)。
        /// </summary>
        public string User { get; set; } = string.Empty;
    }
}
