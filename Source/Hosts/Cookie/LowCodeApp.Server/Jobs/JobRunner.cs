using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.Jobs;
using LowCodeApp.Server.Extras;
using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// このアプリのジョブの一覧と実行。dotnet LowCodeApp.Server.dll job &lt;ジョブ名&gt; [引数...] で呼ばれる (別プロセス)。
    /// 画面のボタンから起動するジョブは、appsettings の Jobs:RunIn で "Server" にすると Web サーバーのプロセスの中で動く (サーバー内実行)。
    /// ジョブは 2 種類: このアセンブリの IJob (C#) と、デザインの Job (Jobs/名前.job.cs)。同じ名前なら IJob を動かす。
    /// </summary>
    public static class JobRunner
    {
        //デザインの Job のスクリプトから MailService (メール) と Excel を使えるようにする。メールの設定は画面のメールと同じ (appsettings の Mail と MailSenderTable)
        static JobRunner()
        {
            DesignJobs.UseMail(SystemConfig.Instance.Mail, MailSenderTable.Create);
            DesignJobs.UseExcel();
        }

        /// <summary>動かせるジョブの名前 (IJob とデザインの Job)。</summary>
        public static IReadOnlyList<string> Names
            => CreateJobs().Select(e => e.Name)
                .Union(DesignJobs.GetNames(() => AppDesign.Store.GetDesignData()), StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>ジョブを 1 つ動かす (別プロセス)。name は Names のどれか、args はジョブ名の後ろの引数。戻り値は成功したときの文言 (画面に出る)、失敗は例外。</summary>
        public static async Task<string?> RunAsync(IConfiguration config, string name, string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
            //意味検索の警告ログ (埋め込みの失敗など) を標準出力に出す
            SemanticSearchTable.Service.LoggerFactory = loggerFactory;
            //実行した SQL とパラメータを標準出力に出す
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = Console.WriteLine;

            //起動したユーザー (空なら匿名)。画面のボタンから起動したときは押したユーザー。Jobs:RunAs に無いジョブは、このユーザーの権限で動く
            return await RunAsync(name, args, config["Jobs:UserId"] ?? string.Empty);
        }

        /// <summary>そのジョブをサーバー内実行にするか (appsettings の Jobs:RunIn が "Server")。</summary>
        public static bool RunsInServer(string name)
            => SystemConfig.Instance.Jobs.RunIn.Any(e => string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Value, "Server", StringComparison.OrdinalIgnoreCase));

        /// <summary>ジョブを 1 つ動かす。userId は起動したユーザー (サーバー内実行では画面のボタンを押したユーザー)。戻り値は成功したときの文言、失敗は例外。</summary>
        public static async Task<string?> RunAsync(string name, string[] args, string userId)
        {
            if (!File.Exists(Path.Combine(SystemConfig.Instance.DesignFileDirectory, "App.zip")))
            {
                throw new FileNotFoundException($"DesignFileDirectory に App.zip がありません: {SystemConfig.Instance.DesignFileDirectory}");
            }

            //appsettings の Jobs:RunAs に書いたジョブは、起動したユーザーによらず、そこに書いたユーザーの権限で読み書きする
            var runAs = SystemConfig.Instance.Jobs.RunAs.FirstOrDefault(e => string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            await using var data = new DataService(string.IsNullOrEmpty(runAs) ? userId : runAs);
            var job = CreateJobs().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (job != null) return await job.RunAsync(data, args);

            //IJob に無い名前はデザインの Job。読み書きは IJob と同じ data.ModuleDataIO を通る。スクリプトの Resources はデザインのリソースを読み、Invoker は起動したユーザー。
            //FileService (添付ファイルの読み書き) の置き場所は画面のアップロード・ダウンロードと同じ
            return await DesignJobs.RunAsync(data.ModuleDataIO, data.Design.DesignData, name, args, AppDesign.Store.GetResource, userId, data.TemporaryFileManager);
        }

        static IEnumerable<IJob> CreateJobs()
            => typeof(IJob).Assembly.GetTypes()
                .Where(e => typeof(IJob).IsAssignableFrom(e) && e.IsClass && !e.IsAbstract && e.GetConstructor(Type.EmptyTypes) != null)
                .Select(e => (IJob)Activator.CreateInstance(e)!);
    }
}
