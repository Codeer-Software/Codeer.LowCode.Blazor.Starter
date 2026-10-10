using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Codeer.LowCode.Blazor.Utils;
using Microsoft.AspNetCore.Authorization;
using LowCodeApp.Client;
using LowCodeApp.Server.Services;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using Codeer.LowCode.Blazor.Extras.Server.Auth;
using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.Extras.Server.Mail;
using Microsoft.Extensions.Caching.Distributed;

namespace LowCodeApp.Server.Controllers
{
    [ApiController, AutoValidateAntiforgeryToken]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        const string LoginPage = "/login.html";

        readonly DataService _dataService;
        readonly ExternalLoginService _externalLogins;
        readonly IDistributedCache _cache;
        readonly AuditContext _audit;

        public AccountController(DataService dataService, ExternalLoginService externalLogins, IDistributedCache cache, AuditContext audit)
        {
            _dataService = dataService;
            _externalLogins = externalLogins;
            _cache = cache;
            _audit = audit;
        }

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

        //ログイン画面に出すもの (ID/パスワードのフォームの有無・外部 IdP のボタン)。見た目は login.html (Web) と Login.razor (MAUI) で変える
        [HttpGet("login_options")]
        public object LoginOptions()
            => new
            {
                Password = SystemConfig.Instance.AllowPasswordLogin,
                Providers = _externalLogins.Options,
            };

        [HttpPost("login"), Audit(AuditCategory.Authentication)]
        public async Task<IActionResult> Login(LoginInfo? loginInfo)
        {
            if (loginInfo == null) throw new ArgumentException(nameof(loginInfo));
            if (!SystemConfig.Instance.AllowPasswordLogin) return NotFound();
            _audit.Event.Detail = $"LoginName={loginInfo.Id}";

            var designData = _dataService.Design.DesignData;
            var accounts = LoginAccountStore.Create(designData, _dataService.DbAccess);
            if (accounts == null || !accounts.HasPassword) return NotFound();

            var account = await accounts.VerifyPasswordAsync(loginInfo.Id, loginInfo.Password);
            if (account == null) return Unauthorized();

            //二要素認証。コード検証が通ったとき (status: ok) だけ下のサインインへ進む
            var totp = TotpLogin.Create(designData, SystemConfig.Instance.TotpLogin, _dataService.DbAccess);
            if (totp != null)
            {
                var result = await totp.VerifyAsync(account.UserId, account.LoginName, loginInfo.TwoFactorCode);
                if (result.Status == TotpLoginStatus.InvalidCode) _audit.Deny($"LoginName={loginInfo.Id}; TwoFactor={result.Status}");
                else if (result.Status != TotpLoginStatus.Ok) _audit.Event.Detail += $"; TwoFactor={result.Status}";
                if (result.Status != TotpLoginStatus.Ok) return Ok(result);
            }
            else if (accounts.HasTwoFactorEmail)
            {
                var dispatcher = new MailDispatcher(SystemConfig.Instance.Mail, MailSenderTable.Create);
                var email = new EmailOtpLogin(SystemConfig.Instance.EmailOtpLogin,
                    message => dispatcher.SendAsync(SystemConfig.Instance.EmailOtpLogin.MailInfraName, message), _cache);
                var result = await email.VerifyAsync(account.UserId, account.TwoFactorEmail ?? string.Empty, loginInfo.TwoFactorCode);
                if (result.Status == EmailOtpLoginStatus.InvalidCode) _audit.Deny($"LoginName={loginInfo.Id}; TwoFactor={result.Status}");
                else if (result.Status != EmailOtpLoginStatus.Ok) _audit.Event.Detail += $"; TwoFactor={result.Status}";
                if (result.Status != EmailOtpLoginStatus.Ok) return Ok(result);
            }

            //監査ログのユーザー Id は Cookie を発行するときだけ入れる (二要素認証待ちの行には入れない)
            _audit.Event.UserId = account.UserId;

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, account.DisplayName),
                new(ClaimTypes.NameIdentifier, account.UserId)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(claimsIdentity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = loginInfo.IsPersistent });

            //サインイン・サインアウトで antiforgery トークンを発行し直す。
            //トークンは発行時のユーザーに紐づくので、匿名のときに取ったトークンは認証後の書き込みで
            //「The provided antiforgery token was meant for a different claims-based user」で拒否される。
            //ブラウザはログイン後にページを読み直して取り直すので表面化しないが、ネイティブアプリの
            //接続はサインインをまたいで生き続けるため、サーバー側から新しいトークンを返してやる必要がある。
            //SignInAsync は同じリクエストの HttpContext.User を更新しないので、先に差し替えてから発行する。
            HttpContext.User = principal;
            CookieAuthentication.AppendAntiforgeryTokenCookie(HttpContext);

            return Ok(new TotpLoginResult { Status = TotpLoginStatus.Ok });
        }

        //外部 IdP: ブラウザがここに遷移 (GET) すると IdP へ送られる。IdP が本人確認した後、ExternalLoginUserResolver が
        //ユーザー行に解決し、パスワードログインと同じ Cookie を発行する。
        //persistent=true は「ログイン状態を保持する」(ブラウザを閉じても残る Cookie)。
        //mobile=true はネイティブアプリ (システムブラウザ) の流れで、Cookie の代わりに使い捨てチケットをアプリへ返す
        [HttpGet("login/{provider}"), Audit(AuditCategory.Authentication)]
        public IActionResult ExternalLogin(string provider, string? returnUrl, bool mobile = false, bool persistent = false)
            => _externalLogins.Challenge(this, provider, returnUrl, mobile, persistent);

        //ネイティブアプリ: システムブラウザで受け取った使い捨てチケットを認証 Cookie に交換する
        [HttpPost("login_ticket"), Audit(AuditCategory.Authentication)]
        public async Task<IActionResult> LoginTicket(LoginTicket? ticket)
        {
            var redeemed = await _externalLogins.RedeemMobileTicketAsync(ticket?.Ticket);
            if (redeemed == null) return Unauthorized();
            _audit.Event.UserId = redeemed.Value.User.UserId;
            _audit.Event.Detail = $"Provider={redeemed.Value.Provider}";

            var principal = _externalLogins.CreatePrincipal(redeemed.Value.User, redeemed.Value.Provider);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            //ログインと同じ理由でトークンを発行し直す (上のコメント参照)
            HttpContext.User = principal;
            CookieAuthentication.AppendAntiforgeryTokenCookie(HttpContext);

            return Ok();
        }

        //外部 IdP でサインインしたセッションは IdP 側のログアウトにブラウザ遷移が要るので、Cookie を残したまま遷移先 URL を返す
        [Authorize]
        [HttpPost("logout"), Audit(AuditCategory.Authentication)]
        public async Task<IActionResult> Logout(bool mobile = false)
        {
            if (!mobile)
            {
                var provider = await _externalLogins.GetSignOutProviderAsync(User);
                if (provider != null) return Ok(new LogoutResult { Redirect = $"/api/account/logout/{provider}" });
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            //サインアウトでも同じ。匿名向けのトークンを返しておかないと、次のログインが弾かれて詰む
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            CookieAuthentication.AppendAntiforgeryTokenCookie(HttpContext);

            return Ok(new LogoutResult());
        }

        //Cookie を破棄し IdP のセッションも終わらせてログイン画面へ戻る (GET: ブラウザ遷移)
        [HttpGet("logout/{provider}"), Audit(AuditCategory.Authentication)]
        public Task<IActionResult> ExternalLogout(string provider)
            => _externalLogins.SignOutAsync(this, provider, LoginPage);

        //ログイン中の自分の認証アプリ (TOTP) の登録状態と解除 (Extras の MyTotpResetButtonField が使う)。
        //対象は常に自分。列は書き込み専用なのでここが唯一の入口
        [Authorize]
        [HttpGet("totp/status")]
        public async Task<IActionResult> GetTotpStatus()
        {
            var totp = TotpLogin.Create(_dataService.Design.DesignData, SystemConfig.Instance.TotpLogin, _dataService.DbAccess);
            if (totp == null) return Ok(new TotpStatus());
            var current = await totp.FindAsync(DataService.GetCurrentUserId(HttpContext));
            return Ok(new TotpStatus { Enabled = true, Registered = current?.IsConfirmed == true });
        }

        [Authorize]
        [HttpPost("totp/reset"), Audit(AuditCategory.Authentication)]
        public async Task<IActionResult> TotpReset()
        {
            var totp = TotpLogin.Create(_dataService.Design.DesignData, SystemConfig.Instance.TotpLogin, _dataService.DbAccess);
            if (totp == null) return NotFound();
            await totp.ResetAsync(DataService.GetCurrentUserId(HttpContext));
            return Ok(new TotpStatus { Enabled = true, Registered = false });
        }

        public class TotpStatus
        {
            public bool Enabled { get; set; }
            public bool Registered { get; set; }
        }
    }
}
