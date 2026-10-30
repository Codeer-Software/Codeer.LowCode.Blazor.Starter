using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LowCodeApp.Server.Data;
using Codeer.LowCode.Blazor.Utils;
using Microsoft.AspNetCore.Authorization;
using LowCodeApp.Client;
using Codeer.LowCode.Blazor;
using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Controllers
{
    [ApiController, AutoValidateAntiforgeryToken]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [Authorize]
        [HttpGet("current_user")]
        public async Task<StringWrapper> GetCurrentUserAsync()
            => new((await DataService.GetCurrentUserInfoAsync(HttpContext, _context)).UserId);

        //antiforgery トークンの Cookie を発行する (login.html とアプリの起動時に取る)
        [HttpGet("antiforgery")]
        public IActionResult Antiforgery()
        {
            CookieAuthentication.AppendAntiforgeryTokenCookie(HttpContext);
            return NoContent();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginInfo? loginInfo)
        {
            if (loginInfo == null) throw new ArgumentException(nameof(loginInfo));

            var user = await _userManager.FindByNameAsync(loginInfo.Id ?? string.Empty);
            if (user == null) return Unauthorized();

            //総当たり対策: ASP.NET Identity のロックアウト (IdentityOptions.Lockout。既定は 5 回失敗で 5 分)
            var result = await _signInManager.CheckPasswordSignInAsync(user, loginInfo.Password ?? string.Empty, lockoutOnFailure: true);
            if (result.IsLockedOut) return StatusCode(StatusCodes.Status429TooManyRequests);
            if (result.Succeeded)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = loginInfo.IsPersistent });

                return new JsonResult("");
            }

            throw LowCodeException.Create("failed login");
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return new JsonResult("");
        }
    }
}
