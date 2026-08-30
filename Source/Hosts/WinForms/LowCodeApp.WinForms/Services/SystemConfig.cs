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
        //保存先は種類ごとの設定 (FileSystemStorages / AzureBlobStorages / S3Storages) を FileStorageTable が IFileStorage に組み立てる
        public List<IFileStorage> FileStorages { get; set; } = [];
        public TemporaryFileTableInfo[] TemporaryFileTableInfo { get; set; } = [];
        public string DesignFileDirectory { get; set; } = string.Empty;
        public string FontFileDirectory { get; set; } = string.Empty;
        //Mail = 製品 (共通層) が読む設定。プロバイダごとの設定 (Gmail 等) は個別のセクションとして持つ
        public MailConfig Mail { get; set; } = new();
        public GmailSettings Gmail { get; set; } = new();
        public AISettings AISettings { get; set; } = new();
    }
}
