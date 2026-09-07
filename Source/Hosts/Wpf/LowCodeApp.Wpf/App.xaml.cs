using System.IO;
using System.Windows;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Excel.Report.PDF;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.License;
using Codeer.LowCode.Blazor.SystemSettings;
using Microsoft.Extensions.Configuration;
using PdfSharp.Fonts;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.Wpf.Services;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;

namespace LowCodeApp.Wpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //load dll.
            typeof(CodeBehindSample).ToString();

            Codeer.LowCode.Blazor.Extras.ScriptObjects.Excel.ConvertPdf = ExcelConverter.ConvertToPdf;
            //メールは HTTP を介さず直接送る (MailField / BulkMailField / プレビュー)
            Codeer.LowCode.Blazor.Extras.Mail.MailTransport.Handler = new MailTransportHandler();

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();
            LicenseManager.DomainLicense = config.GetSection("DomainLicense").Get<string>() ?? string.Empty;
            LicenseManager.IsAutoUpdate = config.GetSection("IsLicenseAutoUpdate").Get<bool>();
            SystemConfig.Instance.UseHotReload = config.GetSection("UseHotReload").Get<bool>();
            SystemConfig.Instance.DataSources = config.GetSection("DataSources").Get<DataSource[]>() ?? new DataSource[0];
            //ファイル保存先の設定 (種類ごとのセクション。実体は Services/FileStorageTable が組み立てる)
            SystemConfig.Instance.FileSystemStorages = config.GetSection("FileSystemStorages").Get<FileSystemStorageSettings[]>() ?? [];
            SystemConfig.Instance.AzureBlobStorages = config.GetSection("AzureBlobStorages").Get<AzureBlobStorageSettings[]>() ?? [];
            SystemConfig.Instance.S3Storages = config.GetSection("S3Storages").Get<S3StorageSettings[]>() ?? [];
            SystemConfig.Instance.FileStorages = config.GetSection("FileStorages").Get<FileStorage[]>() ?? [];
            //Azure Blob の接続文字列は ConnectionStrings:<Name> にも置ける
            foreach (var storage in SystemConfig.Instance.AzureBlobStorages) if (string.IsNullOrEmpty(storage.ConnectionString) && string.IsNullOrEmpty(storage.BlobServiceUri)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            foreach (var storage in SystemConfig.Instance.FileStorages) if (string.IsNullOrEmpty(storage.ConnectionString)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            SystemConfig.Instance.Mail = config.GetSection("Mail").Get<MailConfig>() ?? new();
            SystemConfig.Instance.Gmail = config.GetSection("Gmail").Get<GmailSettings>() ?? new();
            SystemConfig.Instance.AISettings = config.GetSection("AISettings").Get<AISettings>() ?? new();
            SystemConfig.Instance.TemporaryFileTableInfo = config.GetSection("TemporaryFileTableInfo").Get<TemporaryFileTableInfo[]>() ?? new TemporaryFileTableInfo[0];
            SystemConfig.Instance.DesignFileDirectory = config["DesignFileDirectory"] ?? string.Empty;
            SystemConfig.Instance.FontFileDirectory = config["FontFileDirectory"] ?? string.Empty;
            //SQL debug log: dump executed SQL and parameters to the debug output (enable via appsettings.Development.json)
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = s => System.Diagnostics.Debug.WriteLine(s);

            foreach (var dataSource in SystemConfig.Instance.DataSources)
            {
                dataSource.ConnectionString = config.GetConnectionString(dataSource.Name) ?? string.Empty;
            }

            GlobalFontSettings.FontResolver = new CustomFontResolver(SystemConfig.Instance.FontFileDirectory);

            using (var httpClient = new HttpClient(new WinHttpHandler { WindowsProxyUsePolicy = WindowsProxyUsePolicy.UseWinInetProxy }))
            {
                var thread = new Thread(() => LicenseManager.CheckClientServerLicense(httpClient).Wait());
                thread.Start();
                thread.Join();
            }

            base.OnStartup(e);
        }
    }
}
