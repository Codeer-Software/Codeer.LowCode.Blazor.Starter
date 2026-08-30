using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Services;

namespace LowCodeApp.WinForms.Services
{
    public class CustomizedModuleDataIO : ModuleDataIO
    {
        //一括INSERT (multi-row INSERT) の有効化。純Addのみの大量Submit (一括取込) がこの行数以上のとき束ねて挿入される。
        //-1 (コア既定) で無効
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        readonly DesignData _designData;

        public DbAccessor DbAccess { get; }
        public TemporaryFileManager TemporaryFileManager { get; }

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, DbAccessor dbAccess, TemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            _designData = designData;
            DbAccess = dbAccess;
            TemporaryFileManager = temporaryFileManager;
        }

        protected override async Task<string> AddAsync(Guid transactionId, Guid moduleSubmitId, ModuleData data)
        {
            var moduleDesign = _designData.Modules.Find(data.Name);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
            return await base.AddAsync(transactionId, moduleSubmitId, data);
        }

        //一括INSERT (大量取込) は行ごとの AddAsync を通らないため、同じ加工をこちらでも行う
        protected override async Task BulkAddAsync(Guid transactionId, List<ModuleData> datas)
        {
            var moduleDesign = _designData.Modules.Find(datas.FirstOrDefault()?.Name ?? string.Empty);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            foreach (var data in datas) PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
            await base.BulkAddAsync(transactionId, datas);
        }

        protected async override Task UpdateAsync(Guid transactionId, Guid moduleSubmitId, ModuleData data)
        {
            var moduleDesign = _designData.Modules.Find(data.Name);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
            await base.UpdateAsync(transactionId, moduleSubmitId, data);
        }

        //メール送信履歴などシステムの記録を、操作ユーザーの書き込み権限に依存せず追加する内部経路。戻り値は採番された Id
        internal async Task<string> AddSystemRecordAsync(ModuleData data)
            => await AddAsync(Guid.NewGuid(), Guid.NewGuid(), data);
    }
}
