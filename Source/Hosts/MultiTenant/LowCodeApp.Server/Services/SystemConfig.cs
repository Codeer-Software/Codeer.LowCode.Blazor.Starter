using Codeer.LowCode.Blazor.SystemSettings;
using LowCodeApp.Client.Shared.Services;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;

namespace LowCodeApp.Server.Services
{
    public class SystemConfig
    {
        public static SystemConfig Instance { get; set; } = new();

        public string DefaultConnectionString { get; set; } = string.Empty;
        public bool CanScriptDebug { get; set; }
        public bool UseHotReload { get; set; }
        public DataSource[] DataSources { get; set; } = [];
        //ファイル保存先 = 種類ごとの設定 (使うものだけ書けばよい)。実体 (IFileStorage) は Services/FileStorageTable が組み立てる (メールの MailSenderTable と同じ考え方)
        public FileSystemStorageSettings[] FileSystemStorages { get; set; } = [];
        public AzureBlobStorageSettings[] AzureBlobStorages { get; set; } = [];
        public S3StorageSettings[] S3Storages { get; set; } = [];
        //簡易形式 (種別と設定を 1 クラスに持つ。FileSystem / Azure Blob 接続文字列)
        public FileStorage[] FileStorages { get; set; } = [];
        public TemporaryFileTableInfo[] TemporaryFileTableInfo { get; set; } = [];
        public string DesignFileDirectory { get; set; } = string.Empty;
        public string FontFileDirectory { get; set; } = string.Empty;
        //Mail = 製品 (共通層) が読む設定。プロバイダごとの設定 (Smtp / Gmail 等) は個別のセクションとして持つ
        public MailConfig Mail { get; set; } = new();
        public SmtpSettings Smtp { get; set; } = new();
        public GraphApiSettings GraphApi { get; set; } = new();
        public SendGridSettings SendGrid { get; set; } = new();
        public GmailSettings Gmail { get; set; } = new();
        public AISettings AISettings { get; set; } = new();
        public SystemConfigForFront ForFront() => new SystemConfigForFront { CanScriptDebug = CanScriptDebug, UseHotReload = UseHotReload };
    }
}
