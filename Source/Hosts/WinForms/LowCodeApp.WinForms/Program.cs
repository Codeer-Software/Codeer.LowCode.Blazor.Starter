using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.License;
using Codeer.LowCode.Blazor.Extras.Server.Excel;
using Excel.Report.PDF;
using Microsoft.Extensions.Configuration;
using PdfSharp.Fonts;
using LowCodeApp.Client.Shared.Samples;
using LowCodeApp.WinForms.Services;
using LowCodeApp.WinForms.Extras;

namespace LowCodeApp.WinForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

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
            //appsettings → SystemConfig (セクション名 = プロパティ名。設定を足すときは SystemConfig にプロパティを足す。使うものだけ書けばよい)
            config.Bind(SystemConfig.Instance);
            //接続文字列は ConnectionStrings:<Name> に置く (Azure Blob とファイル保存先の簡易形式は設定に直接書いてもよい)
            foreach (var dataSource in SystemConfig.Instance.DataSources) dataSource.ConnectionString = config.GetConnectionString(dataSource.Name) ?? string.Empty;
            foreach (var storage in SystemConfig.Instance.AzureBlobStorages) if (string.IsNullOrEmpty(storage.ConnectionString) && string.IsNullOrEmpty(storage.BlobServiceUri)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            foreach (var storage in SystemConfig.Instance.FileStorages) if (string.IsNullOrEmpty(storage.ConnectionString)) storage.ConnectionString = config.GetConnectionString(storage.Name) ?? string.Empty;
            //SQL のデバッグログ (実行した SQL とパラメータをデバッグ出力へ)。appsettings.Development.json で有効化
            if (config.GetSection("SqlLog").Get<bool>()) DbAccessor.SqlLog = s => System.Diagnostics.Debug.WriteLine(s);

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
