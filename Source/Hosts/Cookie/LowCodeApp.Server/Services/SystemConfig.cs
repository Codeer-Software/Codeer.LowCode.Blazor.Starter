using Codeer.LowCode.Blazor.SystemSettings;
using LowCodeApp.Client.Shared.Services;
using LowCodeApp.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.AI.Embedding;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;

namespace LowCodeApp.Server.Services
{
    public class SystemConfig
    {
        public static SystemConfig Instance { get; set; } = new();

        public bool CanScriptDebug { get; set; }
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
        //Mail = 製品 (共通層) が読む設定。プロバイダごとの設定 (Smtp / Gmail 等) は個別のセクションとして持つ
        public MailConfig Mail { get; set; } = new();
        public SmtpSettings Smtp { get; set; } = new();
        public GraphApiSettings GraphApi { get; set; } = new();
        public SendGridSettings SendGrid { get; set; } = new();
        public GmailSettings Gmail { get; set; } = new();
        public AISettings AISettings { get; set; } = new();
        //AIChatField のサーバー側設定
        public AIChatSettings AIChat { get; set; } = new();
        //SemanticSearchField (意味検索) の埋め込みプロバイダの呼び名 (AI/EmbeddingProviderTable の鍵)
        public SemanticSearchSettings SemanticSearch { get; set; } = new();
        public AzureOpenAIEmbeddingSettings AzureOpenAIEmbedding { get; set; } = new();
        //ID/パスワードのログイン。外部 IdP 専用にするなら false
        public bool AllowPasswordLogin { get; set; } = true;
        //外部 IdP (使うものだけ書けばよい)。実体は Services/ExternalLoginTable が組み立てる
        public EntraLoginSettings EntraLogin { get; set; } = new();
        public GoogleLoginSettings GoogleLogin { get; set; } = new();
        public CognitoLoginSettings CognitoLogin { get; set; } = new();
        public OidcLoginSettings[] OidcLogins { get; set; } = [];
        //MAUI アプリがシステムブラウザで外部 IdP にログインした後に戻る URL。MAUI 側の appsettings (Server:LoginCallbackUrl) と一致させる
        public string MobileLoginCallbackUrl { get; set; } = string.Empty;
        //認証アプリ (TOTP) の二要素認証の表示用 Issuer
        public TotpLoginSettings TotpLogin { get; set; } = new();
        //メールのワンタイムコードによる二要素認証 (メールの体裁と有効期限)
        public EmailOtpLoginSettings EmailOtpLogin { get; set; } = new();
        //監査ログ (Extras.Server の AuditLog)。出力先の実体は Services/AuditSinkTable
        public AuditLogSettings AuditLog { get; set; } = new();
        public SystemConfigForFront ForFront() => new SystemConfigForFront { CanScriptDebug = CanScriptDebug, UseHotReload = UseHotReload };
    }
}
