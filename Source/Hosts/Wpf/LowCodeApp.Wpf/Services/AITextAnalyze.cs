using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Components.AppParts.Loading;
using Codeer.LowCode.Blazor.Extras.Designs;
using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.Extras.Server.FileManagement;

namespace LowCodeApp.Wpf.Services
{
    public class AITextAnalyze
    {
        LoadingService _loadingService;
        Codeer.LowCode.Blazor.RequestInterfaces.ILogger _logger;

        public AITextAnalyze(LoadingService loadingService, Codeer.LowCode.Blazor.RequestInterfaces.ILogger logger)
        {
            _loadingService = loadingService;
            _logger = logger;
        }

        public async Task<ModuleData?> FileToModuleDataAsync(string moduleName, string fieldName, string fileName, StreamContent content)
              => await CheckoutException(async dataIO =>
              {
                  var memoryStream = new MemoryStream();
                  await content.CopyToAsync(memoryStream);
                  memoryStream.Position = 0;
                  return await new AITextAnalyzeService(SystemConfig.Instance.AISettings).AnalyzeFileAsync(
                      dataIO, DesignerService.GetDesignData().Modules,
                      moduleName, fieldName, fileName, memoryStream);
              }, null);

        public async Task<ModuleData?> TextToModuleDataAsync(string moduleName, string fieldName, string text)
            => await CheckoutException(async dataIO => await new AITextAnalyzeService(SystemConfig.Instance.AISettings).AnalyzeTextAsync(
                dataIO, DesignerService.GetDesignData().Modules,
                moduleName, fieldName, text ?? string.Empty), null);

        async Task<T> CheckoutException<T>(Func<CustomizedModuleDataIO, Task<T>> f, T errResult)
        {
            using var scope = _loadingService.StartLoading();
            await using var dbAccess = new DbAccessor(SystemConfig.Instance.DataSources);
            var temporaryFileManager = new TemporaryFileManager(dbAccess, SystemConfig.Instance.TemporaryFileTableInfo, FileStorageTable.Storages);
            var dataIO = new CustomizedModuleDataIO(DesignerService.GetDesignData(), new AuthenticationContext(), dbAccess, temporaryFileManager);
            try
            {
                return await f(dataIO);
            }
            catch
            {
                await _logger.Error("AI analysis failed. Retrying may succeed.");
                return errResult;
            }
        }
    }
}
