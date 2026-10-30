using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DataIO.Db;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;
using LowCodeApp.Server.Extras;

namespace LowCodeApp.Server.Infrastructure
{
    /// <summary>
    /// このアプリの ModuleDataIO。保存の経路に載せる Extras の機能 (インターセプタ) をここで登録する。
    /// 基底の PasswordHashingModuleDataIO が、保存時にパスワードの平文をハッシュにする。
    /// 保存・読み出しそのものを変えたいときは ModuleDataIO の virtual (AddAsync / UpdateAsync / DeleteAsync 等) を override する。
    /// </summary>
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括 INSERT を使う最小行数 (-1 で無効)
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, IDbAccessor dbAccess, ITemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            //監査ログ (他のインターセプタより先に登録する)
            AddInterceptor(new AuditIOInterceptor(designData, SystemConfig.Instance.AuditLog.Database));
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
            //意味検索 (SemanticSearchField)。最後に登録する
            AddInterceptor(SemanticSearchTable.Service.Interceptor);
        }
    }
}
