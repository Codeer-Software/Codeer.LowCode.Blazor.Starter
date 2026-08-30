using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras;
using Codeer.LowCode.Blazor.Extras.Services;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.RequestInterfaces;
using Codeer.LowCode.Blazor.Script;
using Microsoft.AspNetCore.Components;
using System.Windows;
using LowCodeApp.Client.Shared.Services;

namespace LowCodeApp.Wpf.Services
{
    public class AppInfoService : IAppInfoService, IDisposable
    {
        readonly ScriptRuntimeTypeManager _scriptRuntimeTypeManager = new();
        readonly NavigationManager _navigationManager;
        DesignData? _design;
        FileSystemWatcher? _fileWatcher;
        LocalizeService? _localizeService;

        public ModuleData? CurrentUserData { get; private set; }

        public string CurrentUserId { get; set; } = string.Empty;

        public bool IsDesignMode => false;

        public DesignData GetDesignData() => _design ?? new();

        public string Localize(string text)
            => _localizeService?.Localize(text) ?? text;

        public AppInfoService(IHttpService http, ILogger logger, IToastService toaster, NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            ExtrasClientInitializer.Initialize(this, http, logger, toaster);

            Directory.CreateDirectory(SystemConfig.Instance.DesignFileDirectory);
            _fileWatcher = new FileSystemWatcher
            {
                Path = SystemConfig.Instance.DesignFileDirectory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                Filter = "*.zip"
            };
            _fileWatcher.Changed += (_, _) =>
            {
                Application.Current.Dispatcher.Invoke(() => InvokeHotReload());
            };
            _fileWatcher.EnableRaisingEvents = true;

        }

        public async Task InitializeAppAsync()
        {
            if (_design != null) return;
            await Task.CompletedTask;
            _design = DesignerService.GetDesignDataForFront(null);
            _localizeService = await this.CreateLocalizeService();
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }

        public ScriptRuntimeTypeManager GetScriptRuntimeTypeManager()
            => _scriptRuntimeTypeManager;

        public async Task<MemoryStream?> GetResourceAsync(string resourcePath)
        {
            await Task.CompletedTask;
            return DesignerService.GetResource(resourcePath ?? string.Empty);
        }

        void InvokeHotReload() => _navigationManager.Refresh(true);
    }
}
