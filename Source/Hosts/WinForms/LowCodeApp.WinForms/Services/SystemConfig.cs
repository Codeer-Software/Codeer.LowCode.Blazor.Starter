using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.SystemSettings;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;

namespace LowCodeApp.WinForms.Services
{
    public class SystemConfig
    {
        public static SystemConfig Instance { get; set; } = new();

        public bool UseHotReload { get; set; }
        public DataSource[] DataSources { get; set; } = [];
        //ファイル保存先 (使うものだけ書けばよい)。実体は Services/FileStorageTable が組み立てる
        public FileSystemStorageSettings[] FileSystemStorages { get; set; } = [];
        public AzureBlobStorageSettings[] AzureBlobStorages { get; set; } = [];
        public S3StorageSettings[] S3Storages { get; set; } = [];
        //簡易形式 (種別と設定を 1 クラスに持つ。FileSystem / Azure Blob 接続文字列)
        public FileStorage[] FileStorages { get; set; } = [];
        public TemporaryFileTableInfo[] TemporaryFileTableInfo { get; set; } = [];
        public string DesignFileDirectory { get; set; } = string.Empty;
        public string FontFileDirectory { get; set; } = string.Empty;
        //メールの共通設定。プロバイダごとの設定 (Gmail 等) は個別のセクション
        public MailConfig Mail { get; set; } = new();
        public GmailSettings Gmail { get; set; } = new();
        public AISettings AISettings { get; set; } = new();
    }
}
