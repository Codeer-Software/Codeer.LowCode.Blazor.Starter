namespace LowCodeApp.Maui.Services
{
    /// <summary>
    /// Server URL setting. Stored with MAUI Preferences (plain app storage, outside Blazor) so it can be
    /// changed from the native SettingsPage. The default comes from appsettings.json.
    /// </summary>
    public static class ServerSettings
    {
        const string BaseUrlKey = "Server.BaseUrl";

        /// <summary>Set once at startup from appsettings.json (Server:BaseUrl).</summary>
        public static string DefaultBaseUrl
        {
            get => _defaultBaseUrl;
            set =>
#if WINDOWS
                // appsettings.json targets the Android emulator, which reaches the host PC via the special
                // address 10.0.2.2. That address is meaningless on Windows itself (there is no host to reach
                // through it), so when running as a Windows desktop app it must be read as plain localhost.
                _defaultBaseUrl = value.Replace("10.0.2.2", "localhost");
#else
                _defaultBaseUrl = value;
#endif
        }

        static string _defaultBaseUrl = string.Empty;

        /// <summary>
        /// 外部 IdP ログイン後にシステムブラウザから戻ってくる URL (Server:LoginCallbackUrl、既定 lowcodeapp://auth)。
        /// スキームは Android の WebAuthenticatorCallbackActivity / iOS の Info.plist CFBundleURLTypes、
        /// およびサーバー側の MobileLoginCallbackUrl と一致させる。
        /// </summary>
        public static string LoginCallbackUrl { get; set; } = "lowcodeapp://auth";

        public static string BaseUrl
        {
            get => Preferences.Default.Get(BaseUrlKey, string.Empty) is { Length: > 0 } saved ? saved : DefaultBaseUrl;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || value == DefaultBaseUrl) Preferences.Default.Remove(BaseUrlKey);
                else Preferences.Default.Set(BaseUrlKey, value);
            }
        }

        public static bool IsValidUrl(string? text)
            => Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
