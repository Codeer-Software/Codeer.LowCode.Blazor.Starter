using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;
using Codeer.LowCode.Blazor.Extras.Server.Auth;

namespace LowCodeApp.WinForms.Services
{
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括 INSERT を使う最小行数 (-1 で無効)
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        public DbAccessor DbAccess { get; }
        public TemporaryFileManager TemporaryFileManager { get; }

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, DbAccessor dbAccess, TemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
            DbAccess = dbAccess;
            TemporaryFileManager = temporaryFileManager;
        }

        //操作ユーザーの書き込み権限を通さずに追加する内部経路 (メール送信履歴など)。戻り値は採番された Id
        internal async Task<string> AddSystemRecordAsync(ModuleData data)
            => await AddAsync(Guid.NewGuid(), Guid.NewGuid(), data);
    }
}
