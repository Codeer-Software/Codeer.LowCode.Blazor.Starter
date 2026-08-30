using Codeer.LowCode.Blazor.DataIO;
using Codeer.LowCode.Blazor.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using LowCodeApp.Server.Data;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;
using Codeer.LowCode.Blazor.DbAccess;

namespace LowCodeApp.Server.Services
{
    public class DataService : IAuthenticationContext, IAsyncDisposable
    {
        public DbAccessor DbAccess { get; private set; } = null!;
        public TemporaryFileManager TemporaryFileManager { get; private set; } = null!;
        public CustomizedModuleDataIO ModuleDataIO { get; private set; } = null!;
        readonly ApplicationDbContext _context;
        readonly IHttpContextAccessor _httpContextAccessor;
        public string TenantKey { get; private set; } = string.Empty;
        public MultiTenantUserInfo UserInfo { get; private set; } = new();

        public DataService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async ValueTask DisposeAsync()
        {
            if (DbAccess == null) return;
            await DbAccess.DisposeAsync();
        }

        public async Task InitializeAsync()
        {
            var dataSources = SystemConfig.Instance.DataSources.JsonClone().ToArray();
            var userInfo = await GetCurrentUserInfoAsync(_httpContextAccessor.HttpContext, _context);
            UserInfo = userInfo;
            TenantKey = userInfo.TenantKey;
            dataSources.First(e => e.Name == "Main").ConnectionString = userInfo.ConnectionString;

            if (dataSources.Length != 1) throw new ApplicationException("DataSource must be one.");

            DbAccess = new DbAccessor(dataSources);
            TemporaryFileManager = new TemporaryFileManager(DbAccess, SystemConfig.Instance.TemporaryFileTableInfo, SystemConfig.Instance.FileStorages);
            ModuleDataIO = new CustomizedModuleDataIO(DesignerService.GetDesignData(TenantKey), this, DbAccess, TemporaryFileManager);
        }

        public async Task<string> GetCurrentUserIdAsync()
            => (await GetCurrentUserInfoAsync(_httpContextAccessor.HttpContext, _context)).UserId;


        public sealed class TenantCompanyInfo
        {
            public string Key { get; set; } = "";
            public string Connection_String { get; set; } = "";
            public string License_String { get; set; } = "";
        }

        public static async Task<MultiTenantUserInfo> GetCurrentUserInfoAsync(HttpContext? httpContext, ApplicationDbContext context)
        {
            var userName = httpContext?.User?.Identity?.Name;

            TenantCompanyInfo? result = null;
            {
                await using var conn = new NpgsqlConnection(SystemConfig.Instance.DefaultConnectionString);
                result = await conn.QuerySingleOrDefaultAsync<TenantCompanyInfo>(
                    @"
                    SELECT 
                      tc.key,
                      tc.connection_string,
                      tc.license_string
                    FROM 
                      ""AspNetUsers"" au
                    JOIN 
                      tenant_company tc
                    ON 
                      au.tenant_key = tc.key
                    WHERE 
                      au.user_name = @p1;",
                    new { p1 = userName });
                if (result == null) return new();
            }
            long? id = null;
            {
                await using var conn = new NpgsqlConnection(result.Connection_String);
                id = conn.QuerySingleOrDefault<long?>(@"SELECT id FROM app_user WHERE email = @userName LIMIT 1;", new { userName });
            }

            return new()
            {
                UserId = id?.ToString() ?? string.Empty,
                TenantKey = result.Key,
                ConnectionString = result.Connection_String,
                LicenseString = result.License_String
            };
        }
    }

    public class MultiTenantUserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string TenantKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string LicenseString { get; set; } = string.Empty;
    }
}
