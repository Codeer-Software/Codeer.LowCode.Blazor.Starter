using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.Jobs;
using Codeer.LowCode.Blazor.Jobs;
using LowCodeApp.Server.Extras;
using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Jobs
{
    /// <summary>
    /// このアプリのジョブの一覧と実行。dotnet LowCodeApp.Server.dll job &lt;ジョブ名&gt; [引数...] で呼ばれる (別プロセス)。
    /// 画面のボタンから起動するジョブは、appsettings の Jobs:RunIn で "Server" にすると Web サーバーのプロセスの中で動く (サーバー内実行)。
    /// ジョブは 2 種類: このアセンブリの IJob (C#) と、デザインの Job スクリプト (Jobs/名前.job.cs)。同じ名前なら IJob を動かす。
    /// </summary>
    public static class JobRunner
    {
        /// <summary>動かせるジョブの名前。デザインが差し替わると変わるので、参照のたびに今の一覧を返す。</summary>
        public static IReadOnlyCollection<string> Names { get; } = new CurrentNames();

        static List<string> GetNames()
            => CreateJobs().Select(e => e.Name).Concat(GetScriptNames())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        //デザインを読めないとき (App.zip が無い・壊れている・差し替え中) は IJob だけにする (一覧を出すところでは落とさない)
        static IReadOnlyList<string> GetScriptNames()
        {
            if (!HasDesign) return [];
            try
            {
                return JobScriptRunner.GetJobNames(AppDesign.Store.GetDesignData());
            }
            catch
            {
                return [];
            }
        }

        static bool HasDesign => File.Exists(Path.Combine(SystemConfig.Instance.DesignFileDirectory, "App.zip"));

        /// <summary>ジョブを 1 つ動かす (別プロセス)。name は Names のどれか、args はジョブ名の後ろの引数。戻り値は成功したときの文言 (画面に出る)、失敗は例外。</summary>
        public static async Task<string?> RunAsync(IConfiguration config, string name, string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
            //意味検索の警告ログ (埋め込みの失敗など) を標準出力に出す
            SemanticSearchTable.Service.LoggerFactory = loggerFactory;
            //実行した SQL とパラメータを標準出力に出す
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = Console.WriteLine;

            //権限はこのユーザーで判定される (空なら匿名)。画面のボタンから起動したときは押したユーザー。スクリプトの Logger は標準出力に出る
            return await RunAsync(name, args, config["Jobs:UserId"] ?? string.Empty, new ConsoleLogger());
        }

        /// <summary>そのジョブをサーバー内実行にするか (appsettings の Jobs:RunIn が "Server")。</summary>
        public static bool RunsInServer(string name)
            => SystemConfig.Instance.Jobs.RunIn.Any(e => string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Value, "Server", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// ジョブを 1 つ動かす (サーバー内実行)。画面のボタンを押したユーザーの権限で動く。戻り値と失敗の扱いは別プロセスと同じ。
        /// スクリプトの Logger はアプリのログに出る。
        /// </summary>
        public static Task<string?> RunInServerAsync(string name, string[] args, string userId)
            => RunAsync(name, args, userId, new AppLogger(JobProcess.LoggerFactory?.CreateLogger(typeof(JobRunner).FullName!)));

        static async Task<string?> RunAsync(string name, string[] args, string userId, Codeer.LowCode.Blazor.RequestInterfaces.ILogger scriptLogger)
        {
            if (!HasDesign)
            {
                throw new FileNotFoundException($"DesignFileDirectory に App.zip がありません: {SystemConfig.Instance.DesignFileDirectory}");
            }

            await using var data = new DataService(userId);
            var job = CreateJobs().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (job != null) return await job.RunAsync(data, args);

            //デザインの Job スクリプト。読み書きは IJob と同じ data.ModuleDataIO を通る
            var design = data.Design.DesignData;
            var scriptName = JobScriptRunner.GetJobNames(design).First(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase));
            var result = await JobScriptRunner.RunAsync(data.ModuleDataIO, design, scriptName, JobArguments.Parse(args), scriptLogger);
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            return result.Message;
        }

        static IEnumerable<IJob> CreateJobs()
            => typeof(IJob).Assembly.GetTypes()
                .Where(e => typeof(IJob).IsAssignableFrom(e) && e.IsClass && !e.IsAbstract && e.GetConstructor(Type.EmptyTypes) != null)
                .Select(e => (IJob)Activator.CreateInstance(e)!);

        class CurrentNames : IReadOnlyCollection<string>
        {
            public int Count => GetNames().Count;
            public IEnumerator<string> GetEnumerator() => GetNames().GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        class AppLogger(ILogger? logger) : Codeer.LowCode.Blazor.RequestInterfaces.ILogger
        {
            public Task Log(string message) => Write(LogLevel.Information, message);
            public Task Warn(string message) => Write(LogLevel.Warning, message);
            public Task Error(string message) => Write(LogLevel.Error, message);

            Task Write(LogLevel level, string message)
            {
                logger?.Log(level, "{Message}", message);
                return Task.CompletedTask;
            }
        }

        class ConsoleLogger : Codeer.LowCode.Blazor.RequestInterfaces.ILogger
        {
            public Task Log(string message) => Write(message);
            public Task Warn(string message) => Write($"warn: {message}");
            public Task Error(string message) => Write($"error: {message}");

            static Task Write(string message)
            {
                Console.WriteLine(message);
                return Task.CompletedTask;
            }
        }
    }
}
