using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;

namespace LowCodeApp.Server.Services
{
    /// <summary>
    /// 監査ログの出力先 (appsettings の AuditLog.Database / AuditLog.File)。独自の出力先 (SIEM 直送など) は IAuditSink を実装してここに足す。
    /// </summary>
    public static class AuditSinkTable
    {
        public static List<IAuditSink> Create()
        {
            var config = SystemConfig.Instance;
            var list = new List<IAuditSink>();
            if (!string.IsNullOrEmpty(config.AuditLog.Database.DataSourceName))
                list.Add(new DatabaseAuditSink(config.AuditLog.Database, () => new DbAccessor(config.DataSources)));
            if (!string.IsNullOrEmpty(config.AuditLog.File.Directory))
                list.Add(new FileAuditSink(config.AuditLog.File));
            return list;
        }
    }
}
