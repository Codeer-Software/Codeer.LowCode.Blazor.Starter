using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;
using Codeer.LowCode.Blazor.Extras.Server.Auth;

namespace LowCodeApp.Wpf.Services
{
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括INSERT (multi-row INSERT) の有効化。純Addのみの大量Submit (一括取込) がこの行数以上のとき束ねて挿入される。
        //-1 (コア既定) で無効
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

        //メール送信履歴などを操作ユーザーの書き込み権限を通さず追加する内部経路。戻り値は採番された Id
        internal async Task<string> AddSystemRecordAsync(ModuleData data)
            => await AddAsync(Guid.NewGuid(), Guid.NewGuid(), data);
    }
}
