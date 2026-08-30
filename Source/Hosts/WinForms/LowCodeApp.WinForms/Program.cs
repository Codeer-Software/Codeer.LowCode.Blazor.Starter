using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.License;
using Codeer.LowCode.Blazor.SystemSettings;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Excel.Report.PDF;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Microsoft.Extensions.Configuration;
using PdfSharp.Fonts;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.WinForms.Services;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;

namespace LowCodeApp.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

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
            //ファイル保存先は種類ごとのセクションを FileStorageTable が読む (FileSystemStorages / AzureBlobStorages / S3Storages)
            SystemConfig.Instance.FileStorages = FileStorageTable.Create(config);
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

            Application.Run(new MainForm());
        }
    }
}
