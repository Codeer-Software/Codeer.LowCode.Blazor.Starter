using LowCodeApp.Server.Extras;
using Codeer.LowCode.Blazor.Extras.Api;
using Codeer.LowCode.Blazor.Extras.Server.Api;
using LowCodeApp.Server.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LowCodeApp.Server.Controllers
{
    //Extras の機能の受け口。1 本の POST で受け、要求の Feature / Action で振り分ける。出す機能は Extras/ExtrasApiTable.cs
    [Authorize, AutoValidateAntiforgeryToken]
    [ApiController]
    [Route("api/extras")]
    public class ExtrasController : ControllerBase, IAsyncDisposable
    {
        static readonly ExtrasApi _api = ExtrasApiTable.Create();

        readonly DataService _dataService;
        readonly ILogger<ExtrasController> _logger;

        public ExtrasController(DataService dataService, ILogger<ExtrasController> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
            => await _dataService.DisposeAsync();

        [HttpPost]
        public async Task<ActionResult<ExtrasResponse>> ExecuteAsync(ExtrasRequest request)
        {
            await _dataService.InitializeAsync();
            var response = await _api.ExecuteAsync(request, new ExtrasApiContext
            {
                ModuleDataIO = _dataService.ModuleDataIO,
                DesignData = DesignerService.GetDesignData(_dataService.TenantKey),
                DbAccess = _dataService.DbAccess,
                UserId = await _dataService.GetCurrentUserIdAsync(),
                Logger = _logger,
            });
            return response == null ? NotFound() : response;
        }
    }
}
