using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace LowCodeApp.Server.Infrastructure
{
    /// <summary>
    /// DB の例外の分類。利用者に意味のある文言で返したいものだけ (ExceptionHandlerUtils が使う)。
    /// </summary>
    public static class DbErrors
    {
        //一意制約 (UNIQUE / PRIMARY KEY) 違反
        public static bool IsUniqueViolation(Exception ex) => ex switch
        {
            PostgresException e => e.SqlState == PostgresErrorCodes.UniqueViolation,
            SqlException e => e.Number is 2627 or 2601,
            SqliteException e => e.SqliteExtendedErrorCode is 2067 or 1555,
            MySqlException e => e.ErrorCode == MySqlErrorCode.DuplicateKeyEntry,
            OracleException e => e.Number == 1,
            _ => ex.InnerException != null && IsUniqueViolation(ex.InnerException),
        };
    }
}
