using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Codeer.LowCode.Blazor.Extras.Server.Jobs;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.Jobs;
using LowCodeApp.Server.Extras;
using LowCodeApp.Server.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// このアプリのジョブの一覧と実行。dotnet LowCodeApp.Server.dll job &lt;ジョブ名&gt; [引数...] で呼ばれる (別プロセス)。
    /// 画面のボタンから起動するジョブは、appsettings の JobRuntimes で JobTriggerProcessType が InProcess のランタイムに入れると Web サーバーのプロセスの中で動く (プロセス内実行)。
    /// ジョブは 2 種類: このアセンブリの IJob (C#) と、デザイナで作った Job (Jobs/名前.job.cs)。同じ名前なら IJob を動かす。
    /// </summary>
    public static class JobRunner
    {
        //デザイナで作った Job のスクリプトから MailService (メール) と Excel を使えるようにする。メールの設定は画面のメールと同じ (appsettings の Mail と MailSenderTable)。
        //自分のサービスを Job のスクリプトで使うときも、ここで登録する (Jobs/README.md)
        static JobRunner()
        {
            MailApi.UseInJobScripts(SystemConfig.Instance.Mail, MailSenderTable.Create);
            ExcelApi.UseInJobScripts();
        }

        /// <summary>動かせるジョブの名前 (IJob とデザイナで作った Job)。</summary>
        public static IReadOnlyList<string> Names
            => CreateJobs().Select(e => e.Name).Union(GetDesignerJobNames(), StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>ジョブを 1 つ動かす (別プロセス)。name は Names のどれか、args はジョブ名の後ろの引数。戻り値は成功したときの文言 (画面に出る)、失敗は例外。</summary>
        public static async Task<string?> RunAsync(IConfiguration config, string name, string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
            //意味検索の警告ログ (埋め込みの失敗など) を標準出力に出す
            SemanticSearchTable.Service.LoggerFactory = loggerFactory;
            //実行した SQL とパラメータを標準出力に出す
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = Console.WriteLine;

            //起動したユーザー。画面のボタンから別プロセスで起動されたときは、押したユーザーの Id が環境変数 Jobs__UserId (= 設定の Jobs:UserId) で渡ってくる。
            //コマンドラインからは空 (匿名)。User を書いたランタイム (JobRuntimes) に入っていないジョブは、このユーザーの権限で動く
            //スクリプトの Logger は標準出力に出す
            return await RunAsync(name, args, config["Jobs:UserId"] ?? string.Empty, new ScriptLogger(null));
        }

        /// <summary>
        /// 画面のボタンから起動されたジョブを動かす。そのジョブのランタイム (appsettings の JobRuntimes) の JobTriggerProcessType が InProcess ならこのプロセスの中で (プロセス内実行)、それ以外は別プロセスで動かす。
        /// input は画面の内容 (ジョブの args[0])、userId はボタンを押したユーザー。プロセス内実行では、スクリプトの Logger はアプリのログに出す。
        /// </summary>
        public static Task<string?> RunFromScreenAsync(string name, string input, string userId)
            => GetRuntime(name).JobTriggerProcessType == JobTriggerProcessType.InProcess
                ? RunAsync(name, [input], userId, new ScriptLogger(JobProcess.LoggerFactory?.CreateLogger(typeof(JobRunner)) ?? NullLogger.Instance))
                : JobProcess.RunInNewProcessAsync(name, input, userId);

        //ジョブを 1 つ動かす。userId は起動したユーザー。戻り値は成功したときの文言、失敗は例外
        static async Task<string?> RunAsync(string name, string[] args, string userId, ScriptLogger logger)
        {
            if (!File.Exists(Path.Combine(SystemConfig.Instance.DesignFileDirectory, "App.zip")))
            {
                throw new FileNotFoundException($"DesignFileDirectory に App.zip がありません: {SystemConfig.Instance.DesignFileDirectory}");
            }

            //ランタイム (appsettings の JobRuntimes) に User (ログイン ID) が書いてあるジョブは、起動したユーザーによらず、そのユーザーの権限で読み書きする
            var user = GetRuntime(name).User;
            await using var data = new DataService(string.IsNullOrEmpty(user) ? userId : await FindUserIdAsync(user));
            var job = CreateJobs().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (job != null) return await job.RunAsync(data, args);

            //IJob に無い名前はデザイナで作った Job。読み書きは IJob と同じ data.ModuleDataIO を通る。スクリプトの Resources はデザインのリソースを読み、Invoker は起動したユーザー。
            //FileService (添付ファイルの読み書き) の置き場所は画面のアップロード・ダウンロードと同じ
            var result = await JobExecutor.RunAsync(new JobExecutionContext
            {
                ModuleDataIO = data.ModuleDataIO,
                DesignData = data.Design.DesignData,
                GetResource = AppDesign.Store.GetResource,
                InvokerUserId = userId,
                FileStorage = data.TemporaryFileManager,
            }, name, args, logger);
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            return result.Message;
        }

        //ログイン ID (ログイン画面で入力するもの) からユーザーの Id を引く。見つからない・停止中のユーザーならジョブは失敗。
        //ユーザーモジュールに LoginAccountContractField が無いアプリでは、書いた値をそのままユーザーの Id として使う
        static async Task<string> FindUserIdAsync(string loginName)
        {
            await using var data = new DataService(string.Empty);
            var accounts = LoginAccountStore.Create(data.Design.DesignData, data.DbAccess);
            if (accounts == null) return loginName;
            return (await accounts.FindByLoginNameAsync(loginName))?.UserId
                ?? throw new InvalidOperationException($"JobRuntimes の User '{loginName}' が見つかりません");
        }

        //そのジョブを動かすランタイム (appsettings の JobRuntimes で Jobs にそのジョブを書いたもの。どこにも書いていなければ既定)
        static JobRuntime GetRuntime(string name)
            => SystemConfig.Instance.JobRuntimes.FirstOrDefault(e => e.Jobs.Contains(name, StringComparer.OrdinalIgnoreCase)) ?? new();

        //デザインを読めないとき (まだ配置されていない・差し替え中) は空 (ジョブの一覧を出すところでは落とさない)
        static IReadOnlyList<string> GetDesignerJobNames()
        {
            try
            {
                return JobExecutor.GetJobNames(AppDesign.Store.GetDesignData());
            }
            catch
            {
                return [];
            }
        }

        //スクリプトの Logger の出し先。logger が null なら標準出力 (ジョブモード。Warn / Error は行の先頭に warn: / error: を付ける)
        sealed class ScriptLogger(ILogger? logger) : Codeer.LowCode.Blazor.RequestInterfaces.ILogger
        {
            public Task Log(string message) => Write(LogLevel.Information, message, message);
            public Task Warn(string message) => Write(LogLevel.Warning, message, $"warn: {message}");
            public Task Error(string message) => Write(LogLevel.Error, message, $"error: {message}");

            Task Write(LogLevel level, string message, string consoleLine)
            {
                if (logger == null) Console.WriteLine(consoleLine);
                else logger.Log(level, "{Message}", message);
                return Task.CompletedTask;
            }
        }

        static IEnumerable<IJob> CreateJobs()
            => typeof(IJob).Assembly.GetTypes()
                .Where(e => typeof(IJob).IsAssignableFrom(e) && e.IsClass && !e.IsAbstract && e.GetConstructor(Type.EmptyTypes) != null)
                .Select(e => (IJob)Activator.CreateInstance(e)!);
    }
}
