using Codeer.LowCode.Blazor.Extras.Server.Auth;

namespace LowCodeApp.Server.Services
{
    /// <summary>
    /// appsettings の外部 IdP 設定 → IExternalLoginProvider の対応表。IdP の種類ごとに独立したセクションを読む
    /// (メールの MailSenderTable / ファイル保存の FileStorageTable と同じ考え方)。ClientId が書かれているものだけ並べる。
    /// 独自の IdP を足すときは IExternalLoginProvider を実装 (多くは OidcLoginProvider を継承) してここに追加する。
    /// </summary>
    public static class ExternalLoginTable
    {
        public static List<IExternalLoginProvider> Create(IConfiguration config)
        {
            var list = new List<IExternalLoginProvider>();

            var entra = config.GetSection("EntraLogin").Get<EntraLoginSettings>();
            if (entra?.IsConfigured == true) list.Add(new EntraLoginProvider(entra));

            var google = config.GetSection("GoogleLogin").Get<GoogleLoginSettings>();
            if (google?.IsConfigured == true) list.Add(new GoogleLoginProvider(google));

            var cognito = config.GetSection("CognitoLogin").Get<CognitoLoginSettings>();
            if (cognito?.IsConfigured == true) list.Add(new CognitoLoginProvider(cognito));

            //汎用 OpenID Connect (Keycloak / Auth0 / LINE 等) は複数置ける。Name が URL とボタンの識別になる
            foreach (var e in config.GetSection("OidcLogins").Get<OidcLoginSettings[]>() ?? [])
            {
                if (e.IsConfigured) list.Add(new OidcLoginProvider(e));
            }
            return list;
        }
    }
}
