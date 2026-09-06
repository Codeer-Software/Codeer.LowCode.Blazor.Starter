using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Dapper;
using LowCodeApp.Server.Services;

namespace LowCodeApp.Server
{
    /// <summary>
    /// 外部 IdP (Entra ID / Google / AWS Cognito / OIDC) が本人確認したユーザーを、ユーザーテーブルの 1 行に解決する。
    /// これはアプリのプロビジョニング方針なので、パッケージではなくここに置く:
    ///
    /// - 事前登録制 (この実装): IdP のユーザー名 (Entra = UPN、Google / Cognito = メール) がユーザーテーブルのユーザー名列
    ///   (PasswordCheckUserTableInfo) と一致する行だけ許可する。行は作らない
    /// - 自動作成: 行が無ければ INSERT する (identity.LoginName / DisplayName / Email が使える)
    /// - Subject での紐付け: identity.Provider + identity.Subject を紐付けテーブルに持てば、メールが変わってもユーザーを維持できる
    ///
    /// null を返すとサインインしない (ログイン画面に user_not_registered で戻る)
    /// </summary>
    public class ExternalLoginUserResolver : IExternalLoginUserResolver
    {
        readonly DataService _dataService;

        public ExternalLoginUserResolver(DataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<ExternalLoginUser?> ResolveAsync(ExternalLoginIdentity identity)
        {
            var tableInfo = SystemConfig.Instance.PasswordCheckUserTableInfo;
            var designData = DesignerService.GetDesignData();
            var dataSourceName = designData.Modules.Find(designData.AppSettings.CurrentUserModuleDesignName)?.DataSourceName ?? string.Empty;
            var conn = _dataService.DbAccess.GetConnection(dataSourceName);

            var user = (await conn.QueryAsync<PasswordCheckUser>(
                $"SELECT {tableInfo.IdColumn} AS Id, {tableInfo.UserNameColumn} AS UserName FROM {tableInfo.TableName} WHERE {tableInfo.UserNameColumn} = @UserName",
                new { UserName = identity.LoginName })).FirstOrDefault();

            return user == null ? null : new ExternalLoginUser(user.Id, user.UserName);
        }
    }
}
