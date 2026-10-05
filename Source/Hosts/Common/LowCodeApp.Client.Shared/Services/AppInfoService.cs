using System.Collections.Concurrent;
using Codeer.LowCode.Bindings.ApexCharts;
using Codeer.LowCode.Blazor.Components.AppParts.Loading;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.DesignLogic.Transfer;
using Codeer.LowCode.Blazor.Extras;
using Codeer.LowCode.Blazor.Repository;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Repository.Match;
using Codeer.LowCode.Blazor.RequestInterfaces;
using Codeer.LowCode.Blazor.Extras.Services;
using Codeer.LowCode.Blazor.Script;
using Codeer.LowCode.Blazor.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace LowCodeApp.Client.Shared.Services
{
    public interface IAppInfoServiceExtension : IAppInfoService
    {
        Task InitializeAppAsync();
        void SetCurrentUserId(string id);
    }

    public class AppInfoService : IAppInfoServiceExtension
    {
        readonly NavigationManager _navigationManager;
        readonly IHttpService _http;
        readonly HttpClient _httpClient;
        readonly ScriptRuntimeTypeManager _scriptRuntimeTypeManager = new();
        readonly LoadingService _loadingService;
        HubConnection? _hubConnection;
        readonly ConcurrentDictionary<string, Task<byte[]?>> _resources = new();
        DesignData? _design;
        SystemConfigForFront? _config;
        LocalizeService? _localizeService;

        public ModuleData? CurrentUserData { get; private set; }

        public string CurrentUserId { get; private set; } = string.Empty;

        public DesignData GetDesignData() => _design ?? new();

        public bool CanScriptDebug => _config?.CanScriptDebug == true;

        public string Localize(string text)
            => _localizeService?.Localize(text) ?? text;

        public AppInfoService(IHttpService http, HttpClient httpClient, LoadingService loadingService, NavigationManager navigationManager, ILogger logger, IToastService toaster)
        {
            _http = http;
            _httpClient = httpClient;
            _navigationManager = navigationManager;
            _loadingService = loadingService;
            _scriptRuntimeTypeManager.AddService(loadingService);
            _scriptRuntimeTypeManager.AddType<LoadingService.LoadingScope>();
            ApexChartsClientInitializer.Initialize(this);
            ExtrasClientInitializer.Initialize(this, http, logger, toaster);
        }
        public void SetCurrentUserId(string id) => CurrentUserId = id;

        public async Task InitializeAppAsync()
        {
            using var scope = _loadingService.StartLoading(int.MaxValue);

            if (_design != null) return;

            var hotReloadTask = InitializeHotReloadAsync();

            using var designDataStream = await _http.GetFromStreamAsync($"/api/module_data/design");
            _design = DesignDataTransferLogic.ToDesignData(designDataStream);

            var localizeTask = this.CreateLocalizeService();

            var currentUserModule = _design.Modules.Find(_design.AppSettings.CurrentUserModuleDesignName);
            if (currentUserModule != null && !string.IsNullOrEmpty(CurrentUserId))
            {
                var currentUserRequest = new GetListRequest
                {
                    Condition = new()
                    {
                        ModuleName = currentUserModule.Name,
                        Condition = new FieldValueMatchCondition { SearchTargetVariable = "Id.Value", Comparison = MatchComparison.Equal, Value = MultiTypeValue.Create(CurrentUserId) }
                    }
                };
                CurrentUserData = (await ModuleDataService.GetListAsync(_http, [currentUserRequest]))?.FirstOrDefault()?.Items.FirstOrDefault();
            }

            _localizeService = await localizeTask;
            await hotReloadTask;
        }

        public ScriptRuntimeTypeManager GetScriptRuntimeTypeManager()
        => _scriptRuntimeTypeManager;

        //Resources belong to the design, and the design is fetched once per app run (see InitializeAppAsync),
        //so a resource cannot change underneath us while the app is running. Components do ask for the same
        //one more than once though - measured on iOS, the same file went out twice about 100ms apart from the
        //same call site - and on a device every one of those is a real round trip. Cache the bytes, hand out
        //a fresh stream each time so the caller still owns what it reads, and share one in-flight request
        //between callers that ask at the same moment.
        public async Task<MemoryStream?> GetResourceAsync(string resourcePath)
        {
            var bytes = await _resources.GetOrAdd(resourcePath, FetchResourceAsync);
            if (bytes != null) return new MemoryStream(bytes);

            //Do not let one failure poison the cache for the rest of the run.
            _resources.TryRemove(resourcePath, out _);
            return null;
        }

        async Task<byte[]?> FetchResourceAsync(string resourcePath)
        {
            var result = await _http.GetAsync($"/api/module_data/resource?resource={resourcePath}", false);
            if (result == null) return null;
            using var stream = await result.Content.ReadAsStreamAsync();
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            return memory.ToArray();
        }

        async Task InitializeHotReloadAsync()
        {
            _config ??= await _http.GetFromJsonAsync<SystemConfigForFront>($"/api/module_data/config");
            if (_config?.UseHotReload != true || _hubConnection != null) return;

            //The hub lives on the server the HttpClient talks to. In the browser that is also the page origin, but in a
            //BlazorWebView (MAUI/WPF) the page origin is a local pseudo host, so NavigationManager cannot be used here.
            var hubUrl = _httpClient.BaseAddress != null
                ? new Uri(_httpClient.BaseAddress, "hot_reload_hub")
                : _navigationManager.ToAbsoluteUri("/hot_reload_hub");
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .Build();

            _hubConnection.On("ExecuteHotReload", () => _navigationManager.Refresh(true));
            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception)
            {
                //Hot reload is a development convenience; the app must still start when the hub is unreachable
                //(e.g. a native client that cannot validate the development certificate).
                _hubConnection = null;
            }
        }
    }
}
