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
    /// 基底の PasswordHashingModuleDataIO が、行を書く直前にパスワードの平文をハッシュにする (PasswordHashField / LoginAccountContractField)。
    /// 保存・読み出しそのものを変えたいときは ModuleDataIO の virtual (AddAsync / UpdateAsync / DeleteAsync 等) をここで override する (base を呼ぶ)。
    /// </summary>
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括INSERT (multi-row INSERT) の有効化。純Addのみの大量Submit (一括取込) がこの行数以上のとき束ねて挿入される。
        //-1 (コア既定) で無効
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, IDbAccessor dbAccess, ITemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            //監査ログ: 保存の対象を記録し、監査ログのテーブルへの保存を拒否する。他のインターセプタより先に登録する
            AddInterceptor(new AuditIOInterceptor(designData, SystemConfig.Instance.AuditLog.Database));
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
            //意味検索 (SemanticSearchField): 保存した行から索引 (文章と埋め込み) を作る・検索欄の文章を埋め込みにしてから検索する。他のインターセプタより後 (最後) に登録する
            AddInterceptor(SemanticSearchTable.Service.Interceptor);
        }
    }
}
