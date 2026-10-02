using Codeer.LowCode.Blazor.Extras.AIChat;
using Codeer.LowCode.Blazor.Extras.Server.AI.Chat;
using Codeer.LowCode.Blazor.Extras.Server.AuditLog;
using LowCodeApp.Server.AI;
using LowCodeApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LowCodeApp.Server.Controllers
{
    //AIChatField の受け口。Agent は AI/AIChatAgentTable で選ばれる
    [Authorize, AutoValidateAntiforgeryToken]
    [ApiController]
    [Route("api/ai_chat")]
    public class AIChatController : ControllerBase, IAsyncDisposable
    {
        static AIChatService _aiChat => AIChatAgentTable.Service;

        readonly DataService _dataService;

        public AIChatController(DataService dataService)
            => _dataService = dataService;

        public async ValueTask DisposeAsync()
            => await _dataService.DisposeAsync();

        //ジョブと会話履歴の所有者。他人のジョブは見えない。表示名は同名・改名がありうるのでユーザー ID を優先する
        string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? string.Empty;

        //監査ログには誰がどの Agent に送ったかが残る (発言と、Agent が読んだ行は残らない)
        [HttpPost, Audit(AuditCategory.DataRead)]
        public async Task<ActionResult<AIChatSendResponse>> Send([FromBody] AIChatSendRequest request)
            => Accepted(new AIChatSendResponse { RequestId = await _aiChat.StartAsync(Owner, request, _dataService.ModuleDataIO) });

        [HttpGet("{requestId}")]
        public ActionResult<AIChatStatusResponse> Status(string requestId)
        {
            var status = _aiChat.GetStatus(Owner, requestId);
            return status == null ? NotFound() : status;
        }

        [HttpDelete("{requestId}")]
        public IActionResult Cancel(string requestId)
            => _aiChat.Cancel(Owner, requestId) ? NoContent() : NotFound();
    }
}
