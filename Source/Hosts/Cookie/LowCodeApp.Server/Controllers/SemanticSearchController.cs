using Codeer.LowCode.Blazor.Extras.SemanticSearch;
using Codeer.LowCode.Blazor.Extras.Server.AI.SemanticSearch;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using LowCodeApp.Server.AI;
using LowCodeApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LowCodeApp.Server.Controllers
{
    //SemanticSearchField (意味検索) の再索引 API (スクリプトの Reindex / ReindexMissing の受け口)
    [Authorize, AutoValidateAntiforgeryToken]
    [ApiController]
    [Route("api/semantic_search/reindex")]
    public class SemanticSearchController : ControllerBase, IAsyncDisposable
    {
        static SemanticSearchService _semanticSearch => SemanticSearchIndex.Service;

        readonly DataService _dataService;

        public SemanticSearchController(DataService dataService)
            => _dataService = dataService;

        public async ValueTask DisposeAsync()
            => await _dataService.DisposeAsync();

        //ジョブの所有者。他人のジョブは見えない (同じモジュールの走行中ジョブに合流した人は見える)
        string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;

        [HttpPost, Audit(AuditCategory.Admin)]
        public async Task<ActionResult<SemanticSearchReindexResponse>> Start([FromBody] SemanticSearchReindexRequest request)
        {
            //バックグラウンドではリクエストの HttpContext が無いので、ユーザー Id を固定した DataService を開いて渡す
            var userId = await _dataService.GetCurrentUserIdAsync();
            var requestId = await _semanticSearch.StartReindexAsync(Owner, request, _dataService.ModuleDataIO, () =>
            {
                var dataService = new DataService(userId);
                return new SemanticSearchReindexScope(dataService.ModuleDataIO, dataService.DbAccess, dataService);
            });
            return Accepted(new SemanticSearchReindexResponse { RequestId = requestId });
        }

        [HttpGet("{requestId}")]
        public ActionResult<SemanticSearchReindexStatusResponse> Status(string requestId)
        {
            var status = _semanticSearch.GetReindexStatus(Owner, requestId);
            return status == null ? NotFound() : status;
        }

        [HttpDelete("{requestId}")]
        public IActionResult Cancel(string requestId)
            => _semanticSearch.CancelReindex(Owner, requestId) ? NoContent() : NotFound();
    }
}
