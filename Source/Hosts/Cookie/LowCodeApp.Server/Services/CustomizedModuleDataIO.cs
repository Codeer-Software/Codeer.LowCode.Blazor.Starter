using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.DataIO.Db;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using Codeer.LowCode.Blazor.Extras.Server.EditHistory;
using Codeer.LowCode.Blazor.Extras.Services;
using LowCodeApp.Server.AI;

namespace LowCodeApp.Server.Services
{
    public class CustomizedModuleDataIO : ModuleDataIO
    {
        //一括INSERT (multi-row INSERT) の有効化。純Addのみの大量Submit (一括取込) がこの行数以上のとき束ねて挿入される。
        //-1 (コア既定) で無効
        static CustomizedModuleDataIO() => BulkAddThreshold = 100;

        readonly DesignData _designData;

        public CustomizedModuleDataIO(DesignData designData, IAuthenticationContext authenticationContext, IDbAccessor dbAccess, ITemporaryFileManager temporaryFileManager)
            : base(designData, authenticationContext, dbAccess, temporaryFileManager)
        {
            _designData = designData;
            //監査ログ: 保存の対象を記録し、監査ログのテーブルへの保存を拒否する。他のインターセプタより先に登録する
            AddInterceptor(new AuditIOInterceptor(designData, SystemConfig.Instance.AuditLog.Database));
            //編集履歴 (EditHistoryField)
            AddInterceptor(new EditHistoryRecorder(designData));
            //意味検索 (SemanticSearchField): 検索欄の文章を埋め込みにしてから検索する
            AddInterceptor(SemanticSearchIndex.Service.ConditionInterceptor);
        }


        protected override async Task<string> AddAsync(Guid transactionId, Guid moduleSubmitId, ModuleData data)
        {
            var moduleDesign = _designData.Modules.Find(data.Name);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
            //SemanticSearchField の文章に埋め込みベクトルを付ける (新規で文章が無ければここで組み立てる = 一括取込)
            await SemanticSearchIndex.Service.ApplyAsync(data, isNewData: true);
            return await base.AddAsync(transactionId, moduleSubmitId, data);
        }

        //一括INSERT (大量取込) は行ごとの AddAsync を通らないため、同じ加工をこちらでも行う
        protected override async Task BulkAddAsync(Guid transactionId, List<ModuleData> datas)
        {
            var moduleDesign = _designData.Modules.Find(datas.FirstOrDefault()?.Name ?? string.Empty);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            foreach (var data in datas)
            {
                PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
                await SemanticSearchIndex.Service.ApplyAsync(data, isNewData: true);
            }
            await base.BulkAddAsync(transactionId, datas);
        }

        protected async override Task UpdateAsync(Guid transactionId, Guid moduleSubmitId, ModuleData data)
        {
            var moduleDesign = _designData.Modules.Find(data.Name);
            if (moduleDesign == null) throw LowCodeException.Create("invalid design");

            PasswordHashHelper.ApplyPasswordHash(moduleDesign, data);
            //文章が送られてきたとき (対象フィールドが変わったとき・再索引) だけ埋め込みを付け直す
            await SemanticSearchIndex.Service.ApplyAsync(data, isNewData: false);
            await base.UpdateAsync(transactionId, moduleSubmitId, data);
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
