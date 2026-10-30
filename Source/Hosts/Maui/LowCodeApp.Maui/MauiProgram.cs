using Codeer.LowCode.Blazor.RequestInterfaces;
using LowCodeApp.Maui.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LowCodeApp.Client.Shared.Services;

namespace LowCodeApp.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Configuration.AddConfiguration(LoadAppSettings());

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSharedServices();
            builder.Services.AddScoped<INavigationService, NavigationService>();

            //HTTP は ServerSettings のサーバー URL へ (既定は appsettings.json、設定画面で変更可)。ServerConnection が認証 Cookie と antiforgery トークンを持つ
            var baseUrl = builder.Configuration["Server:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl)) throw new InvalidOperationException("Server:BaseUrl is not set in appsettings.json.");
            ServerSettings.DefaultBaseUrl = baseUrl;
            ServerSettings.LoginCallbackUrl = builder.Configuration["Server:LoginCallbackUrl"] ?? ServerSettings.LoginCallbackUrl;
            builder.Services.AddSingleton<ServerConnection>();
            builder.Services.AddScoped(sp => sp.GetRequiredService<ServerConnection>().CreateHttpClient());

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        //appsettings.json は MauiAsset として同梱。appsettings.Development.json があれば上書きする
        static IConfiguration LoadAppSettings()
        {
            var config = new ConfigurationBuilder();
            AddJsonAsset(config, "appsettings.json", optional: false);
            AddJsonAsset(config, "appsettings.Development.json", optional: true);
            return config.Build();
        }

        static void AddJsonAsset(IConfigurationBuilder config, string fileName, bool optional)
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                memory.Position = 0;
                config.AddJsonStream(memory);
            }
            catch (FileNotFoundException) when (optional) { }
        }
    }
}
