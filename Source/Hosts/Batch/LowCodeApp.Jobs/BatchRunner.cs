using System.Text;
using Codeer.LowCode.Bindings.ApexCharts;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.AI.Embedding;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.SystemSettings;
using Excel.Report.PDF;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.Jobs.AI;
using LowCodeApp.Jobs.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfSharp.Fonts;

namespace LowCodeApp.Jobs
{
    /// <summary>
    /// 設定を読み込んでジョブを 1 つ実行する。戻り値はプロセスの終了コード (成功 0 / 失敗 1 / ジョブが無い 2)。
    /// </summary>
    public static class BatchRunner
    {
        public const int Success = 0;
        public const int Failure = 1;
        public const int JobNotFound = 2;

        public static async Task<int> RunAsync(IConfiguration config, string jobName, string[] args)
        {
            var job = FindJob(jobName);
            if (job == null)
            {
                Console.Error.WriteLine($"ジョブ '{jobName}' がありません。ジョブ: {string.Join(", ", GetJobNames())}");
                return JobNotFound;
            }

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
            try
            {
                Initialize(config);
                SemanticSearchIndex.Service.LoggerFactory = loggerFactory;

                if (!File.Exists(Path.Combine(SystemConfig.Instance.DesignFileDirectory, "App.zip")))
                {
                    throw new FileNotFoundException($"DesignFileDirectory に App.zip がありません: {SystemConfig.Instance.DesignFileDirectory}");
                }

                //空なら匿名ユーザー。権限の条件はこのユーザーで判定される
                var userId = config["Batch:UserId"] ?? string.Empty;
                await using var data = new DataService(userId);
                await job.RunAsync(data, args);
                return Success;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return Failure;
            }
        }

        public static IReadOnlyList<string> GetJobNames()
            => CreateJobs().Select(e => e.Name).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();

        static IJob? FindJob(string name)
            => CreateJobs().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        static IEnumerable<IJob> CreateJobs()
            => typeof(IJob).Assembly.GetTypes()
                .Where(e => typeof(IJob).IsAssignableFrom(e) && e.IsClass && !e.IsAbstract && e.GetConstructor(Type.EmptyTypes) != null)
                .Select(e => (IJob)Activator.CreateInstance(e)!);

        static void Initialize(IConfiguration config)
        {
            //フィールド型をリフレクションで見つけられるよう、型を持つアセンブリを読み込んでおく
            typeof(CodeBehindSample).ToString();
            ApexChartsServerInitializer.Initialize();
            ExtrasServerInitializer.Initialize();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Codeer.LowCode.Blazor.Extras.ScriptObjects.Excel.ConvertPdf = ExcelConverter.ConvertToPdf;
            //メールは HTTP を介さず直接送る
            Codeer.LowCode.Blazor.Extras.Mail.MailTransport.Handler = new MailTransportHandler(config["Batch:UserId"] ?? string.Empty);

            var instance = SystemConfig.Instance;
            instance.DataSources = config.GetSection("DataSources").Get<DataSource[]>() ?? [];
            foreach (var dataSource in instance.DataSources) dataSource.ConnectionString = config.GetConnectionString(dataSource.Name) ?? string.Empty;
            //ファイル保存先の設定 (種類ごとのセクション。実体は Services/FileStorageTable が組み立てる)
            instance.FileSystemStorages = config.GetSection("FileSystemStorages").Get<FileSystemStorageSettings[]>() ?? [];
            instance.AzureBlobStorages = config.GetSection("AzureBlobStorages").Get<AzureBlobStorageSettings[]>() ?? [];
            instance.S3Storages = config.GetSection("S3Storages").Get<S3StorageSettings[]>() ?? [];
            instance.FileStorages = config.GetSection("FileStorages").Get<FileStorage[]>() ?? [];
            //Azure Blob の接続文字列は ConnectionStrings:<Name> にも置ける
            foreach (var storage in instance.AzureBlobStorages) if (string.IsNullOrEmpty(storage.ConnectionString) && string.IsNullOrEmpty(storage.BlobServiceUri)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            foreach (var storage in instance.FileStorages) if (string.IsNullOrEmpty(storage.ConnectionString)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            instance.TemporaryFileTableInfo = config.GetSection("TemporaryFileTableInfo").Get<TemporaryFileTableInfo[]>() ?? [];
            instance.DesignFileDirectory = config["DesignFileDirectory"] ?? string.Empty;
            instance.FontFileDirectory = config["FontFileDirectory"] ?? string.Empty;
            //メールのプロバイダ設定はそれぞれ独立したセクション (使うものだけ書けばよい)
            instance.Mail = config.GetSection("Mail").Get<MailConfig>() ?? new();
            instance.Smtp = config.GetSection("Smtp").Get<SmtpSettings>() ?? new();
            instance.GraphApi = config.GetSection("GraphApi").Get<GraphApiSettings>() ?? new();
            instance.SendGrid = config.GetSection("SendGrid").Get<SendGridSettings>() ?? new();
            instance.Gmail = config.GetSection("Gmail").Get<GmailSettings>() ?? new();
            instance.AISettings = config.GetSection("AISettings").Get<AISettings>() ?? new();
            //意味検索 (SemanticSearchField) の埋め込みプロバイダの設定 (使うものだけ書けばよい)
            instance.SemanticSearch = config.GetSection("SemanticSearch").Get<SemanticSearchSettings>() ?? new();
            instance.AzureOpenAIEmbedding = config.GetSection("AzureOpenAIEmbedding").Get<AzureOpenAIEmbeddingSettings>() ?? new();

            //実行した SQL とパラメータを標準出力に出す
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = Console.WriteLine;

            GlobalFontSettings.FontResolver = new CustomFontResolver(instance.FontFileDirectory);
        }
    }
}
