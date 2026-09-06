using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Codeer.LowCode.Blazor.Utils;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using LowCodeApp.Client;
using LowCodeApp.Server.Services;
using Codeer.LowCode.Blazor.Extras.Services;
using Codeer.LowCode.Blazor.Extras.Server.Auth;

namespace LowCodeApp.Server.Controllers
{
    [ApiController, AutoValidateAntiforgeryToken]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        const string LoginPage = "/login.html";

        readonly DataService _dataService;
        readonly ExternalLoginService _externalLogins;

        public AccountController(DataService dataService, ExternalLoginService externalLogins)
        {
            _dataService = dataService;
            _externalLogins = externalLogins;
        }

        [Authorize]
        [HttpGet("current_user")]
        public StringWrapper GetCurrentUser()
            => new(DataService.GetCurrentUserId(HttpContext));

        //Sole issuer of the antiforgery token cookie. login.html fetches this before
        //every login attempt, and the WASM client fetches it at startup.
        [HttpGet("antiforgery")]
        public IActionResult Antiforgery()
        {
            CookieAuthentication.AppendAntiforgeryTokenCookie(HttpContext);
            return NoContent();
        }

        //ログイン画面に出す選択肢: ID/パスワードのフォームと、外部 IdP ごとのボタン (appsettings の EntraLogin / GoogleLogin / CognitoLogin / OidcLogins)
        [HttpGet("login_options")]
        public object LoginOptions()
            => new { Password = SystemConfig.Instance.AllowPasswordLogin, Providers = _externalLogins.Options };

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginInfo? loginInfo)
        {
            if (loginInfo == null) throw new ArgumentException(nameof(loginInfo));
            if (!SystemConfig.Instance.AllowPasswordLogin) return NotFound();

            var tableInfo = SystemConfig.Instance.PasswordCheckUserTableInfo;
            var designData = DesignerService.GetDesignData();

            var dataSourceName = designData.Modules.Find(designData.AppSettings.CurrentUserModuleDesignName)?.DataSourceName ?? string.Empty;

            var conn = _dataService.DbAccess.GetConnection(dataSourceName);

            var user = (await conn.QueryAsync<PasswordCheckUser>(
                $"SELECT {tableInfo.IdColumn} AS Id, {tableInfo.UserNameColumn} AS UserName, {tableInfo.HashColumn} AS Hash, {tableInfo.SaltColumn} AS Salt FROM {tableInfo.TableName} WHERE {tableInfo.UserNameColumn} = @UserName",
                new { UserName = loginInfo.Id })).FirstOrDefault();

            if (user == null) return Unauthorized();

            if (!PasswordHashHelper.VerifyHash(loginInfo.Password ?? string.Empty, user.Hash, user.Salt))
                return Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, loginInfo.Id ?? string.Empty),
                new(ClaimTypes.NameIdentifier, user.Id)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = loginInfo.IsPersistent });

            return Ok();
        }

        //外部 IdP: ブラウザがここに遷移 (GET) すると IdP へ送られる。IdP が本人確認した後、ExternalLoginUserResolver が
        //ユーザー行に解決し、パスワードログインと同じ Cookie を発行する。
        //mobile=true はネイティブアプリ (システムブラウザ) の流れで、Cookie の代わりに使い捨てチケットをアプリへ返す
        [HttpGet("login/{provider}")]
        public IActionResult ExternalLogin(string provider, string? returnUrl, bool mobile = false)
            => _externalLogins.Challenge(this, provider, returnUrl, mobile);

        //ネイティブアプリ: システムブラウザで受け取った使い捨てチケットを認証 Cookie に交換する
        [HttpPost("login_ticket")]
        public async Task<IActionResult> LoginTicket(LoginTicket? ticket)
        {
            var redeemed = await _externalLogins.RedeemMobileTicketAsync(ticket?.Ticket);
            if (redeemed == null) return Unauthorized();

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                _externalLogins.CreatePrincipal(redeemed.Value.User, redeemed.Value.Provider),
                new AuthenticationProperties { IsPersistent = true });

            return Ok();
        }

        //外部 IdP (Entra ID 等) でサインインしたセッションは IdP 側のセッションも終わらせる必要があり、それはブラウザ遷移でしかできない
        //(fetch からは不可)。その場合は Cookie を残したまま ExternalLogout の URL を返し、クライアントがそこへ遷移する (二段構え)。
        //mobile=true (ネイティブアプリ) は Cookie を破棄するだけ
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(bool mobile = false)
        {
            if (!mobile)
            {
                var provider = await _externalLogins.GetSignOutProviderAsync(User);
                if (provider != null) return Ok(new LogoutResult { Redirect = $"/api/account/logout/{provider}" });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new LogoutResult());
        }

        //Cookie を破棄し IdP のセッションも終わらせてログイン画面へ戻る (GET: ブラウザ遷移)
        [HttpGet("logout/{provider}")]
        public Task<IActionResult> ExternalLogout(string provider)
            => _externalLogins.SignOutAsync(this, provider, LoginPage);
    }
}
