using Codeer.LowCode.Blazor;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Repository.Data;
using Codeer.LowCode.Blazor.Extras.Designs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LowCodeApp.Server.Services;

namespace LowCodeApp.Server.Controllers
{
    [Authorize, AutoValidateAntiforgeryToken]
    [ApiController]
    [Route("api/ai_text_analyze")]
    public class AITextAnalyzeController : ControllerBase
    {
        readonly DataService _dataService;

        public AITextAnalyzeController(DataService dataService)
            => _dataService = dataService;

        public async ValueTask DisposeAsync()
            => await _dataService.DisposeAsync();

        [HttpPost("file")]
        public async Task<ModuleData> FileToDataAsync(string? moduleName, string? fieldName, string? fileName)
        {
            await _dataService.InitializeAsync();

            var memoryStream = new MemoryStream();
            await Request.Body.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            try
            {
                var modules = _dataService.ModuleDataIO.DesignData.Modules;
                return await new AITextAnalyzeService(SystemConfig.Instance.AISettings).FileToDataAsync(
                    _dataService.ModuleDataIO, modules,
                    moduleName ?? string.Empty, GetRemarks(modules, moduleName, fieldName), fileName, memoryStream);
            }
            catch
            {
                throw new Exception("AI analysis failed. Retrying may succeed.");
            }
        }

        [HttpPost("text")]
        public async Task<ModuleData> TextToDataAsync(string? moduleName, string? fieldName, [FromForm] string? text)
        {
            await _dataService.InitializeAsync();

            try
            {
                var modules = _dataService.ModuleDataIO.DesignData.Modules;
                return await new AITextAnalyzeService(SystemConfig.Instance.AISettings).TextToDataAsync(
                    _dataService.ModuleDataIO, modules,
                    moduleName ?? string.Empty, GetRemarks(modules, moduleName, fieldName), text ?? string.Empty);
            }
            catch
            {
                throw new Exception("AI analysis failed. Retrying may succeed.");
            }
        }

        static string GetRemarks(IModuleDesigns modules, string? moduleName, string? fieldName)
        {
            var mod = modules.Find(moduleName ?? string.Empty);
            var field = mod?.Fields.FirstOrDefault(e => e.Name == fieldName) as AITextAnalyzerFieldDesign;
            if (field == null) throw LowCodeException.Create($"Invalid Field {moduleName}.{fieldName}");
            return field.Remarks;
        }
    }
}
