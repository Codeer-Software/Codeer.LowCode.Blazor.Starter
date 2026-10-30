using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DataIO.Db;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;

namespace LowCodeApp.Server.Infrastructure
{
    public class CustomizedModuleDataIO : PasswordHashingModuleDataIO
    {
        //一括 INSERT を使う最小行数 (-1 で無効)
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, IDbAccessor dbAccess, ITemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
        }
    }
}
