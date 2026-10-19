using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.DbAccess;

namespace LowCodeApp.Jobs.Services
{
    /// <summary>
    /// ジョブ 1 回分の DB アクセス。ModuleDataIO の権限の判定は userId のユーザーで行われる (空なら匿名)。
    /// </summary>
    public class DataService : IAuthenticationContext, IAsyncDisposable
    {
        public DbAccessor DbAccess { get; }
        public TemporaryFileManager TemporaryFileManager { get; }
        public CustomizedModuleDataIO ModuleDataIO { get; }
        //このジョブが使うデザイン (作った時点の版に固定)
        public DesignData Design { get; }
        readonly string _userId;

        public DataService(string userId)
        {
            _userId = userId;
            Design = DesignerService.GetDesignData();
            DbAccess = new DbAccessor(SystemConfig.Instance.DataSources);
            TemporaryFileManager = new TemporaryFileManager(DbAccess, SystemConfig.Instance.TemporaryFileTableInfo, FileStorageTable.Storages);
            ModuleDataIO = new CustomizedModuleDataIO(Design, this, DbAccess, TemporaryFileManager);
        }

        public Task<string> GetCurrentUserIdAsync()
            => Task.FromResult(_userId);

        public async ValueTask DisposeAsync()
            => await DbAccess.DisposeAsync();
    }
}
