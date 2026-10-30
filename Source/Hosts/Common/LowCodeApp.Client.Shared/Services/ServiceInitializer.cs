using Codeer.LowCode.Blazor.RequestInterfaces;
using Sotsera.Blazor.Toaster.Core.Models;
using Codeer.LowCode.Blazor.Components.AppParts.Loading;
using Codeer.LowCode.Blazor.Extras.Fields;
using Codeer.LowCode.Blazor.Extras.ScriptObjects;
using Codeer.LowCode.Blazor.Extras.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace LowCodeApp.Client.Shared.Services
{
    public static class ServiceInitializer
    {
        public static void AddSharedServices(this IServiceCollection services)
        {
            //Extras の機能は共通の API (/api/extras) に送るので設定不要。ここに並ぶのはファイル取込の API
            BulkFileReader.ParseFileEndPoint = "/api/module_data/parse_file";
            BulkFileTransferService.ListFileByDataEndPoint = "/api/module_data/list_file_by_data";
            BulkFileTransferService.BulkSubmitEndPoint = "/api/module_data/bulk_submit";

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

            var cultureName = CultureInfo.CurrentCulture.Name;
            if (cultureName == "ja") cultureName = "ja-JP";
            var cultureInfo = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
    }
}
