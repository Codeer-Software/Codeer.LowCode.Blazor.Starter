using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DataIO.Db;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;

namespace LowCodeApp.Server.Infrastructure
{
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括INSERT (multi-row INSERT) の有効化。純Addのみの大量Submit (一括取込) がこの行数以上のとき束ねて挿入される。
        //-1 (コア既定) で無効
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        readonly DesignData _designData;

        public DesignData DesignData => _designData;

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, IDbAccessor dbAccess, ITemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            _designData = designData;
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
        }

        //メール送信履歴・承認データなどシステムの記録を、操作ユーザーの書き込み権限に依存せず追加する内部経路。
        //クライアントから直接は呼ばれない (サーバー内部の記録専用)。戻り値は採番された Id
        internal async Task<string> AddSystemRecordAsync(ModuleData data)
            => await AddAsync(Guid.NewGuid(), Guid.NewGuid(), data);

        //承認フローなどが使う内部経路 (操作ユーザーの書き込み権限を通さない)。data に含まれるフィールドだけが更新される
        internal async Task UpdateSystemRecordAsync(ModuleData data)
            => await UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), data);
    }
}
