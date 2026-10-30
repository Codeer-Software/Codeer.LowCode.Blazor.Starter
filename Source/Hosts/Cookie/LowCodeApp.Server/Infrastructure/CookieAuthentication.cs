using LowCodeApp.Server.Extras;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using Codeer.LowCode.Blazor.Extras.Server.Auth;

namespace LowCodeApp.Server.Infrastructure
{
    public static class CookieAuthentication
    {
        public static void UseCookieAuthentication(this WebApplicationBuilder builder)
        {
            //ログイン途中の一時データ (使い捨てチケット・メール認証コード・ロックアウトの回数) の置き場。プロセス内メモリなので単一インスタンス向け。
            //複数インスタンスでは AddStackExchangeRedisCache 等の共有キャッシュに差し替える (複数のアプリで共有するときは InstanceName で分ける)
            builder.Services.AddDistributedMemoryCache();

            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => {
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    };
                })
                //外部 IdP (Entra ID / Google / AWS Cognito / OIDC)。並べるプロバイダは Extras/ExternalLoginTable が組み立てる
                .AddExternalLogins(ExternalLoginTable.Create(), o => o.MobileCallbackUrl = SystemConfig.Instance.MobileLoginCallbackUrl);
            builder.Services.AddScoped<IExternalLoginUserResolver, ExternalLoginUserResolver>();

            //CSRF
            builder.Services.AddAntiforgery(options => {
                options.HeaderName = "X-ANTIFORGERY-TOKEN";
            });
        }

        public static void UseCookieAuthentication(this WebApplication app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            _ = CreateInitialUserAsync(app);
        }

        public static void AppendAntiforgeryTokenCookie(HttpContext ctx)
        {
            var anti = ctx.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = anti.GetAndStoreTokens(ctx);
            ctx.Response.Cookies.Append(
                "X-ANTIFORGERY-TOKEN",
                tokens.RequestToken ?? string.Empty,
                new CookieOptions
                {
                    HttpOnly = false,
                    //HTTPS のリクエストにだけ Secure を付ける (HTTP で動かす構成でも送られるように)
                    Secure = ctx.Request.IsHttps,
                    SameSite = SameSiteMode.Lax
                });
        }

        //ユーザーが 0 件なら admin / admin を作る (表・列はユーザーモジュールのデザインから。パスワードログインがある構成だけ)。
        static async Task CreateInitialUserAsync(WebApplication app)
        {
            await using var dbAccessor = new DbAccessor(SystemConfig.Instance.DataSources);
            var designData = AppDesign.Store.GetDesignData();
            var accounts = LoginAccountStore.Create(designData, dbAccessor);
            if (accounts == null || !accounts.HasPassword || await accounts.AnyAsync()) return;
            var userId = await accounts.AddAsync("admin", "admin");
            try
            {
                await app.Services.GetRequiredService<AuditLogger>().WriteAsync(new AuditEvent
                {
                    Category = AuditCategory.System,
                    Action = "Account.InitialUserCreated",
                    Targets = { new AuditTarget { Module = designData.AppSettings.CurrentUserModuleDesignName, Id = userId, Operation = "Add" } },
                    Detail = "LoginName=admin",
                });
            }
            catch (AuditLogException)
            {
            }
        }
    }
}
