using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using LowCodeApp.Server.Data;

namespace LowCodeApp.Server
{
    public static class CookieAuthentication
    {
        public static void UseCookieAuthentication(this WebApplicationBuilder builder)
        {
            builder.Services.AddIdentityCore<ApplicationUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

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
                });

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

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

                if (!dbContext.Users.Any())
                {
                    userManager.CreateAsync(new ApplicationUser
                    {
                        Name = "admin",
                        UserName = "admin",
                        EmailConfirmed = true,
                    }, "Abcdefg123##").Wait();
                }
            }
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
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
        }
    }
}
