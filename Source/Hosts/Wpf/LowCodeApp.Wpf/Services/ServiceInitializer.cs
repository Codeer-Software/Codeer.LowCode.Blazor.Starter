using Codeer.LowCode.Blazor.RequestInterfaces;
using Sotsera.Blazor.Toaster.Core.Models;
using Codeer.LowCode.Blazor.Components.AppParts.Loading;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Codeer.LowCode.Blazor.Extras.Fields;
using Codeer.LowCode.Blazor.Extras.Services;

namespace LowCodeApp.Wpf.Services
{
    public static class ServiceInitializer
    {
        public static void AddSharedServices(this IServiceCollection services)
        {
            //デスクトップはフック(Excel.ConvertPdf / MailTransport.Handler / AITextAnalyzerField.XxxCoreAsync)で
            //ローカル処理するためエンドポイント(static プロパティ)は未設定
            services.AddScoped<IAppInfoService, AppInfoService>();
            services.AddScoped<IModuleDataService, ModuleDataService>();
            services.AddScoped<IUIService, UIService>();
            services.AddScoped<Codeer.LowCode.Blazor.RequestInterfaces.Services>();
            services.AddScoped<ILogger, Logger>();
            services.AddSingleton<LoadingService>();
            services.AddToaster(config =>
            {
                config.PositionClass = Defaults.Classes.Position.BottomRight;
                config.MaximumOpacity = 100;
                config.VisibleStateDuration = 1000 * 5;
                config.ShowTransitionDuration = 10;
                config.HideTransitionDuration = 500;
            });
            services.AddScoped<IToastService, ToastService>();
            services.AddScoped<IHttpService, HttpService>();
            services.AddScoped<AITextAnalyze>();
            AITextAnalyzerField.FileToModuleDataCoreAsync = (field, fileName, content)
                => field.Services.Provider.GetRequiredService<AITextAnalyze>()
                    .FileToModuleDataAsync(field.Module.Design.Name, field.Design.Name, fileName, content);
            AITextAnalyzerField.TextToModuleDataCoreAsync = (field, text)
                => field.Services.Provider.GetRequiredService<AITextAnalyze>()
                    .TextToModuleDataAsync(field.Module.Design.Name, field.Design.Name, text);

            services.AddScoped<INavigationService, NavigationService>();
            services.AddScoped(sp => new HttpClient());

            var cultureName = CultureInfo.CurrentCulture.Name;
            if (cultureName == "ja") cultureName = "ja-JP";
            var cultureInfo = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
    }
}
